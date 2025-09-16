using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Comprehensive light effects system supporting 9 distinct interaction types
/// This replaces the basic ILightInteractable interface with sophisticated effect-based interactions
/// </summary>
public enum LightEffect
{
    Reveal,     // Basic illumination - shows hidden objects
    Energize,   // Powers nodes, mechanisms, and ancient tech
    Refract,    // Bounces off prisms and reflective surfaces
    Purify,     // Clears corruption, shadow, and dark energy
    Slow,       // Creates temporal distortion fields
    Stun,       // Disables enemies and interrupts channeling
    Shield,     // Creates protective barriers and safe zones
    Decoy,      // Projects phantom images and false targets
    Stealth     // Concealment and phase shifting
}

/// <summary>
/// Enhanced light interaction interface supporting complex effect-based gameplay
/// </summary>
public interface ILightInteractable
{
    /// <summary>
    /// Called when light first touches this object
    /// </summary>
    void OnLightEnter(LightEffect effect, float intensity, Vector2 direction);

    /// <summary>
    /// Called continuously while light is affecting this object
    /// </summary>
    void OnLightStay(LightEffect effect, float intensity, Vector2 direction, float deltaTime);

    /// <summary>
    /// Called when light stops affecting this object
    /// </summary>
    void OnLightExit(LightEffect effect);

    /// <summary>
    /// Check if this object responds to a specific light effect
    /// </summary>
    bool RespondsToEffect(LightEffect effect);

    /// <summary>
    /// Get the minimum intensity required for this effect to work
    /// </summary>
    float GetMinimumIntensity(LightEffect effect);

    /// <summary>
    /// Current state for UI and gameplay feedback
    /// </summary>
    bool IsCurrentlyIlluminated { get; }
    LightEffect CurrentActiveEffect { get; }
}

/// <summary>
/// Data structure defining how light effects behave
/// </summary>
[System.Serializable]
public class LightEffectData
{
    [Header("Basic Properties")]
    public LightEffect effectType;
    public string displayName;
    [TextArea(2, 3)]
    public string description;

    [Header("Visual Properties")]
    public Color effectColor = Color.white;
    public float baseIntensity = 1f;
    public float baseRange = 10f;
    public float baseWidth = 30f; // Degrees for cone effects

    [Header("Behavior")]
    public bool requiresContinuousLight = true;
    public bool canPierceObjects = false;
    public bool canBounceOffSurfaces = false;
    public float effectBuildupTime = 0f; // Time to reach full effect
    public float effectLingerTime = 0f;  // Time effect persists after light removal

    [Header("Resource Costs")]
    public float manaCostPerSecond = 2f;
    public float activationCost = 0f;

    [Header("Audio & Visual")]
    public AudioClip activationSound;
    public AudioClip sustainingSound;
    public AudioClip deactivationSound;
    public ParticleSystem effectParticles;
    public Material beamMaterial;

    [Header("Performance")]
    public int maxSimultaneousTargets = 10;
    public float raycastRefreshRate = 0.1f; // Seconds between detection updates
}

/// <summary>
/// Enhanced lantern controller supporting sophisticated light effects
/// </summary>
public class EnhancedLightEffectsController : MonoBehaviour
{
    [Header("Light Effect System")]
    [SerializeField] private LightEffectData[] _availableEffects;
    [SerializeField] private LightEffect _currentEffect = LightEffect.Reveal;
    [SerializeField] private float _effectIntensity = 1f;

    [Header("Detection System")]
    [SerializeField] private LayerMask _interactionLayers = -1;
    [SerializeField] private float _detectionRefreshRate = 0.1f;
    [SerializeField] private bool _useConeDetection = true;
    [SerializeField] private bool _useMultiRay = true;
    [SerializeField] private int _rayCount = 7;

    [Header("Reflection System")]
    [SerializeField] private bool _enableReflection = true;
    [SerializeField] private int _maxReflectionBounces = 3;
    [SerializeField] private LayerMask _reflectiveLayers = 0;

    [Header("Debug & Visualization")]
    [SerializeField] private bool _showDebugRays = true;
    [SerializeField] private bool _verboseLogging = false;

    // Core references
    private EnhancedLanternController _lanternController;
    private FloatingLantern _floatingLantern;

    // Effect tracking
    private Dictionary<ILightInteractable, LightEffectInstance> _activeEffects;
    private List<Vector3> _currentLightPath;
    private Coroutine _detectionCoroutine;

    // Performance optimization
    private float _lastDetectionTime;
    private LightEffectData _currentEffectData;

    /// <summary>
    /// Represents an active light effect on an object
    /// </summary>
    private class LightEffectInstance
    {
        public ILightInteractable target;
        public LightEffect effectType;
        public float intensity;
        public Vector2 direction;
        public float startTime;
        public float accumulatedTime;
        public bool isBuiltUp;

        public LightEffectInstance(ILightInteractable target, LightEffect effect, float intensity, Vector2 direction)
        {
            this.target = target;
            this.effectType = effect;
            this.intensity = intensity;
            this.direction = direction;
            this.startTime = Time.time;
            this.accumulatedTime = 0f;
            this.isBuiltUp = false;
        }
    }

    private void Awake()
    {
        _activeEffects = new Dictionary<ILightInteractable, LightEffectInstance>();
        _currentLightPath = new List<Vector3>();

        // Get required components
        _lanternController = GetComponent<EnhancedLanternController>();

        // Setup default effects if none provided
        if (_availableEffects == null || _availableEffects.Length == 0)
        {
            CreateDefaultEffectData();
        }

        UpdateCurrentEffectData();
    }

    private void Start()
    {
        // Find floating lantern
        _floatingLantern = FindObjectOfType<FloatingLantern>();

        if (_verboseLogging)
        {
            Debug.Log("✓ Enhanced Light Effects Controller initialized");
            Debug.Log($"  Available effects: {_availableEffects.Length}");
            Debug.Log($"  Current effect: {_currentEffect}");
        }
    }

    private void Update()
    {
        if (_lanternController == null || !_lanternController.IsLanternActive)
        {
            ClearAllEffects();
            return;
        }

        // Update effect detection at controlled rate
        if (Time.time - _lastDetectionTime >= _detectionRefreshRate)
        {
            UpdateLightEffects();
            _lastDetectionTime = Time.time;
        }

        // Update active effects
        UpdateActiveEffects();
    }

    #region Effect Data Management

    private void CreateDefaultEffectData()
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
                baseRange = 10f,
                baseWidth = 25f,
                requiresContinuousLight = true,
                manaCostPerSecond = 1f
            },
            new LightEffectData
            {
                effectType = LightEffect.Energize,
                displayName = "Energize",
                description = "Powers ancient mechanisms and crystal nodes",
                effectColor = new Color(0.3f, 0.8f, 1f),
                baseIntensity = 1.2f,
                baseRange = 8f,
                baseWidth = 20f,
                requiresContinuousLight = true,
                effectBuildupTime = 1f,
                manaCostPerSecond = 2f
            },
            new LightEffectData
            {
                effectType = LightEffect.Stun,
                displayName = "Solar Flare",
                description = "Intense burst that stuns enemies and activates multiple nodes",
                effectColor = new Color(1f, 0.8f, 0.2f),
                baseIntensity = 2f,
                baseRange = 6f,
                baseWidth = 60f,
                requiresContinuousLight = false,
                activationCost = 15f,
                effectLingerTime = 3f
            }
        };
    }

    private void UpdateCurrentEffectData()
    {
        _currentEffectData = GetEffectData(_currentEffect);
        if (_currentEffectData == null)
        {
            Debug.LogWarning($"No effect data found for {_currentEffect}");
        }
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

    #region Light Effect Detection

    private void UpdateLightEffects()
    {
        if (_currentEffectData == null || _floatingLantern == null) return;

        Vector3 beamOrigin = _floatingLantern.GetBeamOrigin();
        Vector2 beamDirection = GetBeamDirection();

        // Clear previous detection results
        var newlyDetected = new HashSet<ILightInteractable>();

        // Perform light path calculation
        CalculateLightPath(beamOrigin, beamDirection, newlyDetected);

        // Process changes in illumination
        ProcessIlluminationChanges(newlyDetected, beamDirection);

        if (_verboseLogging)
        {
            Debug.Log($"Light effect update: {newlyDetected.Count} objects detected");
        }
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

            // Check for reflection
            if (!_enableReflection || !_currentEffectData.canBounceOffSurfaces) break;

            RaycastHit2D reflectionHit = Physics2D.Raycast(currentPos, currentDir, remainingRange, _reflectiveLayers);
            if (reflectionHit.collider == null) break;

            // Calculate reflection
            Vector2 hitNormal = reflectionHit.normal;
            Vector2 reflectedDir = Vector2.Reflect(currentDir, hitNormal);

            currentPos = reflectionHit.point + hitNormal * 0.01f; // Offset to prevent re-hitting
            currentDir = reflectedDir;
            remainingRange -= reflectionHit.distance;

            _currentLightPath.Add(currentPos);

            if (_verboseLogging)
            {
                Debug.Log($"Light reflection {bounce}: {reflectionHit.collider.name}");
            }
        }
    }

    private void DetectAlongSegment(Vector3 startPos, Vector2 direction, float maxDistance, HashSet<ILightInteractable> detected)
    {
        if (_useConeDetection)
        {
            DetectCone(startPos, direction, maxDistance, detected);
        }
        else
        {
            DetectRay(startPos, direction, maxDistance, detected);
        }
    }

    private void DetectCone(Vector3 startPos, Vector2 direction, float maxDistance, HashSet<ILightInteractable> detected)
    {
        float halfAngle = _currentEffectData.baseWidth * 0.5f;
        int rayCount = _useMultiRay ? _rayCount : 1;

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount > 1 ? (float)i / (rayCount - 1) : 0.5f;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 rayDir = RotateVector2(direction, angle);

            DetectRay(startPos, rayDir, maxDistance, detected);
        }
    }

    private void DetectRay(Vector3 startPos, Vector2 direction, float maxDistance, HashSet<ILightInteractable> detected)
    {
        // Use RaycastAll to get all objects in path
        RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, direction, maxDistance, _interactionLayers);

        // Sort by distance
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var interactable = hit.collider.GetComponent<ILightInteractable>();
            if (interactable != null && interactable.RespondsToEffect(_currentEffect))
            {
                float intensity = CalculateIntensity(hit.distance, _currentEffectData.baseRange);
                if (intensity >= interactable.GetMinimumIntensity(_currentEffect))
                {
                    detected.Add(interactable);
                }
            }

            // Check if this object blocks further light propagation
            if (!_currentEffectData.canPierceObjects && hit.collider.gameObject.layer != LayerMask.NameToLayer("LightInteractable"))
            {
                break; // Stop raycast here
            }
        }

        // Debug visualization
        if (_showDebugRays)
        {
            Color rayColor = hits.Length > 0 ? Color.yellow : Color.red;
            Debug.DrawRay(startPos, direction * maxDistance, rayColor, _detectionRefreshRate);
        }
    }

    private float CalculateIntensity(float distance, float maxRange)
    {
        // Quadratic falloff for more natural light behavior
        float normalizedDistance = Mathf.Clamp01(distance / maxRange);
        return _effectIntensity * (1f - normalizedDistance * normalizedDistance);
    }

    #endregion

    #region Effect Instance Management

    private void ProcessIlluminationChanges(HashSet<ILightInteractable> newlyDetected, Vector2 direction)
    {
        // Find newly illuminated objects
        foreach (var interactable in newlyDetected)
        {
            if (!_activeEffects.ContainsKey(interactable))
            {
                StartEffect(interactable, direction);
            }
        }

        // Find objects that left the light
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
        float intensity = CalculateIntensity(0f, _currentEffectData.baseRange); // TODO: Calculate actual distance

        var instance = new LightEffectInstance(target, _currentEffect, intensity, direction);
        _activeEffects[target] = instance;

        target.OnLightEnter(_currentEffect, intensity, direction);

        if (_verboseLogging)
        {
            Debug.Log($"Started effect {_currentEffect} on {target}");
        }
    }

    private void EndEffect(ILightInteractable target)
    {
        if (_activeEffects.TryGetValue(target, out var instance))
        {
            target.OnLightExit(instance.effectType);
            _activeEffects.Remove(target);

            if (_verboseLogging)
            {
                Debug.Log($"Ended effect {instance.effectType} on {target}");
            }
        }
    }

    private void UpdateActiveEffects()
    {
        float deltaTime = Time.deltaTime;

        foreach (var kvp in _activeEffects)
        {
            var instance = kvp.Value;
            instance.accumulatedTime += deltaTime;

            // Check if effect has built up to full strength
            if (!instance.isBuiltUp && instance.accumulatedTime >= _currentEffectData.effectBuildupTime)
            {
                instance.isBuiltUp = true;
            }

            // Update the effect
            instance.target.OnLightStay(instance.effectType, instance.intensity, instance.direction, deltaTime);
        }
    }

    private void ClearAllEffects()
    {
        foreach (var kvp in _activeEffects)
        {
            kvp.Key.OnLightExit(kvp.Value.effectType);
        }
        _activeEffects.Clear();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Switch to a different light effect
    /// </summary>
    public void SetLightEffect(LightEffect effect)
    {
        if (_currentEffect == effect) return;

        // Clear current effects
        ClearAllEffects();

        _currentEffect = effect;
        UpdateCurrentEffectData();

        if (_verboseLogging)
        {
            Debug.Log($"Switched to light effect: {effect}");
        }
    }

    /// <summary>
    /// Get the current light effect data
    /// </summary>
    public LightEffectData GetCurrentEffectData()
    {
        return _currentEffectData;
    }

    /// <summary>
    /// Check if a specific effect is available
    /// </summary>
    public bool HasEffect(LightEffect effect)
    {
        return GetEffectData(effect) != null;
    }

    /// <summary>
    /// Get all available effects
    /// </summary>
    public LightEffect[] GetAvailableEffects()
    {
        var effects = new LightEffect[_availableEffects.Length];
        for (int i = 0; i < _availableEffects.Length; i++)
        {
            effects[i] = _availableEffects[i].effectType;
        }
        return effects;
    }

    /// <summary>
    /// Trigger a burst effect (like Solar Flare)
    /// </summary>
    public void TriggerBurstEffect(LightEffect effect, Vector3 center, float overrideRange = -1f)
    {
        var effectData = GetEffectData(effect);
        if (effectData == null) return;

        float range = overrideRange > 0 ? overrideRange : effectData.baseRange;
        var affected = new List<ILightInteractable>();

        // Find all objects in burst radius
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, range, _interactionLayers);

        foreach (var collider in colliders)
        {
            var interactable = collider.GetComponent<ILightInteractable>();
            if (interactable != null && interactable.RespondsToEffect(effect))
            {
                float distance = Vector3.Distance(center, collider.transform.position);
                float intensity = CalculateIntensity(distance, range);

                if (intensity >= interactable.GetMinimumIntensity(effect))
                {
                    Vector2 direction = (collider.transform.position - center).normalized;

                    // Trigger burst effect
                    interactable.OnLightEnter(effect, intensity, direction);
                    affected.Add(interactable);
                }
            }
        }

        // Start coroutine to handle linger time
        if (effectData.effectLingerTime > 0)
        {
            StartCoroutine(HandleBurstLingerEffect(effect, affected, effectData.effectLingerTime));
        }

        if (_verboseLogging)
        {
            Debug.Log($"Triggered burst effect {effect}: {affected.Count} objects affected");
        }
    }

    private IEnumerator HandleBurstLingerEffect(LightEffect effect, List<ILightInteractable> affected, float lingerTime)
    {
        yield return new WaitForSeconds(lingerTime);

        foreach (var interactable in affected)
        {
            if (interactable != null)
            {
                interactable.OnLightExit(effect);
            }
        }
    }

    #endregion

    #region Utility Methods

    private Vector2 GetBeamDirection()
    {
        // Get direction from lantern controller or use mouse input
        if (_lanternController != null)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector3 beamOrigin = _floatingLantern != null ? _floatingLantern.GetBeamOrigin() : transform.position;
            return ((Vector2)mousePos - (Vector2)beamOrigin).normalized;
        }

        return Vector2.right; // Default direction
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

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Show Current Effects")]
    public void DebugShowCurrentEffects()
    {
        Debug.Log($"=== LIGHT EFFECTS DEBUG ===");
        Debug.Log($"Current Effect: {_currentEffect}");
        Debug.Log($"Active Effects: {_activeEffects.Count}");

        foreach (var kvp in _activeEffects)
        {
            var instance = kvp.Value;
            Debug.Log($"  {kvp.Key}: {instance.effectType} (intensity: {instance.intensity:F2}, time: {instance.accumulatedTime:F2}s)");
        }
    }

    [ContextMenu("Debug: Test Solar Flare")]
    public void DebugTestSolarFlare()
    {
        Vector3 center = transform.position;
        TriggerBurstEffect(LightEffect.Stun, center);
    }

    #endregion
}