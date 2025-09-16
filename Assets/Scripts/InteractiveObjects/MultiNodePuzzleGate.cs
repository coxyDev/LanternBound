using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Puzzle gate that requires multiple nodes to be activated simultaneously
/// Demonstrates Solar Flare's multi-node activation capability
/// </summary>
public class MultiNodePuzzleGate : MonoBehaviour
{
    [Header("Gate Configuration")]
    [SerializeField] private string _gateId = "";
    [SerializeField] private GateType _gateType = GateType.Simultaneous;
    [SerializeField] private float _simultaneousWindow = 2f; // Time window for simultaneous activation
    [SerializeField] private bool _requiresAllNodes = true;
    [SerializeField] private int _minimumActiveNodes = 2;

    [Header("Node Requirements")]
    [SerializeField] private List<LightPuzzleNode> _requiredNodes = new List<LightPuzzleNode>();
    [SerializeField] private LightPuzzleNode.NodeGroup _nodeGroup = LightPuzzleNode.NodeGroup.Alpha;
    [SerializeField] private bool _autoDetectNodes = true;
    [SerializeField] private float _nodeDetectionRadius = 10f;

    [Header("Gate Behavior")]
    [SerializeField] private float _openDuration = 10f; // How long gate stays open
    [SerializeField] private bool _staysOpenPermanently = false;
    [SerializeField] private bool _canReopenAfterClose = true;
    [SerializeField] private float _reopenCooldown = 5f;

    [Header("Visual Components")]
    [SerializeField] private Animator _gateAnimator;
    [SerializeField] private SpriteRenderer _gateRenderer;
    [SerializeField] private BoxCollider2D _gateCollider;
    [SerializeField] private Light2D _gateLight;
    [SerializeField] private ParticleSystem _activationParticles;
    [SerializeField] private ParticleSystem _closingWarningParticles;

    [Header("Progress Indicators")]
    [SerializeField] private List<Light2D> _progressLights = new List<Light2D>();
    [SerializeField] private LineRenderer _connectionLines;
    [SerializeField] private bool _showNodeConnections = true;

    [Header("Audio")]
    [SerializeField] private AudioClip _gateOpenSound;
    [SerializeField] private AudioClip _gateCloseSound;
    [SerializeField] private AudioClip _nodeActivationSound;
    [SerializeField] private AudioClip _puzzleSolvedSound;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    public enum GateType
    {
        Simultaneous,   // All nodes must be active at the same time
        Sequential,     // Nodes must be activated in specific order
        Timed,          // Nodes must be activated within time limit
        Cascade,        // First node opens for limited time, then next, etc.
        Solar,          // Specifically designed for Solar Flare multi-activation
        Memory          // Remembers which nodes have been activated
    }

    // State tracking
    public bool IsOpen { get; private set; }
    public bool IsPermanentlyOpen { get; private set; }
    public bool IsOnCooldown { get; private set; }
    public float OpenTimeRemaining { get; private set; }
    public int ActiveNodeCount { get; private set; }
    public int RequiredNodeCount => _requiredNodes.Count;

    // Internal state
    private Dictionary<LightPuzzleNode, float> _nodeActivationTimes;
    private List<LightPuzzleNode> _currentlyActiveNodes;
    private float _cooldownRemaining;
    private AudioSource _audioSource;
    private Coroutine _gateStateCoroutine;

    // Events
    public static System.Action<MultiNodePuzzleGate> OnGateOpened;
    public static System.Action<MultiNodePuzzleGate> OnGateClosed;
    public static System.Action<MultiNodePuzzleGate, float> OnGateProgress; // Gate, progress 0-1

    // Visual state colors
    private Color _closedColor = new Color(0.8f, 0.3f, 0.3f);
    private Color _progressColor = new Color(0.8f, 0.8f, 0.3f);
    private Color _openColor = new Color(0.3f, 0.8f, 0.3f);
    private Color _warningColor = new Color(1f, 0.5f, 0f);

    private void Awake()
    {
        _nodeActivationTimes = new Dictionary<LightPuzzleNode, float>();
        _currentlyActiveNodes = new List<LightPuzzleNode>();
        _audioSource = GetComponent<AudioSource>();

        // Auto-generate ID if empty
        if (string.IsNullOrEmpty(_gateId))
        {
            _gateId = $"Gate_{GetInstanceID()}";
        }

        SetupComponents();

        if (_autoDetectNodes)
        {
            AutoDetectNodes();
        }

        ValidateNodeSetup();
    }

    private void Start()
    {
        // Subscribe to node events
        LightPuzzleNode.OnNodeActivated += HandleNodeActivated;
        LightPuzzleNode.OnNodeDeactivated += HandleNodeDeactivated;

        UpdateVisualState();
        UpdateProgressIndicators();

        if (_showNodeConnections)
        {
            CreateNodeConnections();
        }

        if (_debugMode)
        {
            Debug.Log($"✓ Multi-Node Puzzle Gate initialized: {_gateId}");
            Debug.Log($"  Gate Type: {_gateType}");
            Debug.Log($"  Required Nodes: {_requiredNodes.Count}");
            Debug.Log($"  Node Group: {_nodeGroup}");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        LightPuzzleNode.OnNodeActivated -= HandleNodeActivated;
        LightPuzzleNode.OnNodeDeactivated -= HandleNodeDeactivated;
    }

    private void SetupComponents()
    {
        // Setup gate collider (closed by default)
        if (_gateCollider == null)
            _gateCollider = GetComponent<BoxCollider2D>();

        if (_gateCollider != null)
        {
            _gateCollider.isTrigger = false; // Solid when closed
        }

        // Setup gate renderer
        if (_gateRenderer == null)
            _gateRenderer = GetComponent<SpriteRenderer>();

        if (_gateRenderer != null && _gateRenderer.sprite == null)
        {
            _gateRenderer.sprite = CreateGateSprite();
        }

        // Setup gate light
        if (_gateLight == null)
        {
            GameObject lightObj = new GameObject("GateLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _gateLight = lightObj.AddComponent<Light2D>();
        }

        _gateLight.lightType = Light2D.LightType.Point;
        _gateLight.intensity = 1f;
        _gateLight.pointLightInnerRadius = 0.5f;
        _gateLight.pointLightOuterRadius = 4f;
        _gateLight.color = _closedColor;

        // Setup progress lights
        if (_progressLights.Count == 0)
        {
            CreateProgressLights();
        }
    }

    private Sprite CreateGateSprite()
    {
        // Create a simple gate sprite
        int width = 64;
        int height = 128;
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        // Create gate pattern
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isFrame = x < 4 || x >= width - 4 || y < 4 || y >= height - 4;
                bool isBar = (x % 8 < 4) && y > height * 0.2f && y < height * 0.8f;

                if (isFrame || isBar)
                {
                    pixels[y * width + x] = Color.gray;
                }
                else
                {
                    pixels[y * width + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private void AutoDetectNodes()
    {
        _requiredNodes.Clear();

        // Find all nodes in detection radius
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _nodeDetectionRadius);

        foreach (var collider in colliders)
        {
            var node = collider.GetComponent<LightPuzzleNode>();
            if (node != null && (node.GetNodeGroup() == _nodeGroup || _nodeGroup == LightPuzzleNode.NodeGroup.None))
            {
                _requiredNodes.Add(node);
            }
        }

        if (_debugMode)
        {
            Debug.Log($"Auto-detected {_requiredNodes.Count} nodes for gate {_gateId}");
        }
    }

    private void ValidateNodeSetup()
    {
        // Remove null nodes
        _requiredNodes.RemoveAll(node => node == null);

        // Warn about setup issues
        if (_requiredNodes.Count == 0)
        {
            Debug.LogWarning($"Gate {_gateId} has no required nodes!");
        }
        else if (_requiredNodes.Count < _minimumActiveNodes)
        {
            Debug.LogWarning($"Gate {_gateId} requires {_minimumActiveNodes} active nodes but only has {_requiredNodes.Count} total nodes");
        }
    }

    private void CreateProgressLights()
    {
        _progressLights.Clear();

        for (int i = 0; i < _requiredNodes.Count; i++)
        {
            GameObject lightObj = new GameObject($"ProgressLight_{i}");
            lightObj.transform.SetParent(transform);

            // Position lights in a line above the gate
            float spacing = 1f;
            float totalWidth = (_requiredNodes.Count - 1) * spacing;
            float startX = -totalWidth * 0.5f;
            lightObj.transform.localPosition = new Vector3(startX + i * spacing, 2f, 0f);

            var progressLight = lightObj.AddComponent<Light2D>();
            progressLight.lightType = Light2D.LightType.Point;
            progressLight.intensity = 0.5f;
            progressLight.pointLightInnerRadius = 0.1f;
            progressLight.pointLightOuterRadius = 1f;
            progressLight.color = _closedColor;

            _progressLights.Add(progressLight);
        }
    }

    private void CreateNodeConnections()
    {
        if (_connectionLines == null)
        {
            GameObject connectionObj = new GameObject("NodeConnections");
            connectionObj.transform.SetParent(transform);
            _connectionLines = connectionObj.AddComponent<LineRenderer>();
        }

        _connectionLines.material = new Material(Shader.Find("Sprites/Default"));
        _connectionLines.startColor = new Color(0.5f, 0.5f, 1f, 0.3f);
        _connectionLines.startWidth = 0.05f;
        _connectionLines.endWidth = 0.05f;
        _connectionLines.useWorldSpace = true;

        // Create lines from gate to each node
        _connectionLines.positionCount = _requiredNodes.Count * 2;

        for (int i = 0; i < _requiredNodes.Count; i++)
        {
            if (_requiredNodes[i] != null)
            {
                _connectionLines.SetPosition(i * 2, transform.position);
                _connectionLines.SetPosition(i * 2 + 1, _requiredNodes[i].transform.position);
            }
        }
    }

    private void Update()
    {
        UpdateCooldown();
        UpdateOpenTimer();
        UpdateGateLogic();
        UpdateVisualState();
        UpdateProgressIndicators();
    }

    private void UpdateCooldown()
    {
        if (IsOnCooldown)
        {
            _cooldownRemaining -= Time.deltaTime;
            if (_cooldownRemaining <= 0f)
            {
                IsOnCooldown = false;
                _cooldownRemaining = 0f;
            }
        }
    }

    private void UpdateOpenTimer()
    {
        if (IsOpen && !IsPermanentlyOpen && OpenTimeRemaining > 0f)
        {
            OpenTimeRemaining -= Time.deltaTime;

            // Warning when about to close
            if (OpenTimeRemaining <= 2f && OpenTimeRemaining > 0f)
            {
                if (_closingWarningParticles != null && !_closingWarningParticles.isPlaying)
                {
                    _closingWarningParticles.Play();
                }
            }

            if (OpenTimeRemaining <= 0f)
            {
                CloseGate();
            }
        }
    }

    private void UpdateGateLogic()
    {
        // Count currently active nodes
        _currentlyActiveNodes.Clear();
        ActiveNodeCount = 0;

        foreach (var node in _requiredNodes)
        {
            if (node != null && node.IsActive)
            {
                _currentlyActiveNodes.Add(node);
                ActiveNodeCount++;
            }
        }

        // Check if gate should open based on type
        bool shouldOpen = false;

        switch (_gateType)
        {
            case GateType.Simultaneous:
            case GateType.Solar:
                shouldOpen = CheckSimultaneousActivation();
                break;

            case GateType.Sequential:
                shouldOpen = CheckSequentialActivation();
                break;

            case GateType.Timed:
                shouldOpen = CheckTimedActivation();
                break;

            case GateType.Memory:
                shouldOpen = CheckMemoryActivation();
                break;
        }

        // Open gate if conditions are met
        if (shouldOpen && !IsOpen && !IsOnCooldown)
        {
            OpenGate();
        }
    }

    private bool CheckSimultaneousActivation()
    {
        if (_requiresAllNodes)
        {
            return ActiveNodeCount == _requiredNodes.Count;
        }
        else
        {
            return ActiveNodeCount >= _minimumActiveNodes;
        }
    }

    private bool CheckSequentialActivation()
    {
        // For now, treat as simultaneous - could be expanded for sequence checking
        return CheckSimultaneousActivation();
    }

    private bool CheckTimedActivation()
    {
        // Check if nodes were activated within the time window
        float currentTime = Time.time;
        int recentActivations = 0;

        foreach (var kvp in _nodeActivationTimes)
        {
            if (currentTime - kvp.Value <= _simultaneousWindow)
            {
                recentActivations++;
            }
        }

        return recentActivations >= (_requiresAllNodes ? _requiredNodes.Count : _minimumActiveNodes);
    }

    private bool CheckMemoryActivation()
    {
        // Memory gate remembers which nodes have been activated
        return _nodeActivationTimes.Count >= (_requiresAllNodes ? _requiredNodes.Count : _minimumActiveNodes);
    }

    #region Gate State Management

    private void HandleNodeActivated(LightPuzzleNode node)
    {
        if (!_requiredNodes.Contains(node)) return;

        _nodeActivationTimes[node] = Time.time;

        // Play node activation sound
        if (_nodeActivationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_nodeActivationSound);
        }

        if (_debugMode)
        {
            Debug.Log($"🔵 Gate {_gateId}: Node {node.GetNodeId()} activated ({ActiveNodeCount + 1}/{_requiredNodes.Count})");
        }
    }

    private void HandleNodeDeactivated(LightPuzzleNode node)
    {
        if (!_requiredNodes.Contains(node)) return;

        // For most gate types, deactivation means the gate should close
        if (IsOpen && (_gateType == GateType.Simultaneous || _gateType == GateType.Sequential))
        {
            // Close immediately if required nodes are no longer active
            if (_requiresAllNodes && !IsPermanentlyOpen)
            {
                CloseGate();
            }
        }

        if (_debugMode)
        {
            Debug.Log($"🔴 Gate {_gateId}: Node {node.GetNodeId()} deactivated");
        }
    }

    private void OpenGate()
    {
        if (IsOpen) return;

        IsOpen = true;

        if (_staysOpenPermanently)
        {
            IsPermanentlyOpen = true;
            OpenTimeRemaining = float.MaxValue;
        }
        else
        {
            OpenTimeRemaining = _openDuration;
        }

        // Update physical state
        if (_gateCollider != null)
        {
            _gateCollider.enabled = false; // Allow passage
        }

        // Play opening animation
        if (_gateAnimator != null)
        {
            _gateAnimator.SetTrigger("Open");
        }

        // Play opening sound
        if (_gateOpenSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_gateOpenSound);
        }

        // Play success sound for puzzle completion
        if (_puzzleSolvedSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_puzzleSolvedSound);
        }

        // Play activation particles
        if (_activationParticles != null)
        {
            _activationParticles.Play();
        }

        OnGateOpened?.Invoke(this);

        if (_debugMode)
        {
            Debug.Log($"🟢 Gate {_gateId} OPENED! (Duration: {OpenTimeRemaining:F1}s)");
        }
    }

    private void CloseGate()
    {
        if (!IsOpen || IsPermanentlyOpen) return;

        IsOpen = false;
        OpenTimeRemaining = 0f;

        // Update physical state
        if (_gateCollider != null)
        {
            _gateCollider.enabled = true; // Block passage
        }

        // Play closing animation
        if (_gateAnimator != null)
        {
            _gateAnimator.SetTrigger("Close");
        }

        // Play closing sound
        if (_gateCloseSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_gateCloseSound);
        }

        // Stop warning particles
        if (_closingWarningParticles != null && _closingWarningParticles.isPlaying)
        {
            _closingWarningParticles.Stop();
        }

        // Start cooldown if applicable
        if (_canReopenAfterClose && _reopenCooldown > 0f)
        {
            IsOnCooldown = true;
            _cooldownRemaining = _reopenCooldown;
        }

        OnGateClosed?.Invoke(this);

        if (_debugMode)
        {
            Debug.Log($"🔴 Gate {_gateId} closed");
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateVisualState()
    {
        Color targetColor;

        if (IsOpen)
        {
            if (OpenTimeRemaining <= 2f && !IsPermanentlyOpen)
            {
                // Warning color when about to close
                targetColor = _warningColor;
            }
            else
            {
                targetColor = _openColor;
            }
        }
        else if (ActiveNodeCount > 0)
        {
            targetColor = Color.Lerp(_closedColor, _progressColor, (float)ActiveNodeCount / _requiredNodes.Count);
        }
        else
        {
            targetColor = _closedColor;
        }

        // Update gate light
        if (_gateLight != null)
        {
            _gateLight.color = targetColor;
            _gateLight.intensity = IsOpen ? 2f : 1f;
        }

        // Update gate renderer
        if (_gateRenderer != null)
        {
            _gateRenderer.color = targetColor;
        }
    }

    private void UpdateProgressIndicators()
    {
        for (int i = 0; i < _progressLights.Count && i < _requiredNodes.Count; i++)
        {
            var progressLight = _progressLights[i];
            var node = _requiredNodes[i];

            if (node != null && node.IsActive)
            {
                progressLight.color = _openColor;
                progressLight.intensity = 1f;
            }
            else if (node != null && node.IsCharging)
            {
                progressLight.color = _progressColor;
                progressLight.intensity = 0.5f + Mathf.Sin(Time.time * 10f) * 0.25f; // Pulse
            }
            else
            {
                progressLight.color = _closedColor;
                progressLight.intensity = 0.3f;
            }
        }

        // Fire progress event
        float progress = (float)ActiveNodeCount / _requiredNodes.Count;
        OnGateProgress?.Invoke(this, progress);
    }

    #endregion

    #region Public API

    public string GetGateId() => _gateId;
    public GateType GetGateType() => _gateType;
    public List<LightPuzzleNode> GetRequiredNodes() => new List<LightPuzzleNode>(_requiredNodes);
    public float GetProgress() => (float)ActiveNodeCount / _requiredNodes.Count;

    public void ForceOpen()
    {
        OpenGate();
    }

    public void ForceClose()
    {
        CloseGate();
    }

    public void ResetGate()
    {
        CloseGate();
        IsOnCooldown = false;
        IsPermanentlyOpen = false;
        _cooldownRemaining = 0f;
        _nodeActivationTimes.Clear();
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Gate Opening")]
    public void TestGateOpening()
    {
        if (Application.isPlaying)
        {
            // Force activate all required nodes
            foreach (var node in _requiredNodes)
            {
                if (node != null)
                {
                    node.ForceActivate();
                }
            }
        }
    }

    [ContextMenu("Show Gate Info")]
    public void ShowGateInfo()
    {
        Debug.Log($"=== GATE INFO: {_gateId} ===");
        Debug.Log($"Type: {_gateType}");
        Debug.Log($"Is Open: {IsOpen}");
        Debug.Log($"Is Permanently Open: {IsPermanentlyOpen}");
        Debug.Log($"Is On Cooldown: {IsOnCooldown}");
        Debug.Log($"Active Nodes: {ActiveNodeCount}/{_requiredNodes.Count}");
        Debug.Log($"Open Time Remaining: {OpenTimeRemaining:F1}s");
        Debug.Log($"Required Nodes:");

        for (int i = 0; i < _requiredNodes.Count; i++)
        {
            var node = _requiredNodes[i];
            if (node != null)
            {
                Debug.Log($"  {i}: {node.GetNodeId()} - Active: {node.IsActive}");
            }
            else
            {
                Debug.Log($"  {i}: NULL NODE");
            }
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Draw gate area
        Gizmos.color = IsOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);

        // Draw node detection radius
        if (_autoDetectNodes)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _nodeDetectionRadius);
        }

        // Draw connections to required nodes
        Gizmos.color = Color.yellow;
        foreach (var node in _requiredNodes)
        {
            if (node != null)
            {
                Gizmos.DrawLine(transform.position, node.transform.position);

                // Color-code nodes based on status
                if (Application.isPlaying)
                {
                    Gizmos.color = node.IsActive ? Color.green : (node.IsCharging ? Color.yellow : Color.red);
                    Gizmos.DrawWireSphere(node.transform.position, 0.5f);
                    Gizmos.color = Color.yellow; // Reset for line drawing
                }
            }
        }
    }
}