using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Core puzzle node that responds to specific light effects
/// Used for gates, mechanisms, and multi-node activation puzzles
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightPuzzleNode : MonoBehaviour, ILightInteractable
{
    [Header("Node Configuration")]
    [SerializeField] private string _nodeId = "";
    [SerializeField] private NodeType _nodeType = NodeType.Standard;
    [SerializeField] private NodeGroup _nodeGroup = NodeGroup.None;

    [Header("Light Requirements")]
    [SerializeField] private LightEffect[] _acceptedEffects = { LightEffect.Energize };
    [SerializeField] private float _minimumIntensity = 0.5f;
    [SerializeField] private float _activationTime = 1f;
    [SerializeField] private bool _requiresContinuousLight = true;

    [Header("Timing")]
    [SerializeField] private float _activeStateDuration = 5f; // How long node stays active
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private bool _canReactivate = true;

    [Header("Visual Feedback")]
    [SerializeField] private Light2D _nodeLight;
    [SerializeField] private SpriteRenderer _nodeRenderer;
    [SerializeField] private ParticleSystem _activationParticles;
    [SerializeField] private ParticleSystem _chargingParticles;

    [Header("Audio")]
    [SerializeField] private AudioClip _chargingSound;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    public enum NodeType
    {
        Standard,       // Basic puzzle node
        Timed,          // Must be activated within time window
        Sequential,     // Must be activated in specific order
        Simultaneous,   // Must be activated with other nodes at same time
        Burst,          // Only responds to burst effects (Solar Flare)
        Continuous,     // Requires constant light to stay active
        Memory          // Remembers activation state
    }

    public enum NodeGroup
    {
        None,
        Alpha,
        Beta,
        Gamma,
        Delta,
        Epsilon
    }

    // State tracking
    public bool IsCurrentlyIlluminated { get; private set; }
    public LightEffect CurrentActiveEffect { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsCharging { get; private set; }
    public bool IsOnCooldown { get; private set; }

    EnhancedLanternController.LightEffect ILightInteractable.CurrentActiveEffect => _lightInteractable.CurrentActiveEffect;

    // Internal state
    private float _chargingProgress = 0f;
    private float _activeTimeRemaining = 0f;
    private float _cooldownTimeRemaining = 0f;
    private AudioSource _audioSource;
    private Coroutine _stateCoroutine;

    // Events for puzzle system
    public static System.Action<LightPuzzleNode> OnNodeActivated;
    public static System.Action<LightPuzzleNode> OnNodeDeactivated;
    public static System.Action<LightPuzzleNode> OnNodeChargeStarted;

    // Color scheme
    private Color _inactiveColor = new Color(0.3f, 0.3f, 0.3f);
    private Color _chargingColor = new Color(1f, 0.8f, 0.3f);
    private Color _activeColor = new Color(0.3f, 1f, 0.3f);
    private Color _cooldownColor = new Color(1f, 0.3f, 0.3f);

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // Auto-generate ID if empty
        if (string.IsNullOrEmpty(_nodeId))
        {
            _nodeId = $"Node_{GetInstanceID()}";
        }

        SetupComponents();
        SetVisualState(_inactiveColor);

        // Make sure collider is trigger for light detection
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void SetupComponents()
    {
        // Setup node light
        if (_nodeLight == null)
        {
            _nodeLight = GetComponent<Light2D>();
            if (_nodeLight == null)
            {
                GameObject lightObj = new GameObject("NodeLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.zero;
                _nodeLight = lightObj.AddComponent<Light2D>();
            }
        }

        _nodeLight.lightType = Light2D.LightType.Point;
        _nodeLight.intensity = 0.5f;
        _nodeLight.pointLightInnerRadius = 0.1f;
        _nodeLight.pointLightOuterRadius = 2f;
        _nodeLight.enabled = false;

        // Setup renderer
        if (_nodeRenderer == null)
        {
            _nodeRenderer = GetComponent<SpriteRenderer>();
            if (_nodeRenderer == null)
            {
                _nodeRenderer = gameObject.AddComponent<SpriteRenderer>();
                _nodeRenderer.sprite = CreateNodeSprite();
            }
        }

        // Setup charging particles
        if (_chargingParticles == null)
        {
            GameObject chargingObj = new GameObject("ChargingParticles");
            chargingObj.transform.SetParent(transform);
            chargingObj.transform.localPosition = Vector3.zero;
            _chargingParticles = chargingObj.AddComponent<ParticleSystem>();

            var main = _chargingParticles.main;
            main.startLifetime = 1f;
            main.startSpeed = 2f;
            main.startSize = 0.1f;
            main.startColor = _chargingColor;

            var emission = _chargingParticles.emission;
            emission.rateOverTime = 20f;
        }
    }

    private Sprite CreateNodeSprite()
    {
        // Create a simple hexagon sprite for the node
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.4f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance <= radius)
                {
                    // Create hexagon shape
                    bool isInsideHex = IsInsideHexagon(pos - center, radius * 0.8f);
                    if (isInsideHex)
                    {
                        float alpha = 1f - (distance / radius) * 0.2f;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private bool IsInsideHexagon(Vector2 point, float radius)
    {
        // Simple hexagon check - could be more precise
        float distance = point.magnitude;
        return distance <= radius;
    }

    private void Update()
    {
        UpdateTimers();
        UpdateVisualFeedback();
    }

    private void UpdateTimers()
    {
        // Update active time
        if (IsActive && _activeTimeRemaining > 0)
        {
            _activeTimeRemaining -= Time.deltaTime;
            if (_activeTimeRemaining <= 0)
            {
                DeactivateNode();
            }
        }

        // Update cooldown
        if (IsOnCooldown && _cooldownTimeRemaining > 0)
        {
            _cooldownTimeRemaining -= Time.deltaTime;
            if (_cooldownTimeRemaining <= 0)
            {
                IsOnCooldown = false;
                if (_debugMode)
                    Debug.Log($"Node {_nodeId} cooldown finished");
            }
        }
    }

    private void UpdateVisualFeedback()
    {
        // Update visual state based on current status
        if (IsActive)
        {
            SetVisualState(_activeColor);
        }
        else if (IsCharging)
        {
            // Animate charging with pulsing effect
            float pulse = Mathf.Sin(Time.time * 5f) * 0.3f + 0.7f;
            Color chargingWithPulse = _chargingColor * pulse;
            SetVisualState(chargingWithPulse);
        }
        else if (IsOnCooldown)
        {
            SetVisualState(_cooldownColor);
        }
        else
        {
            SetVisualState(_inactiveColor);
        }

        // Update charging particles
        if (_chargingParticles != null)
        {
            if (IsCharging && !_chargingParticles.isPlaying)
            {
                _chargingParticles.Play();
            }
            else if (!IsCharging && _chargingParticles.isPlaying)
            {
                _chargingParticles.Stop();
            }
        }
    }

    private void SetVisualState(Color color)
    {
        if (_nodeRenderer != null)
            _nodeRenderer.color = color;

        if (_nodeLight != null)
        {
            _nodeLight.color = color;
            _nodeLight.enabled = IsActive || IsCharging;
            _nodeLight.intensity = IsActive ? 1f : 0.5f;
        }
    }

    #region ILightInteractable Implementation

    public void OnLightEnter(LightEffect effect, float intensity, Vector2 direction)
    {
        if (!RespondsToEffect(effect) || intensity < GetMinimumIntensity(effect))
        {
            if (_debugMode)
                Debug.Log($"Node {_nodeId} ignoring effect {effect} (intensity: {intensity:F2})");
            return;
        }

        if (IsOnCooldown || IsActive)
        {
            if (_debugMode)
                Debug.Log($"Node {_nodeId} cannot activate (Cooldown: {IsOnCooldown}, Active: {IsActive})");
            return;
        }

        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;

        // Handle different node types
        switch (_nodeType)
        {
            case NodeType.Burst:
                // Burst nodes activate immediately from burst effects
                if (effect == LightEffect.Stun) // Solar Flare
                {
                    ActivateNodeInstantly();
                }
                break;

            case NodeType.Standard:
            case NodeType.Timed:
            case NodeType.Sequential:
            case NodeType.Simultaneous:
                // These nodes require charging time
                StartCharging();
                break;

            case NodeType.Continuous:
                // Continuous nodes activate while lit, deactivate when light leaves
                ActivateNodeInstantly();
                break;
        }

        if (_debugMode)
            Debug.Log($"Node {_nodeId} illuminated with {effect} (intensity: {intensity:F2})");
    }

    public void OnLightStay(LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        if (!RespondsToEffect(effect) || !IsCurrentlyIlluminated) return;

        // Update charging progress for nodes that require buildup
        if (IsCharging && _nodeType != NodeType.Burst)
        {
            _chargingProgress += deltaTime;

            if (_chargingProgress >= _activationTime)
            {
                CompleteCharging();
            }
        }
    }

    public void OnLightExit(LightEffect effect)
    {
        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = LightEffect.Reveal;

        // Handle different node behaviors when light leaves
        switch (_nodeType)
        {
            case NodeType.Continuous:
                // Continuous nodes deactivate immediately
                if (IsActive)
                {
                    DeactivateNode();
                }
                break;

            case NodeType.Standard:
            case NodeType.Timed:
            case NodeType.Sequential:
            case NodeType.Simultaneous:
                // These nodes stop charging but may have grace period
                if (IsCharging && !_requiresContinuousLight)
                {
                    // Continue charging briefly even after light leaves
                    StartCoroutine(ChargeGracePeriod(0.5f));
                }
                else if (IsCharging)
                {
                    StopCharging();
                }
                break;
        }

        if (_debugMode)
            Debug.Log($"Node {_nodeId} light exit");
    }

    public bool RespondsToEffect(LightEffect effect)
    {
        foreach (var acceptedEffect in _acceptedEffects)
        {
            if (acceptedEffect == effect)
                return true;
        }
        return false;
    }

    public float GetMinimumIntensity(LightEffect effect)
    {
        return _minimumIntensity;
    }

    #endregion

    #region Node State Management

    private void StartCharging()
    {
        if (IsCharging || IsActive || IsOnCooldown) return;

        IsCharging = true;
        _chargingProgress = 0f;

        // Play charging sound
        if (_chargingSound != null && _audioSource != null)
        {
            _audioSource.clip = _chargingSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        OnNodeChargeStarted?.Invoke(this);

        if (_debugMode)
            Debug.Log($"Node {_nodeId} started charging");
    }

    private void StopCharging()
    {
        if (!IsCharging) return;

        IsCharging = false;
        _chargingProgress = 0f;

        // Stop charging sound
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        if (_debugMode)
            Debug.Log($"Node {_nodeId} stopped charging");
    }

    private void CompleteCharging()
    {
        if (!IsCharging) return;

        StopCharging();
        ActivateNode();
    }

    private void ActivateNodeInstantly()
    {
        StopCharging();
        ActivateNode();
    }

    private void ActivateNode()
    {
        if (IsActive) return;

        IsActive = true;
        _activeTimeRemaining = _activeStateDuration;

        // Play activation sound
        if (_activationSound != null && _audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.loop = false;
            _audioSource.PlayOneShot(_activationSound);
        }

        // Play activation particles
        if (_activationParticles != null)
        {
            _activationParticles.Play();
        }

        OnNodeActivated?.Invoke(this);

        if (_debugMode)
            Debug.Log($"🟢 Node {_nodeId} ACTIVATED!");
    }

    private void DeactivateNode()
    {
        if (!IsActive) return;

        IsActive = false;
        _activeTimeRemaining = 0f;

        // Start cooldown if applicable
        if (_canReactivate && _cooldownTime > 0)
        {
            IsOnCooldown = true;
            _cooldownTimeRemaining = _cooldownTime;
        }

        // Play deactivation sound
        if (_deactivationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_deactivationSound);
        }

        OnNodeDeactivated?.Invoke(this);

        if (_debugMode)
            Debug.Log($"🔴 Node {_nodeId} deactivated");
    }

    private IEnumerator ChargeGracePeriod(float gracePeriod)
    {
        yield return new WaitForSeconds(gracePeriod);

        if (IsCharging && !IsCurrentlyIlluminated)
        {
            StopCharging();
        }
    }

    #endregion

    #region Public API

    public string GetNodeId() => _nodeId;
    public NodeType GetNodeType() => _nodeType;
    public NodeGroup GetNodeGroup() => _nodeGroup;
    public float GetChargingProgress() => _chargingProgress / _activationTime;
    public float GetActiveTimeRemaining() => _activeTimeRemaining;
    public float GetCooldownTimeRemaining() => _cooldownTimeRemaining;

    /// <summary>
    /// Force activate the node (for testing or special circumstances)
    /// </summary>
    public void ForceActivate()
    {
        ActivateNodeInstantly();
    }

    /// <summary>
    /// Force deactivate the node
    /// </summary>
    public void ForceDeactivate()
    {
        StopCharging();
        DeactivateNode();
    }

    /// <summary>
    /// Reset the node to inactive state
    /// </summary>
    public void ResetNode()
    {
        StopCharging();
        IsActive = false;
        IsOnCooldown = false;
        _activeTimeRemaining = 0f;
        _cooldownTimeRemaining = 0f;
        _chargingProgress = 0f;

        if (_debugMode)
            Debug.Log($"Node {_nodeId} reset");
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Activation")]
    public void TestActivation()
    {
        if (Application.isPlaying)
        {
            OnLightEnter(LightEffect.Energize, 1f, Vector2.zero);
        }
    }

    [ContextMenu("Force Activate")]
    public void DebugForceActivate()
    {
        if (Application.isPlaying)
        {
            ForceActivate();
        }
    }

    [ContextMenu("Show Node Info")]
    public void ShowNodeInfo()
    {
        Debug.Log($"=== NODE INFO: {_nodeId} ===");
        Debug.Log($"Type: {_nodeType}");
        Debug.Log($"Group: {_nodeGroup}");
        Debug.Log($"Is Active: {IsActive}");
        Debug.Log($"Is Charging: {IsCharging}");
        Debug.Log($"Is On Cooldown: {IsOnCooldown}");
        Debug.Log($"Charging Progress: {GetChargingProgress():P}");
        Debug.Log($"Active Time Remaining: {_activeTimeRemaining:F1}s");
        Debug.Log($"Cooldown Time Remaining: {_cooldownTimeRemaining:F1}s");
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Color-code gizmos by node type
        Color gizmoColor = _nodeType switch
        {
            NodeType.Standard => Color.white,
            NodeType.Timed => Color.yellow,
            NodeType.Sequential => Color.cyan,
            NodeType.Simultaneous => Color.magenta,
            NodeType.Burst => Color.red,
            NodeType.Continuous => Color.green,
            NodeType.Memory => Color.blue,
            _ => Color.gray
        };

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Show activation range
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
        Gizmos.DrawSphere(transform.position, 1f);

        // Show group connections
        if (_nodeGroup != NodeGroup.None)
        {
            var otherNodes = FindObjectsOfType<LightPuzzleNode>();
            foreach (var node in otherNodes)
            {
                if (node != this && node.GetNodeGroup() == _nodeGroup)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, node.transform.position);
                }
            }
        }
    }

    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        _lightInteractable.OnLightEnter(effect, intensity, direction);
    }

    public void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        _lightInteractable.OnLightStay(effect, intensity, direction, deltaTime);
    }

    public void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        _lightInteractable.OnLightExit(effect);
    }

    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        return _lightInteractable.RespondsToEffect(effect);
    }

    public float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _lightInteractable.GetMinimumIntensity(effect);
    }
}