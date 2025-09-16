using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Puzzle-first Solar Flare ability implementation
/// Primary Function: Simultaneous multi-node activation for puzzle solving
/// Secondary Function: Enemy stunning/blinding for tactical advantage
/// </summary>
public class SolarFlareAbility : MonoBehaviour
{
    [Header("Solar Flare Configuration")]
    [SerializeField] private float _blastRadius = 8f;
    [SerializeField] private float _chargeTime = 1f;
    [SerializeField] private float _effectDuration = 3f;
    [SerializeField] private float _manaCost = 25f;
    [SerializeField] private float _cooldownTime = 8f;

    [Header("Puzzle Mechanics")]
    [SerializeField] private LayerMask _puzzleNodeLayers = 1 << 6; // Layer for puzzle nodes
    [SerializeField] private bool _activatesAllNodesInRange = true;
    [SerializeField] private bool _bypassesChargeTime = true; // Instant activation for burst nodes

    [Header("Enemy Mechanics")]
    [SerializeField] private LayerMask _enemyLayers = 1 << 7; // Layer for enemies
    [SerializeField] private float _stunDuration = 3f;
    [SerializeField] private float _blindRadius = 10f; // Larger than blast for enemy effects

    [Header("Visual Effects")]
    [SerializeField] private Light2D _chargeLight;
    [SerializeField] private ParticleSystem _chargeParticles;
    [SerializeField] private ParticleSystem _blastParticles;
    [SerializeField] private Material _expansionRingMaterial;

    [Header("Audio")]
    [SerializeField] private AudioClip _chargeSound;
    [SerializeField] private AudioClip _blastSound;
    [SerializeField] private AudioClip _puzzleSuccessSound;

    [Header("Debug & Feedback")]
    [SerializeField] private bool _showPreviewRadius = true;
    [SerializeField] private bool _debugMode = true;

    // State tracking
    public bool IsCharging { get; private set; }
    public bool IsOnCooldown { get; private set; }
    public float ChargingProgress { get; private set; }
    public float CooldownRemaining { get; private set; }

    // Components
    private EnhancedLightEffectsController _lightController;
    private DualProgressionSystem _progressionSystem;
    private AudioSource _audioSource;

    // Effect tracking
    private List<LightPuzzleNode> _affectedNodes;
    private List<ILightInteractable> _affectedEnemies;
    private Coroutine _abilityCoroutine;

    // Visual components
    private GameObject _expansionRing;
    private LineRenderer _previewCircle;

    // Events
    public System.Action OnSolarFlareCharged;
    public System.Action<int> OnNodesActivated; // Pass number of nodes activated
    public System.Action<int> OnEnemiesStunned; // Pass number of enemies stunned

    private void Awake()
    {
        _affectedNodes = new List<LightPuzzleNode>();
        _affectedEnemies = new List<ILightInteractable>();

        _lightController = GetComponent<EnhancedLightEffectsController>();
        _progressionSystem = GetComponent<DualProgressionSystem>();
        _audioSource = GetComponent<AudioSource>();

        SetupVisualComponents();
    }

    private void SetupVisualComponents()
    {
        // Setup charge light
        if (_chargeLight == null)
        {
            GameObject lightObj = new GameObject("SolarFlareChargeLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _chargeLight = lightObj.AddComponent<Light2D>();
        }

        _chargeLight.lightType = Light2D.LightType.Point;
        _chargeLight.color = new Color(1f, 0.8f, 0.2f);
        _chargeLight.intensity = 0f;
        _chargeLight.pointLightInnerRadius = 0.5f;
        _chargeLight.pointLightOuterRadius = 3f;

        // Setup preview circle for aiming
        if (_previewCircle == null)
        {
            GameObject previewObj = new GameObject("SolarFlarePreview");
            previewObj.transform.SetParent(transform);
            _previewCircle = previewObj.AddComponent<LineRenderer>();
        }

        _previewCircle.material = new Material(Shader.Find("Sprites/Default"));
        _previewCircle.startColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        _previewCircle.startWidth = 0.1f;
        _previewCircle.endWidth = 0.1f;
        _previewCircle.useWorldSpace = false;
        _previewCircle.enabled = false;

        CreatePreviewCircle();

        // Setup expansion ring for blast effect
        _expansionRing = new GameObject("SolarFlareRing");
        _expansionRing.transform.SetParent(transform);
        var ringRenderer = _expansionRing.AddComponent<LineRenderer>();
        ringRenderer.material = _expansionRingMaterial ?? new Material(Shader.Find("Sprites/Default"));
        ringRenderer.startColor = new Color(1f, 0.8f, 0.2f, 0.8f);
        ringRenderer.startWidth = 0.2f;
        ringRenderer.endWidth = 0.2f;
        ringRenderer.useWorldSpace = true;
        ringRenderer.enabled = false;

        CreateExpansionRing(ringRenderer);
    }

    private void CreatePreviewCircle()
    {
        int segments = 64;
        _previewCircle.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * _blastRadius,
                Mathf.Sin(angle) * _blastRadius,
                0f
            );
            _previewCircle.SetPosition(i, pos);
        }
    }

    private void CreateExpansionRing(LineRenderer renderer)
    {
        int segments = 32;
        renderer.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            renderer.SetPosition(i, Vector3.zero); // Will be updated during blast
        }
    }

    private void Update()
    {
        UpdateCooldown();
        UpdatePreview();

        // Handle input for ability activation
        if (Input.GetKeyDown(KeyCode.Q) && CanActivate())
        {
            StartSolarFlare();
        }

        // Handle charging cancellation
        if (Input.GetKeyUp(KeyCode.Q) && IsCharging)
        {
            CancelSolarFlare();
        }
    }

    private void UpdateCooldown()
    {
        if (IsOnCooldown)
        {
            CooldownRemaining -= Time.deltaTime;
            if (CooldownRemaining <= 0f)
            {
                IsOnCooldown = false;
                CooldownRemaining = 0f;

                if (_debugMode)
                    Debug.Log("☀️ Solar Flare cooldown finished");
            }
        }
    }

    private void UpdatePreview()
    {
        // Show preview when player is holding Q but not yet charging
        bool shouldShowPreview = _showPreviewRadius &&
                                Input.GetKey(KeyCode.Q) &&
                                !IsCharging &&
                                CanActivate();

        _previewCircle.enabled = shouldShowPreview;

        if (shouldShowPreview)
        {
            // Count nodes and enemies in range for feedback
            int nodeCount = CountNodesInRange();
            int enemyCount = CountEnemiesInRange();

            // Color-code preview based on targets
            Color previewColor = nodeCount > 0 ? Color.green : (enemyCount > 0 ? Color.yellow : Color.red);
            previewColor.a = 0.3f;
            _previewCircle.startColor = previewColor;
        }
    }

    #region Ability Execution

    public bool CanActivate()
    {
        if (IsCharging || IsOnCooldown) return false;
        if (_progressionSystem != null && _progressionSystem.GetLightEssence() < _manaCost) return false;

        return true;
    }

    public void StartSolarFlare()
    {
        if (!CanActivate())
        {
            if (_debugMode)
                Debug.Log("❌ Cannot activate Solar Flare");
            return;
        }

        _abilityCoroutine = StartCoroutine(SolarFlareSequence());
    }

    public void CancelSolarFlare()
    {
        if (_abilityCoroutine != null)
        {
            StopCoroutine(_abilityCoroutine);
            _abilityCoroutine = null;
        }

        IsCharging = false;
        ChargingProgress = 0f;

        // Reset visual effects
        _chargeLight.intensity = 0f;
        if (_chargeParticles != null && _chargeParticles.isPlaying)
        {
            _chargeParticles.Stop();
        }

        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        if (_debugMode)
            Debug.Log("☀️ Solar Flare cancelled");
    }

    private IEnumerator SolarFlareSequence()
    {
        // PHASE 1: Charging
        yield return StartCoroutine(ChargingPhase());

        // PHASE 2: Blast Effect
        yield return StartCoroutine(BlastPhase());

        // PHASE 3: Effect Resolution
        yield return StartCoroutine(EffectPhase());

        // PHASE 4: Cooldown
        StartCooldown();
    }

    private IEnumerator ChargingPhase()
    {
        if (_debugMode)
            Debug.Log("☀️ Solar Flare charging started");

        IsCharging = true;
        ChargingProgress = 0f;

        // Start charge effects
        if (_chargeSound != null && _audioSource != null)
        {
            _audioSource.clip = _chargeSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        if (_chargeParticles != null)
        {
            _chargeParticles.Play();
        }

        // Charging animation
        float elapsed = 0f;
        while (elapsed < _chargeTime)
        {
            elapsed += Time.deltaTime;
            ChargingProgress = elapsed / _chargeTime;

            // Animate charge light
            _chargeLight.intensity = ChargingProgress * 2f;
            _chargeLight.pointLightOuterRadius = 3f + ChargingProgress * 5f;

            yield return null;
        }

        ChargingProgress = 1f;
        IsCharging = false;

        // Stop charge effects
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }

        OnSolarFlareCharged?.Invoke();

        if (_debugMode)
            Debug.Log("☀️ Solar Flare charged!");
    }

    private IEnumerator BlastPhase()
    {
        if (_debugMode)
            Debug.Log("☀️ Solar Flare BLAST!");

        // Consume mana
        if (_progressionSystem != null)
        {
            _progressionSystem.AddLightEssence(-Mathf.RoundToInt(_manaCost));
        }

        // Play blast sound
        if (_blastSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_blastSound);
        }

        // Visual blast effect
        yield return StartCoroutine(VisualBlastEffect());

        // Detect and affect targets
        DetectAndAffectTargets();
    }

    private IEnumerator VisualBlastEffect()
    {
        // Bright flash
        _chargeLight.intensity = 5f;
        _chargeLight.pointLightOuterRadius = _blastRadius * 2f;

        // Expansion ring animation
        var ringRenderer = _expansionRing.GetComponent<LineRenderer>();
        ringRenderer.enabled = true;

        float animationTime = 0.5f;
        float elapsed = 0f;

        while (elapsed < animationTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationTime;

            // Animate ring expansion
            float currentRadius = progress * _blastRadius;
            UpdateRingRadius(ringRenderer, currentRadius);

            // Fade ring
            Color ringColor = ringRenderer.startColor;
            ringColor.a = 1f - progress;
            ringRenderer.startColor = ringColor;

            // Fade charge light
            _chargeLight.intensity = Mathf.Lerp(5f, 0f, progress);

            yield return null;
        }

        ringRenderer.enabled = false;
        _chargeLight.intensity = 0f;

        // Play blast particles
        if (_blastParticles != null)
        {
            _blastParticles.Play();
        }
    }

    private void UpdateRingRadius(LineRenderer renderer, float radius)
    {
        Vector3 center = transform.position;

        for (int i = 0; i <= renderer.positionCount - 1; i++)
        {
            float angle = i * 2f * Mathf.PI / (renderer.positionCount - 1);
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
            renderer.SetPosition(i, pos);
        }
    }

    private void DetectAndAffectTargets()
    {
        Vector3 center = transform.position;
        _affectedNodes.Clear();
        _affectedEnemies.Clear();

        // PUZZLE MECHANIC: Activate all nodes in blast radius
        DetectPuzzleNodes(center);

        // ENEMY MECHANIC: Stun/blind enemies in larger radius
        DetectEnemies(center);

        // Trigger effects
        if (_lightController != null)
        {
            _lightController.TriggerBurstEffect(LightEffect.Stun, center, _blastRadius);
        }

        // Fire events
        OnNodesActivated?.Invoke(_affectedNodes.Count);
        OnEnemiesStunned?.Invoke(_affectedEnemies.Count);

        if (_debugMode)
        {
            Debug.Log($"☀️ Solar Flare affected {_affectedNodes.Count} nodes and {_affectedEnemies.Count} enemies");
        }
    }

    private void DetectPuzzleNodes(Vector3 center)
    {
        Collider2D[] nodeColliders = Physics2D.OverlapCircleAll(center, _blastRadius, _puzzleNodeLayers);

        foreach (var collider in nodeColliders)
        {
            var node = collider.GetComponent<LightPuzzleNode>();
            if (node != null)
            {
                _affectedNodes.Add(node);

                // Burst nodes activate instantly, others get energized
                if (node.GetNodeType() == LightPuzzleNode.NodeType.Burst)
                {
                    node.ForceActivate();
                }
                else if (node.RespondsToEffect(LightEffect.Energize))
                {
                    // Give them intense light to charge quickly
                    node.OnLightEnter(LightEffect.Energize, 2f, Vector2.zero);
                }

                if (_debugMode)
                    Debug.Log($"☀️ Solar Flare activated node: {node.GetNodeId()}");
            }
        }

        // Play success sound if nodes were activated
        if (_affectedNodes.Count > 0 && _puzzleSuccessSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_puzzleSuccessSound);
        }
    }

    private void DetectEnemies(Vector3 center)
    {
        Collider2D[] enemyColliders = Physics2D.OverlapCircleAll(center, _blindRadius, _enemyLayers);

        foreach (var collider in enemyColliders)
        {
            var enemy = collider.GetComponent<ILightInteractable>();
            if (enemy != null && enemy.RespondsToEffect(LightEffect.Stun))
            {
                _affectedEnemies.Add(enemy);

                // Apply stun effect
                float distance = Vector3.Distance(center, collider.transform.position);
                float intensity = Mathf.Clamp01(1f - (distance / _blindRadius));
                Vector2 direction = (collider.transform.position - center).normalized;

                enemy.OnLightEnter(LightEffect.Stun, intensity, direction);

                if (_debugMode)
                    Debug.Log($"☀️ Solar Flare stunned enemy: {collider.name}");
            }
        }
    }

    private IEnumerator EffectPhase()
    {
        // Wait for effect duration
        yield return new WaitForSeconds(_effectDuration);

        // Clear effects from affected enemies
        foreach (var enemy in _affectedEnemies)
        {
            if (enemy != null)
            {
                enemy.OnLightExit(LightEffect.Stun);
            }
        }

        if (_debugMode)
            Debug.Log("☀️ Solar Flare effects ended");
    }

    private void StartCooldown()
    {
        IsOnCooldown = true;
        CooldownRemaining = _cooldownTime;

        if (_debugMode)
            Debug.Log($"☀️ Solar Flare on cooldown for {_cooldownTime}s");
    }

    #endregion

    #region Utility Methods

    private int CountNodesInRange()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _blastRadius, _puzzleNodeLayers);
        int count = 0;

        foreach (var collider in colliders)
        {
            if (collider.GetComponent<LightPuzzleNode>() != null)
                count++;
        }

        return count;
    }

    private int CountEnemiesInRange()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, _blindRadius, _enemyLayers);
        int count = 0;

        foreach (var collider in colliders)
        {
            if (collider.GetComponent<ILightInteractable>() != null)
                count++;
        }

        return count;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Solar Flare")]
    public void TestSolarFlare()
    {
        if (Application.isPlaying && CanActivate())
        {
            StartSolarFlare();
        }
    }

    [ContextMenu("Show Targets in Range")]
    public void ShowTargetsInRange()
    {
        int nodes = CountNodesInRange();
        int enemies = CountEnemiesInRange();

        Debug.Log($"☀️ Solar Flare Range Analysis:");
        Debug.Log($"  Blast Radius: {_blastRadius}");
        Debug.Log($"  Blind Radius: {_blindRadius}");
        Debug.Log($"  Nodes in Range: {nodes}");
        Debug.Log($"  Enemies in Range: {enemies}");
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Draw blast radius
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawSphere(transform.position, _blastRadius);

        // Draw blast radius outline
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, _blastRadius);

        // Draw blind radius for enemies
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _blindRadius);

        // Show charging progress during play
        if (Application.isPlaying && IsCharging)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _blastRadius * ChargingProgress);
        }
    }
}