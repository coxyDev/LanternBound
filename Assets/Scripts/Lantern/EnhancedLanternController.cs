using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Enhanced Lantern Controller — DIRECTIONAL BEAM implementation
/// SINGLE SOURCE OF TRUTH for all mana state.
/// DualProgressionSystem delegates all mana checks and deductions here.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnhancedLanternController : MonoBehaviour
{
    #region Serialized Fields

    [Header("DIRECTIONAL BEAM SETTINGS")]
    [Tooltip("If true, beam projects forward from player facing direction. If false, uses full mouse control (legacy).")]
    [SerializeField] private bool _beamFollowsPlayerFacing = true;

    [Tooltip("Maximum angle (degrees) the mouse can adjust beam from player facing direction. 45° = moderate freedom, 30° = strict, 60° = forgiving")]
    [SerializeField] private float _mouseAimingConeAngle = 45f;

    [Tooltip("Show debug rays for beam direction")]
    [SerializeField] private bool _debugBeamDirection = true;

    [Header("Basic Lantern Properties")]
    [SerializeField] private float _baseMana = 100f;
    [SerializeField] private float _manaRegenRate = 10f;
    [SerializeField] private float _baseRange = 10f;
    [SerializeField] private float _beamWidth = 15f;

    [Header("Interaction Settings")]
    [SerializeField] private LayerMask _interactionLayers = ~0;
    [SerializeField] private int _maxReflectionBounces = 2;
    [SerializeField] private bool _enableReflection = false;

    [Header("Light Effects")]
    [SerializeField] private LightEffectData[] _availableEffects;

    [Header("Visual Components")]
    [SerializeField] private LineRenderer _beamRenderer;
    [SerializeField] private Light _playerInnerLight2D;
    [SerializeField] private ParticleSystem _lightParticles;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _debugLanternPosition = false;

    #endregion

    #region Light Effect System

    [System.Serializable]
    public class LightEffectData
    {
        public LightEffect effectType;
        public string displayName;
        public float baseIntensity = 1f;
        public float baseRange = 10f;
        public float baseWidth = 15f;
        public Color effectColor = Color.white;
        public float activationCost = 0f;
        public float continuousCost = 5f;
        public bool requiresContinuousLight = true;
        public bool canPierceObjects = false;
        public bool canBounceOffSurfaces = false;
    }

    public enum LightEffect
    {
        Reveal,      // Basic light — reveals hidden objects, clears corruption
        Energize,    // Powers ancient receptors and mechanisms
        Stun,        // Temporarily disables enemies
        Purify,      // Cleanses corruption
        Slow,        // Slows time/movement in area
        Refract,     // Bounces off reflective surfaces
        Shield,      // Creates protective barrier
        Decoy,       // Creates false light source
        Stealth      // Inverts light (creates darkness)
    }

    public enum LightType
    {
        Ember,           // Tutorial/basic light
        Radiance,        // Upgraded reveal
        SolarFlare,      // Area stun
        PrismaticLight,  // Refraction specialist
        Starlight,       // Purification
        MoonBeam,        // Shield
        VoidLight        // Stealth
    }

    #endregion

    #region Properties

    public bool HasLantern { get; private set; }
    public bool IsLanternActive { get; private set; }
    public float CurrentMana => _currentMana;
    public float MaxMana => _baseMana;
    public float ManaPercentage => _baseMana > 0 ? _currentMana / _baseMana : 0f;
    public LightEffect CurrentLightEffect => _currentLightEffect;
    public bool CanUseAdvancedFeatures => HasLantern;

    /// <summary>
    /// Set by DualProgressionSystem when ManaRegeneration upgrades are purchased.
    /// Expressed as a multiplier bonus: 0.1f = 10% faster regen.
    /// </summary>
    public float ManaRegenBonus { get; set; } = 0f;

    // Velocity for animation system compatibility
    public float HorizontalVelocity { get; private set; }
    public float VerticalVelocity { get; private set; }

    #endregion

    #region Private Fields

    private Rigidbody2D _rb;
    private PlayerMovement _playerMovement;

    private float _currentMana;
    private LightEffect _currentLightEffect = LightEffect.Reveal;
    private LightEffectData _currentEffectData;
    private float _effectIntensity = 1f;

    private Dictionary<ILightInteractable, LightEffectInstance> _activeEffects;
    private List<Vector3> _currentLightPath;

    // Events
    public System.Action OnLanternAcquired;
    public System.Action<LightType> OnLightTypeChanged;
    public System.Action<ILightInteractable> OnObjectIlluminated;
    public System.Action<ILightInteractable> OnObjectLeftLight;

    #endregion

    #region Light Effect Instance

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

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
        SetupDefaultEffects();
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
        RegenerateMana();

        if (_rb != null)
        {
            HorizontalVelocity = _rb.linearVelocity.x;
            VerticalVelocity = _rb.linearVelocity.y;
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerMovement = GetComponent<PlayerMovement>();

        _activeEffects = new Dictionary<ILightInteractable, LightEffectInstance>();
        _currentLightPath = new List<Vector3>();
        _currentMana = _baseMana;

        SetupBeamRenderer();

        if (_debugMode)
            Debug.Log("✓ EnhancedLanternController initialized (DIRECTIONAL BEAM — SINGLE MANA POOL)");
    }

    private void SetupBeamRenderer()
    {
        if (_beamRenderer == null)
        {
            GameObject beamObj = new GameObject("LightBeam");
            beamObj.transform.SetParent(transform);
            _beamRenderer = beamObj.AddComponent<LineRenderer>();

            _beamRenderer.startWidth = 0.1f;
            _beamRenderer.endWidth = 0.3f;
            _beamRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _beamRenderer.startColor = Color.yellow;
            _beamRenderer.endColor = new Color(1f, 1f, 0f, 0.3f);
            _beamRenderer.sortingOrder = 10;
        }

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
                    displayName = "Ember Light",
                    baseIntensity = 1f,
                    baseRange = 10f,
                    baseWidth = 15f,
                    effectColor = new Color(1f, 0.9f, 0.6f),
                    continuousCost = 5f,
                    requiresContinuousLight = true
                },
                new LightEffectData
                {
                    effectType = LightEffect.Energize,
                    displayName = "Energize",
                    baseIntensity = 1f,
                    baseRange = 10f,
                    baseWidth = 15f,
                    effectColor = new Color(0.8f, 1f, 0.6f),
                    continuousCost = 8f,
                    requiresContinuousLight = false
                }
            };
        }

        UpdateCurrentEffectData();
    }

    private void CompleteInitialization()
    {
        if (_debugMode && _beamFollowsPlayerFacing)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log("🎯 DIRECTIONAL BEAM MODE ACTIVE");
            Debug.Log($"   Mouse Aiming Cone: ±{_mouseAimingConeAngle}°");
            Debug.Log($"   Mana Pool: {_baseMana} (regen: {_manaRegenRate}/sec)");
            Debug.Log("═══════════════════════════════════");
        }
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        // Uses InputManager — never legacy Input.GetKeyDown
        if (InputManager.LanternTogglePressed)
        {
            ToggleLantern();
        }
    }

    private void ToggleLantern()
    {
        IsLanternActive = !IsLanternActive;

        if (_beamRenderer != null)
            _beamRenderer.enabled = IsLanternActive;

        if (_debugMode)
            Debug.Log($"💡 Lantern {(IsLanternActive ? "ON" : "OFF")} | Mana: {_currentMana:F0}/{_baseMana:F0}");
    }

    #endregion

    #region Directional Beam System

    private Vector2 GetBeamDirection()
    {
        if (!_beamFollowsPlayerFacing)
            return GetBeamDirectionFromMouse();

        Vector2 playerFacingDirection = GetPlayerFacingDirection();

        // Use InputManager.MousePosition — consistent with rest of codebase
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(InputManager.MousePosition.x, InputManager.MousePosition.y, 0f));
        mouseWorldPos.z = 0f;

        Vector3 beamOrigin = GetBeamOrigin();
        Vector2 mouseDirection = ((Vector2)mouseWorldPos - (Vector2)beamOrigin).normalized;

        float angleToMouse = Vector2.SignedAngle(playerFacingDirection, mouseDirection);
        float clampedAngle = Mathf.Clamp(angleToMouse, -_mouseAimingConeAngle, _mouseAimingConeAngle);
        Vector2 finalDirection = RotateVector2(playerFacingDirection, clampedAngle);

        if (_debugBeamDirection && IsLanternActive)
        {
            Debug.DrawRay(beamOrigin, playerFacingDirection * 3f, Color.blue, 0.1f);
            Debug.DrawRay(beamOrigin, mouseDirection * 2.5f, Color.red, 0.1f);
            Debug.DrawRay(beamOrigin, finalDirection * 5f, Color.yellow, 0.1f);

            Vector2 coneLeft = RotateVector2(playerFacingDirection, -_mouseAimingConeAngle);
            Vector2 coneRight = RotateVector2(playerFacingDirection, _mouseAimingConeAngle);
            Debug.DrawRay(beamOrigin, coneLeft * 4f, Color.green, 0.1f);
            Debug.DrawRay(beamOrigin, coneRight * 4f, Color.green, 0.1f);
        }

        return finalDirection;
    }

    private Vector2 GetPlayerFacingDirection()
    {
        if (_playerMovement != null)
            return _playerMovement._isFacingRight ? Vector2.right : Vector2.left;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            return spriteRenderer.flipX ? Vector2.left : Vector2.right;

        return transform.localScale.x > 0 ? Vector2.right : Vector2.left;
    }

    private Vector2 GetBeamDirectionFromMouse()
    {
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(InputManager.MousePosition.x, InputManager.MousePosition.y, 0f));
        mouseWorldPos.z = 0f;
        Vector3 beamOrigin = GetBeamOrigin();
        return ((Vector2)mouseWorldPos - (Vector2)beamOrigin).normalized;
    }

    private Vector3 GetBeamOrigin()
    {
        return transform.position + Vector3.up * 0.5f;
    }

    #endregion

    #region Light Detection

    private void UpdateLightDetection()
    {
        if (!IsLanternActive || _currentEffectData == null)
        {
            if (_activeEffects.Count > 0)
            {
                var targets = new List<ILightInteractable>(_activeEffects.Keys);
                foreach (var target in targets)
                    EndEffect(target);
            }
            return;
        }

        if (!ConsumeMana(_currentEffectData.continuousCost * Time.deltaTime))
        {
            IsLanternActive = false;
            _beamRenderer.enabled = false;

            if (_debugMode)
                Debug.Log("💡 Lantern OFF — out of mana");
            return;
        }

        Vector3 beamOrigin = GetBeamOrigin();
        Vector2 beamDirection = GetBeamDirection();

        HashSet<ILightInteractable> newlyDetected = new HashSet<ILightInteractable>();
        CalculateLightPath(beamOrigin, beamDirection, newlyDetected);
        ProcessIlluminationChanges(newlyDetected, beamDirection);
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
            DetectAlongSegment(currentPos, currentDir, remainingRange, detected);

            if (!_enableReflection || !_currentEffectData.canBounceOffSurfaces) break;

            RaycastHit2D reflectionHit = Physics2D.Raycast(currentPos, currentDir, remainingRange,
                LayerMask.GetMask("Reflective"));
            if (reflectionHit.collider == null) break;

            Vector2 hitNormal = reflectionHit.normal;
            Vector2 reflectedDir = Vector2.Reflect(currentDir, hitNormal);

            currentPos = reflectionHit.point + hitNormal * 0.01f;
            currentDir = reflectedDir;
            remainingRange -= reflectionHit.distance;

            _currentLightPath.Add(currentPos);
        }

        _currentLightPath.Add(currentPos + (Vector3)currentDir * remainingRange);
    }

    private void DetectAlongSegment(Vector3 startPos, Vector2 direction, float maxDistance,
        HashSet<ILightInteractable> detected)
    {
        float halfAngle = _currentEffectData.baseWidth * 0.5f;
        int rayCount = 5;

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount > 1 ? (float)i / (rayCount - 1) : 0.5f;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 rayDir = RotateVector2(direction, angle);

            RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, rayDir, maxDistance, _interactionLayers);

            foreach (var hit in hits)
            {
                var interactable = hit.collider.GetComponent<ILightInteractable>();

                if (interactable == null)
                    interactable = hit.collider.GetComponentInParent<ILightInteractable>();

                if (interactable != null && interactable.RespondsToEffect(_currentLightEffect))
                {
                    float intensity = CalculateIntensity(hit.distance, maxDistance);
                    if (intensity >= interactable.GetMinimumIntensity(_currentLightEffect))
                        detected.Add(interactable);
                }

                if (!_currentEffectData.canPierceObjects)
                    break;
            }
        }
    }

    private void ProcessIlluminationChanges(HashSet<ILightInteractable> newlyDetected, Vector2 direction)
    {
        foreach (var interactable in newlyDetected)
        {
            if (!_activeEffects.ContainsKey(interactable))
            {
                StartEffect(interactable, direction);
            }
            else
            {
                var instance = _activeEffects[interactable];
                interactable.OnLightStay(_currentLightEffect, instance.intensity, direction, Time.deltaTime);
            }
        }

        var toRemove = new List<ILightInteractable>();
        foreach (var kvp in _activeEffects)
        {
            if (!newlyDetected.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }

        foreach (var interactable in toRemove)
            EndEffect(interactable);
    }

    private void StartEffect(ILightInteractable target, Vector2 direction)
    {
        float intensity = _currentEffectData.baseIntensity * _effectIntensity;

        var instance = new LightEffectInstance(target, _currentLightEffect, intensity, direction);
        _activeEffects[target] = instance;

        target.OnLightEnter(_currentLightEffect, intensity, direction);
        OnObjectIlluminated?.Invoke(target);

        if (_debugMode)
            Debug.Log($"💡 Started {_currentLightEffect} on {(target as MonoBehaviour)?.name}");
    }

    private void EndEffect(ILightInteractable target)
    {
        if (_activeEffects.TryGetValue(target, out var instance))
        {
            target.OnLightExit(instance.effectType);
            _activeEffects.Remove(target);
            OnObjectLeftLight?.Invoke(target);

            if (_debugMode)
                Debug.Log($"💡 Ended {instance.effectType} on {(target as MonoBehaviour)?.name}");
        }
    }

    private void UpdateActiveEffects()
    {
        // Placeholder for future time-based effect logic
    }

    #endregion

    #region Mana System — Single Source of Truth

    /// <summary>
    /// Attempts to consume mana. Returns true if successful, false if insufficient.
    /// PUBLIC — DualProgressionSystem calls this for all ability costs.
    /// </summary>
    public bool ConsumeMana(float amount)
    {
        if (_currentMana >= amount)
        {
            _currentMana -= amount;
            _currentMana = Mathf.Max(0f, _currentMana);
            return true;
        }
        return false;
    }

    private void RegenerateMana()
    {
        if (_currentMana < _baseMana)
        {
            // ManaRegenBonus set by DualProgressionSystem when regen upgrades are purchased
            float effectiveRate = _manaRegenRate * (1f + ManaRegenBonus);
            _currentMana = Mathf.Min(_currentMana + effectiveRate * Time.deltaTime, _baseMana);
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateVisualComponents()
    {
        if (_beamRenderer != null && IsLanternActive)
            UpdateBeamVisualization();
    }

    private void UpdateBeamVisualization()
    {
        if (_beamRenderer == null || _currentLightPath.Count < 2) return;

        _beamRenderer.positionCount = _currentLightPath.Count;
        for (int i = 0; i < _currentLightPath.Count; i++)
            _beamRenderer.SetPosition(i, _currentLightPath[i]);

        if (_currentEffectData != null)
        {
            _beamRenderer.startColor = _currentEffectData.effectColor;
            Color endColor = _currentEffectData.effectColor;
            endColor.a = 0.3f;
            _beamRenderer.endColor = endColor;
        }
    }

    #endregion

    #region Public API

    public void AcquireLantern()
    {
        HasLantern = true;
        _currentMana = _baseMana;

        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log("✨ Lantern acquired");
            Debug.Log($"   Mana: {_currentMana:F0}/{_baseMana:F0}");
            Debug.Log("   Press F to toggle light");
            Debug.Log("═══════════════════════════════════");
        }

        OnLanternAcquired?.Invoke();
    }

    public void SetLightEffect(LightEffect effect)
    {
        _currentLightEffect = effect;
        UpdateCurrentEffectData();

        if (_debugMode)
            Debug.Log($"🔦 Light effect: {effect}");
    }

    /// <summary>
    /// Check if mana pool can cover a cost without deducting.
    /// Used by DualProgressionSystem before committing to ability use.
    /// </summary>
    public bool CanAffordAbility(float manaCost)
    {
        return _currentMana >= manaCost;
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

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!_debugLanternPosition || !HasLantern) return;

        Vector3 beamOrigin = GetBeamOrigin();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(beamOrigin, 0.2f);

        if (_beamFollowsPlayerFacing && Application.isPlaying)
        {
            Vector2 facingDir = GetPlayerFacingDirection();
            Vector2 coneLeft = RotateVector2(facingDir, -_mouseAimingConeAngle);
            Vector2 coneRight = RotateVector2(facingDir, _mouseAimingConeAngle);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(beamOrigin, facingDir * 5f);
            Gizmos.DrawRay(beamOrigin, coneLeft * 4f);
            Gizmos.DrawRay(beamOrigin, coneRight * 4f);
        }
    }

    #endregion
}