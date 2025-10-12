using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// MIGRATION VERSION: Enhanced LanternController that preserves your existing interface
/// while adding the new advanced light effects system
/// 
/// COMPATIBILITY: Maintains all existing public methods and properties
/// NEW FEATURES: 9-effect light system, reflection mechanics, performance optimization
/// </summary>
public class EnhancedLanternController : MonoBehaviour
{
    [Header("Compatibility Settings")]
    [SerializeField] private bool _enableAdvancedFeatures = true;
    [SerializeField] private bool _maintainLegacyInterface = true;
    [SerializeField] private bool _debugMode = true;

    [Header("Basic Lantern Properties")]
    [SerializeField] private float _baseMana = 100f;
    [SerializeField] private float _manaRegenRate = 5f;
    [SerializeField] private float _baseRange = 10f;
    [SerializeField] private float _beamWidth = 30f;
    [SerializeField] private LayerMask _interactionLayers = -1;

    [Header("Light Effects System")]
    [SerializeField] private LightEffect _currentLightEffect = LightEffect.Reveal;
    [SerializeField] private LightEffectData[] _availableEffects;
    [SerializeField] private float _effectIntensity = 1f;
    [SerializeField] private bool _enableReflection = true;
    [SerializeField] private int _maxReflectionBounces = 3;

    [Header("Visual Components")]
    [SerializeField] private Light2D _playerInnerLight2D;
    [SerializeField] private LineRenderer _beamRenderer;
    [SerializeField] private ParticleSystem _lightParticles;

    [Header("Legacy Compatibility")]
    [SerializeField] private LightType _currentLightType = LightType.Ember;
    [SerializeField] private List<LightType> _discoveredLightTypes = new List<LightType>();

    // PRESERVED LEGACY INTERFACE
    public enum LightType
    {
        Ember,
        Radiance,
        SolarFlare,
        MoonBeam,
        Starlight,
        PrismaticLight,
        VoidLight
    }

    // NEW ADVANCED INTERFACE
    public enum LightEffect
    {
        Reveal,     // Basic illumination
        Energize,   // Power nodes/mechanisms
        Refract,    // Bounce off prisms
        Purify,     // Clear corruption/shadows
        Slow,       // Temporal effects
        Stun,       // Disable enemies
        Shield,     // Protective barriers
        Decoy,      // Phantom projections
        Stealth     // Concealment/phase
    }

    [System.Serializable]
    public class LightEffectData
    {
        public LightEffect effectType;
        public string displayName;
        public string description;
        public Color effectColor = Color.white;
        public float baseIntensity = 1f;
        public float baseRange = 10f;
        public float baseWidth = 30f;
        public bool requiresContinuousLight = true;
        public bool canPierceObjects = false;
        public bool canBounceOffSurfaces = false;
        public float manaCostPerSecond = 2f;
        public float activationCost = 0f;
        public AudioClip activationSound;
        public ParticleSystem effectParticles;
    }

    // PRESERVED LEGACY PROPERTIES
    public bool HasLantern { get; private set; }
    public bool IsLanternActive { get; private set; }
    public LightType CurrentLightType => _currentLightType;
    public float ManaPercentage => _currentMana / _baseMana;
    public float HorizontalVelocity { get; private set; } // For animation compatibility
    public float VerticalVelocity { get; private set; } // For animation compatibility

    // NEW ADVANCED PROPERTIES
    public LightEffect CurrentLightEffect => _currentLightEffect;
    public float CurrentEffectIntensity => _effectIntensity;
    public bool CanUseAdvancedFeatures => _enableAdvancedFeatures && HasLantern;

    // Internal state
    private float _currentMana;
    private bool _isInitialized = false;
    private Dictionary<ILightInteractable, LightEffectInstance> _activeEffects;
    private List<Vector3> _currentLightPath;
    private Coroutine _manaRegenCoroutine;
    private LightEffectData _currentEffectData;

    // Components
    private Rigidbody2D _rb;
    private FloatingLantern _floatingLantern;

    // Active effect tracking
    private class LightEffectInstance
    {
        public ILightInteractable target;
        public LightEffect effectType;
        public float intensity;
        public Vector2 direction;
        public float startTime;

        public LightEffectInstance(ILightInteractable target, LightEffect effect, float intensity, Vector2 direction)
        {
            this.target = target;
            this.effectType = effect;
            this.intensity = intensity;
            this.direction = direction;
            this.startTime = Time.time;
        }
    }

    // Events for system integration
    public System.Action OnLanternAcquired;
    public System.Action<LightType> OnLightTypeChanged;
    public System.Action<ILightInteractable> OnObjectIlluminated;
    public System.Action<ILightInteractable> OnObjectLeftLight;

    private void Awake()
    {
        InitializeComponents();
        SetupDefaultEffects();
        SetupLegacyCompatibility();
        ValidateLanternPositioning();
    }

    private void Start()
    {
        CompleteInitialization();
    }

    private void Update()
    {
        if (!HasLantern) return;

        HandleInput();
        UpdateLightDetection();
        UpdateActiveEffects();
        UpdateVisualComponents();

        if (IsLanternActive)
            UpdateLanternLightPositions();

        // Update velocity for animation compatibility
        if (_rb != null)
        {
            HorizontalVelocity = _rb.linearVelocity.x;
            VerticalVelocity = _rb.linearVelocity.y;
        }
    }

    #region Initialization

    private void InitializeComponents()
    {
        _rb = GetComponent<Rigidbody2D>();
        _activeEffects = new Dictionary<ILightInteractable, LightEffectInstance>();
        _currentLightPath = new List<Vector3>();
        _currentMana = _baseMana;

        // Setup light components
        SetupLightComponents();

        if (_debugMode)
            Debug.Log("✓ Enhanced LanternController components initialized");
    }

    private void SetupLightComponents()
    {
        // Setup inner light
        if (_playerInnerLight2D == null)
        {
            var lightObj = new GameObject("PlayerInnerLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _playerInnerLight2D = lightObj.AddComponent<Light2D>();
        }

        _playerInnerLight2D.lightType = Light2D.LightType.Point;
        _playerInnerLight2D.intensity = 0.5f;
        _playerInnerLight2D.pointLightInnerRadius = 0.1f;
        _playerInnerLight2D.pointLightOuterRadius = 2f;
        _playerInnerLight2D.color = new Color(1f, 0.9f, 0.6f);
        _playerInnerLight2D.enabled = false;

        // Setup beam renderer
        if (_beamRenderer == null)
        {
            var beamObj = new GameObject("LightBeam");
            beamObj.transform.SetParent(transform);
            beamObj.transform.localPosition = Vector3.zero;
            _beamRenderer = beamObj.AddComponent<LineRenderer>();
        }

        _beamRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _beamRenderer.startColor = Color.white;
        _beamRenderer.startWidth = 0.2f;
        _beamRenderer.endWidth = 0.1f;
        _beamRenderer.positionCount = 0;
        _beamRenderer.enabled = false;
    }

    private void SetupDefaultEffects()
    {
        if (_availableEffects == null || _availableEffects.Length == 0)
        {
            _availableEffects = new LightEffectData[]
            {
                new LightEffectData
                {
                    effectType = LightEffect.Reveal,
                    displayName = "Reveal",
                    description = "Basic illumination that reveals hidden objects",
                    effectColor = new Color(1f, 0.9f, 0.7f),
                    baseIntensity = 1f,
                    baseRange = _baseRange,
                    baseWidth = _beamWidth,
                    manaCostPerSecond = 1f
                },
                new LightEffectData
                {
                    effectType = LightEffect.Energize,
                    displayName = "Energize",
                    description = "Powers ancient mechanisms",
                    effectColor = new Color(0.3f, 0.8f, 1f),
                    baseIntensity = 1.2f,
                    baseRange = _baseRange * 0.8f,
                    baseWidth = _beamWidth * 0.8f,
                    manaCostPerSecond = 2f
                },
                new LightEffectData
                {
                    effectType = LightEffect.Stun,
                    displayName = "Solar Flare",
                    description = "Burst effect for stunning enemies and activating nodes",
                    effectColor = new Color(1f, 0.8f, 0.2f),
                    baseIntensity = 2f,
                    baseRange = 8f,
                    baseWidth = 60f,
                    requiresContinuousLight = false,
                    activationCost = 25f
                },
                new LightEffectData
                {
                    effectType = LightEffect.Refract,
                    displayName = "Prism Beam",
                    description = "Focused beam that bounces off reflective surfaces",
                    effectColor = new Color(0.8f, 0.4f, 1f),
                    baseIntensity = 1.5f,
                    baseRange = _baseRange * 1.2f,
                    baseWidth = _beamWidth * 0.5f,
                    canBounceOffSurfaces = true,
                    manaCostPerSecond = 3f
                }
            };
        }

        UpdateCurrentEffectData();
    }

    private void SetupLegacyCompatibility()
    {
        // Initialize legacy light types list
        if (_discoveredLightTypes.Count == 0)
        {
            _discoveredLightTypes.Add(LightType.Ember);
        }
    }

    private void CompleteInitialization()
    {
        // Find floating lantern if it exists
        _floatingLantern = FindObjectOfType<FloatingLantern>();

        _isInitialized = true;

        if (_debugMode)
            Debug.Log("✓ Enhanced LanternController initialization completed");
    }

    #endregion

    #region Legacy Interface (PRESERVED)

    /// <summary>
    /// LEGACY METHOD: Maintains compatibility with existing code
    /// </summary>
    public void AcquireLantern()
    {
        if (HasLantern)
        {
            if (_debugMode)
                Debug.LogWarning("Player already has lantern!");
            return;
        }

        HasLantern = true;

        // Enable default light effect
        _currentLightEffect = LightEffect.Reveal;
        UpdateCurrentEffectData();

        // Start mana regeneration
        if (_manaRegenCoroutine != null)
            StopCoroutine(_manaRegenCoroutine);
        _manaRegenCoroutine = StartCoroutine(ManaRegeneration());

        OnLanternAcquired?.Invoke();

        if (_debugMode)
            Debug.Log("🔦 Lantern acquired! Enhanced features enabled.");
    }

    /// <summary>
    /// LEGACY METHOD: Activate lantern (F key functionality)
    /// </summary>
    public void ActivateLantern()
    {
        if (!HasLantern || IsLanternActive) return;

        IsLanternActive = true;

        // Enable visual components
        if (_playerInnerLight2D != null)
            _playerInnerLight2D.enabled = true;

        if (_beamRenderer != null)
            _beamRenderer.enabled = true;

        if (_debugMode)
            Debug.Log("🔦 Lantern activated");
    }

    /// <summary>
    /// LEGACY METHOD: Deactivate lantern
    /// </summary>
    public void DeactivateLantern()
    {
        if (!IsLanternActive) return;

        IsLanternActive = false;

        // Clear all active effects
        ClearAllEffects();

        // Disable visual components
        if (_playerInnerLight2D != null)
            _playerInnerLight2D.enabled = false;

        if (_beamRenderer != null)
            _beamRenderer.enabled = false;

        if (_debugMode)
            Debug.Log("🔦 Lantern deactivated");
    }

    /// <summary>
    /// LEGACY METHOD: Switch light types (mouse wheel)
    /// </summary>
    public void SwitchToLightType(LightType lightType)
    {
        if (!_discoveredLightTypes.Contains(lightType)) return;

        _currentLightType = lightType;

        // Map legacy light type to new effect
        _currentLightEffect = MapLightTypeToEffect(lightType);
        UpdateCurrentEffectData();

        OnLightTypeChanged?.Invoke(lightType);

        if (_debugMode)
            Debug.Log($"🔦 Light type changed to: {lightType} ({_currentLightEffect})");
    }

    /// <summary>
    /// LEGACY METHOD: Consume mana for abilities
    /// </summary>
    public bool ConsumeMana(float amount)
    {
        if (_currentMana < amount) return false;

        _currentMana = Mathf.Max(0f, _currentMana - amount);
        return true;
    }

    /// <summary>
    /// LEGACY METHOD: Add mana (for pickups)
    /// </summary>
    public void AddMana(float amount)
    {
        _currentMana = Mathf.Min(_baseMana, _currentMana + amount);
    }

    #endregion

    #region New Advanced Interface

    /// <summary>
    /// NEW: Switch to specific light effect
    /// </summary>
    public void SetLightEffect(LightEffect effect)
    {
        if (_currentLightEffect == effect) return;

        // Clear current effects
        ClearAllEffects();

        _currentLightEffect = effect;
        UpdateCurrentEffectData();

        // Update legacy light type for compatibility
        _currentLightType = MapEffectToLightType(effect);
        OnLightTypeChanged?.Invoke(_currentLightType);

        if (_debugMode)
            Debug.Log($"💡 Light effect changed to: {effect}");
    }

    /// <summary>
    /// NEW: Trigger burst effect (like Solar Flare)
    /// </summary>
    public void TriggerBurstEffect(LightEffect effect, Vector3 center, float range = -1f)
    {
        var effectData = GetEffectData(effect);
        if (effectData == null) return;

        float actualRange = range > 0 ? range : effectData.baseRange;

        // Check mana cost
        if (effectData.activationCost > 0 && !ConsumeMana(effectData.activationCost))
        {
            if (_debugMode)
                Debug.Log($"❌ Not enough mana for {effect}");
            return;
        }

        // Find all objects in burst radius
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, actualRange, _interactionLayers);
        var affected = new List<ILightInteractable>();

        foreach (var collider in colliders)
        {
            var interactable = collider.GetComponent<ILightInteractable>();
            if (interactable != null && interactable.RespondsToEffect(effect))
            {
                float distance = Vector3.Distance(center, collider.transform.position);
                float intensity = CalculateIntensity(distance, actualRange);

                if (intensity >= interactable.GetMinimumIntensity(effect))
                {
                    Vector2 direction = (collider.transform.position - center).normalized;
                    interactable.OnLightEnter(effect, intensity, direction);
                    affected.Add(interactable);
                }
            }
        }

        // Handle effect duration for burst effects
        if (effectData.requiresContinuousLight == false && affected.Count > 0)
        {
            StartCoroutine(HandleBurstDuration(effect, affected, 3f)); // 3 second duration
        }

        if (_debugMode)
            Debug.Log($"💥 Burst {effect}: {affected.Count} objects affected");
    }

    /// <summary>
    /// NEW: Get current effect data
    /// </summary>
    public LightEffectData GetCurrentEffectData()
    {
        return _currentEffectData;
    }

    /// <summary>
    /// NEW: Check if player has discovered specific effect
    /// </summary>
    public bool HasEffect(LightEffect effect)
    {
        return GetEffectData(effect) != null;
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        if (!HasLantern) return;

        // Lantern toggle (F key)
        if (InputManager.LanternTogglePressed)
        {
            if (IsLanternActive)
                DeactivateLantern();
            else
                ActivateLantern();
        }

        // Light type cycling (mouse wheel) - Legacy compatibility
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f && _discoveredLightTypes.Count > 1)
        {
            CycleLightType(scrollInput > 0f);
        }

        // Advanced effect switching (number keys)
        if (_enableAdvancedFeatures)
        {
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    var effectIndex = i - 1;
                    if (effectIndex < _availableEffects.Length)
                    {
                        SetLightEffect(_availableEffects[effectIndex].effectType);
                    }
                }
            }
        }
    }

    private void CycleLightType(bool forward)
    {
        int currentIndex = _discoveredLightTypes.IndexOf(_currentLightType);
        if (currentIndex == -1) return;

        int newIndex;
        if (forward)
        {
            newIndex = (currentIndex + 1) % _discoveredLightTypes.Count;
        }
        else
        {
            newIndex = currentIndex - 1;
            if (newIndex < 0) newIndex = _discoveredLightTypes.Count - 1;
        }

        SwitchToLightType(_discoveredLightTypes[newIndex]);
    }

    #endregion

    #region Light Detection and Effects

    private void UpdateLightDetection()
    {
        if (!IsLanternActive || _currentEffectData == null) return;

        Vector3 beamOrigin = GetBeamOriginFixed();
        Vector2 beamDirection = GetBeamDirectionFixed();

        var newlyDetected = new HashSet<ILightInteractable>();

        // Perform light path calculation
        CalculateLightPath(beamOrigin, beamDirection, newlyDetected);

        // Process changes in illumination
        ProcessIlluminationChanges(newlyDetected, beamDirection);

        // Update beam visualization
        UpdateBeamVisualization();
    }

    private void CalculateLightPath(Vector3 startPos, Vector2 direction, HashSet<ILightInteractable> detected)
    {
        _currentLightPath.Clear();
        _currentLightPath.Add(startPos);

        Vector3 currentPos = startPos;
        Vector2 currentDir = direction;
        float remainingRange = _currentEffectData.baseRange;

        for (int bounce = 0; bounce <= _maxReflectionBounces && remainingRange > 0; bounce++)
        {
            // Perform detection along current segment
            DetectAlongSegment(currentPos, currentDir, remainingRange, detected);

            // Check for reflection if enabled
            if (!_enableReflection || !_currentEffectData.canBounceOffSurfaces) break;

            RaycastHit2D reflectionHit = Physics2D.Raycast(currentPos, currentDir, remainingRange, LayerMask.GetMask("Reflective"));
            if (reflectionHit.collider == null) break;

            // Calculate reflection
            Vector2 hitNormal = reflectionHit.normal;
            Vector2 reflectedDir = Vector2.Reflect(currentDir, hitNormal);

            currentPos = reflectionHit.point + hitNormal * 0.01f;
            currentDir = reflectedDir;
            remainingRange -= reflectionHit.distance;

            _currentLightPath.Add(currentPos);
        }
    }

    private void DetectAlongSegment(Vector3 startPos, Vector2 direction, float maxDistance, HashSet<ILightInteractable> detected)
    {
        // Use cone detection for more natural light behavior
        float halfAngle = _currentEffectData.baseWidth * 0.5f;
        int rayCount = 5; // Multiple rays for cone detection

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount > 1 ? (float)i / (rayCount - 1) : 0.5f;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 rayDir = RotateVector2(direction, angle);

            RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, rayDir, maxDistance, _interactionLayers);

            foreach (var hit in hits)
            {
                var interactable = hit.collider.GetComponent<ILightInteractable>();
                if (interactable != null && interactable.RespondsToEffect(_currentLightEffect))
                {
                    float intensity = CalculateIntensity(hit.distance, maxDistance);
                    if (intensity >= interactable.GetMinimumIntensity(_currentLightEffect))
                    {
                        detected.Add(interactable);
                    }
                }

                // Check if light is blocked
                if (!_currentEffectData.canPierceObjects)
                {
                    break;
                }
            }
        }
    }

    private void ProcessIlluminationChanges(HashSet<ILightInteractable> newlyDetected, Vector2 direction)
    {
        // Start new effects
        foreach (var interactable in newlyDetected)
        {
            if (!_activeEffects.ContainsKey(interactable))
            {
                StartEffect(interactable, direction);
            }
        }

        // End effects that are no longer detected
        var toRemove = new List<ILightInteractable>();
        foreach (var kvp in _activeEffects)
        {
            if (!newlyDetected.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var interactable in toRemove)
        {
            EndEffect(interactable);
        }
    }

    private void StartEffect(ILightInteractable target, Vector2 direction)
    {
        float intensity = _currentEffectData.baseIntensity * _effectIntensity;

        var instance = new LightEffectInstance(target, _currentLightEffect, intensity, direction);
        _activeEffects[target] = instance;

        target.OnLightEnter(_currentLightEffect, intensity, direction);
        OnObjectIlluminated?.Invoke(target);

        if (_debugMode)
            Debug.Log($"💡 Started {_currentLightEffect} effect on {target}");
    }

    private void EndEffect(ILightInteractable target)
    {
        if (_activeEffects.TryGetValue(target, out var instance))
        {
            target.OnLightExit(instance.effectType);
            _activeEffects.Remove(target);
            OnObjectLeftLight?.Invoke(target);

            if (_debugMode)
                Debug.Log($"💡 Ended {instance.effectType} effect on {target}");
        }
    }

    private void UpdateActiveEffects()
    {
        float deltaTime = Time.deltaTime;

        // Consume mana for continuous effects
        if (_currentEffectData.requiresContinuousLight && _activeEffects.Count > 0)
        {
            float manaCost = _currentEffectData.manaCostPerSecond * deltaTime;
            if (!ConsumeMana(manaCost))
            {
                // Not enough mana - deactivate lantern
                DeactivateLantern();
                return;
            }
        }

        // Update each active effect
        foreach (var kvp in _activeEffects)
        {
            var instance = kvp.Value;
            instance.target.OnLightStay(instance.effectType, instance.intensity, instance.direction, deltaTime);
        }
    }

    private void ClearAllEffects()
    {
        foreach (var kvp in _activeEffects)
        {
            kvp.Key.OnLightExit(kvp.Value.effectType);
            OnObjectLeftLight?.Invoke(kvp.Key);
        }
        _activeEffects.Clear();
    }

    #endregion

    #region LANTERN POSITIONING FIXES

    [Header("Lantern Positioning Debug")]
    [SerializeField] private bool _debugLanternPosition = true;

    /// <summary>
    /// Ensure lantern light sources are positioned at lantern, not player
    /// </summary>
    private void ValidateLanternPositioning()
    {
        // Make sure floating lantern is properly connected
        if (_floatingLantern == null)
        {
            _floatingLantern = GetComponentInChildren<FloatingLantern>();

            if (_floatingLantern == null)
            {
                Debug.LogWarning("⚠️ No FloatingLantern found! Creating basic lantern position...");
                CreateBasicLanternPosition();
            }
        }

        // Validate that the lantern light is positioned correctly
        if (_floatingLantern != null)
        {
            // Make sure the FloatingLantern's light components are the ones being used
            Light2D lanternLight = _floatingLantern.GetComponent<Light2D>();
            if (lanternLight != null && _playerInnerLight2D != lanternLight)
            {
                if (_debugLanternPosition)
                    Debug.Log("🔄 Connecting to FloatingLantern's Light2D component");

                _playerInnerLight2D = lanternLight;
            }

            if (_debugLanternPosition)
            {
                Debug.Log($"✓ Lantern positioned at: {_floatingLantern.transform.position}");
                Debug.Log($"✓ Player positioned at: {transform.position}");
                Debug.Log($"✓ Distance: {Vector3.Distance(_floatingLantern.transform.position, transform.position):F2}");
            }
        }
    }

    /// <summary>
    /// Create a basic lantern position if FloatingLantern component is missing
    /// </summary>
    private void CreateBasicLanternPosition()
    {
        GameObject lanternObj = new GameObject("BasicFloatingLantern");
        lanternObj.transform.SetParent(transform);
        lanternObj.transform.localPosition = new Vector3(1.5f, 0.8f, 0f);

        // Add the FloatingLantern component
        _floatingLantern = lanternObj.AddComponent<FloatingLantern>();

        if (_debugLanternPosition)
            Debug.Log("🔨 Created basic floating lantern at offset position");
    }

    /// <summary>
    /// FIXED: Get beam origin from lantern position, not player
    /// </summary>
    private Vector3 GetBeamOriginFixed()
    {
        if (_floatingLantern != null)
        {
            // Use the FloatingLantern's beam origin if available
            if (_floatingLantern.GetBeamOriginTransform() != null)
                return _floatingLantern.GetBeamOriginTransform().position;

            // Otherwise use the FloatingLantern's position
            return _floatingLantern.transform.position;
        }

        // Fallback to player position with offset (should not happen in normal gameplay)
        if (_debugLanternPosition)
            Debug.LogWarning("⚠️ Falling back to player position for beam origin!");

        return transform.position + Vector3.up * 0.5f;
    }

    /// <summary>
    /// FIXED: Get beam direction from lantern to mouse, not player to mouse
    /// </summary>
    private Vector2 GetBeamDirectionFixed()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(InputManager.MousePosition);
        mousePos.z = 0f;
        Vector3 beamOrigin = GetBeamOriginFixed(); // Use fixed origin
        return ((Vector2)mousePos - (Vector2)beamOrigin).normalized;
    }

    /// <summary>
    /// Ensure all lantern-based lights are positioned at the lantern
    /// </summary>
    private void UpdateLanternLightPositions()
    {
        if (_floatingLantern == null) return;

        // The FloatingLantern should handle its own Light2D positioning
        // We just need to make sure abilities/effects use the lantern position

        // Update any ability effects to use lantern position
        if (_currentEffectData != null && IsLanternActive)
        {
            Vector3 lanternPos = GetBeamOriginFixed();
            Vector2 beamDir = GetBeamDirectionFixed();

            // Make sure beam visualization starts from lantern
            if (_beamRenderer != null && _beamRenderer.positionCount > 0)
            {
                _beamRenderer.SetPosition(0, lanternPos);
            }
        }
    }

    /// <summary>
    /// Debug visualization for lantern positioning
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!_debugLanternPosition) return;

        // Draw player position
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);

        // Draw lantern position
        if (_floatingLantern != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_floatingLantern.transform.position, 0.2f);

            // Draw connection line
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _floatingLantern.transform.position);

            // Draw beam direction if active
            if (IsLanternActive && Application.isPlaying)
            {
                Vector3 beamOrigin = GetBeamOriginFixed();
                Vector2 beamDir = GetBeamDirectionFixed();

                Gizmos.color = Color.red;
                Gizmos.DrawRay(beamOrigin, beamDir * 5f);
            }
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + new Vector3(1.5f, 0.8f, 0f), Vector3.one * 0.2f);
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateVisualComponents()
    {
        if (!IsLanternActive) return;

        // Update inner light
        if (_playerInnerLight2D != null && _currentEffectData != null)
        {
            _playerInnerLight2D.color = _currentEffectData.effectColor;
            _playerInnerLight2D.intensity = _currentEffectData.baseIntensity * 0.5f;
        }
    }

    private void UpdateBeamVisualization()
    {
        if (_beamRenderer == null || _currentLightPath.Count < 2)
        {
            if (_beamRenderer != null)
                _beamRenderer.positionCount = 0;
            return;
        }

        _beamRenderer.positionCount = _currentLightPath.Count;
        for (int i = 0; i < _currentLightPath.Count; i++)
        {
            _beamRenderer.SetPosition(i, _currentLightPath[i]);
        }

        // Update beam color
        if (_currentEffectData != null)
        {
            _beamRenderer.startColor = _currentEffectData.effectColor;
        }
    }

    #endregion

    #region Utility Methods

    private float CalculateIntensity(float distance, float maxRange)
    {
        float normalizedDistance = Mathf.Clamp01(distance / maxRange);
        return _effectIntensity * (1f - normalizedDistance * normalizedDistance);
    }

    private Vector2 RotateVector2(Vector2 vector, float angleInDegrees)
    {
        float angleInRadians = angleInDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleInRadians);
        float sin = Mathf.Sin(angleInRadians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    private void UpdateCurrentEffectData()
    {
        _currentEffectData = GetEffectData(_currentLightEffect);
    }

    private LightEffectData GetEffectData(LightEffect effect)
    {
        foreach (var data in _availableEffects)
        {
            if (data.effectType == effect)
                return data;
        }
        return null;
    }

    private LightEffect MapLightTypeToEffect(LightType lightType)
    {
        return lightType switch
        {
            LightType.Ember => LightEffect.Reveal,
            LightType.Radiance => LightEffect.Energize,
            LightType.SolarFlare => LightEffect.Stun,
            LightType.PrismaticLight => LightEffect.Refract,
            LightType.Starlight => LightEffect.Purify,
            LightType.MoonBeam => LightEffect.Shield,
            LightType.VoidLight => LightEffect.Stealth,
            _ => LightEffect.Reveal
        };
    }

    private LightType MapEffectToLightType(LightEffect effect)
    {
        return effect switch
        {
            LightEffect.Reveal => LightType.Ember,
            LightEffect.Energize => LightType.Radiance,
            LightEffect.Stun => LightType.SolarFlare,
            LightEffect.Refract => LightType.PrismaticLight,
            LightEffect.Purify => LightType.Starlight,
            LightEffect.Shield => LightType.MoonBeam,
            LightEffect.Stealth => LightType.VoidLight,
            _ => LightType.Ember
        };
    }

    private IEnumerator HandleBurstDuration(LightEffect effect, List<ILightInteractable> affected, float duration)
    {
        yield return new WaitForSeconds(duration);

        foreach (var interactable in affected)
        {
            if (interactable != null)
            {
                interactable.OnLightExit(effect);
            }
        }
    }

    private IEnumerator ManaRegeneration()
    {
        while (HasLantern)
        {
            if (_currentMana < _baseMana && !IsLanternActive)
            {
                _currentMana = Mathf.Min(_baseMana, _currentMana + _manaRegenRate * Time.deltaTime);
            }
            yield return null;
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Show Current Status")]
    public void DebugShowStatus()
    {
        Debug.Log($"=== ENHANCED LANTERN CONTROLLER STATUS ===");
        Debug.Log($"Has Lantern: {HasLantern}");
        Debug.Log($"Is Active: {IsLanternActive}");
        Debug.Log($"Current Light Type: {CurrentLightType}");
        Debug.Log($"Current Effect: {CurrentLightEffect}");
        Debug.Log($"Mana: {_currentMana:F1}/{_baseMana:F1} ({ManaPercentage:P})");
        Debug.Log($"Active Effects: {_activeEffects.Count}");
        Debug.Log($"Advanced Features: {(_enableAdvancedFeatures ? "Enabled" : "Disabled")}");
    }

    [ContextMenu("Debug: Test Solar Flare")]
    public void DebugTestSolarFlare()
    {
        if (HasLantern)
        {
            TriggerBurstEffect(LightEffect.Stun, transform.position, 8f);
        }
    }

    #endregion
}