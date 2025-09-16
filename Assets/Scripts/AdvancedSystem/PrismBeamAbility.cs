using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Prism Beam ability - channeled focusing beam that bounces off reflective surfaces
/// Primary Function: Precise routing through prisms to power distant conduits/gates
/// Secondary Function: Piercing shadow armor and interrupting enemy channelers
/// </summary>
public class PrismBeamAbility : MonoBehaviour
{
    [Header("Beam Configuration")]
    [SerializeField] private float _maxRange = 15f;
    [SerializeField] private float _beamWidth = 0.2f;
    [SerializeField] private float _focusTime = 0.5f; // Time to reach full power
    [SerializeField] private float _manaCostPerSecond = 8f;
    [SerializeField] private float _maxChannelTime = 10f;

    [Header("Reflection System")]
    [SerializeField] private LayerMask _reflectiveLayers = 1 << 9;
    [SerializeField] private LayerMask _targetLayers = 1 << 6;
    [SerializeField] private int _maxReflections = 5;
    [SerializeField] private float _reflectionIntensityLoss = 0.1f; // 10% loss per bounce
    [SerializeField] private bool _enableAimAssist = true;
    [SerializeField] private float _aimAssistRange = 1f;

    [Header("Puzzle Mechanics")]
    [SerializeField] private float _prismChargeRate = 2f;
    [SerializeField] private float _conduitActivationThreshold = 0.8f;
    [SerializeField] private bool _requiresContinuousBeam = true;
    [SerializeField] private float _prismOverloadProtection = 3f; // Max time before overload

    [Header("Enemy Mechanics")]
    [SerializeField] private float _armorPiercingDamage = 50f;
    [SerializeField] private float _channelInterruptRange = 1f;
    [SerializeField] private bool _ignoresLightResistance = true;

    [Header("Visual Effects")]
    [SerializeField] private LineRenderer _beamRenderer;
    [SerializeField] private Light2D _beamLight;
    [SerializeField] private ParticleSystem _focusParticles;
    [SerializeField] private ParticleSystem _reflectionParticles;
    [SerializeField] private ParticleSystem _impactParticles;
    [SerializeField] private Material _beamMaterial;
    [SerializeField] private Gradient _intensityGradient;

    [Header("Audio")]
    [SerializeField] private AudioClip _chargeSound;
    [SerializeField] private AudioClip _beamSustainSound;
    [SerializeField] private AudioClip _reflectionSound;
    [SerializeField] private AudioClip _targetHitSound;
    [SerializeField] private AudioClip _overloadWarningSound;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _showReflectionPaths = true;
    [SerializeField] private bool _showAimAssist = true;

    // State tracking
    public bool IsCharging { get; private set; }
    public bool IsChanneling { get; private set; }
    public float FocusProgress { get; private set; }
    public float ChannelTimeRemaining { get; private set; }
    public int CurrentReflectionCount { get; private set; }
    public float CurrentIntensity { get; private set; }

    // Components
    private EnhancedLanternController _lanternController;
    private EnhancedLightEffectsController _lightEffectsController;
    private DualProgressionSystem _progressionSystem;
    private AudioSource _audioSource;
    private FloatingLantern _floatingLantern;

    // Beam tracking
    private List<Vector3> _beamPath;
    private List<ReflectionPoint> _reflectionPoints;
    private List<IPrismBeamTarget> _currentTargets;
    private Vector3 _aimDirection;
    private Vector3 _beamOrigin;

    // Visual components
    private List<LineRenderer> _reflectionRenderers;
    private Coroutine _beamCoroutine;

    [System.Serializable]
    public class ReflectionPoint
    {
        public Vector3 position;
        public Vector3 incomingDirection;
        public Vector3 reflectedDirection;
        public float intensity;
        public PrismReflector reflector;
        public float hitTime;
    }

    // Events for system integration
    public System.Action OnBeamStarted;
    public System.Action OnBeamStopped;
    public System.Action<int> OnReflectionsChanged;
    public System.Action<IPrismBeamTarget> OnTargetHit;
    public System.Action OnOverloadWarning;

    private void Awake()
    {
        _beamPath = new List<Vector3>();
        _reflectionPoints = new List<ReflectionPoint>();
        _currentTargets = new List<IPrismBeamTarget>();
        _reflectionRenderers = new List<LineRenderer>();

        _lanternController = GetComponent<EnhancedLanternController>();
        _lightEffectsController = GetComponent<EnhancedLightEffectsController>();
        _progressionSystem = GetComponent<DualProgressionSystem>();
        _audioSource = GetComponent<AudioSource>();

        SetupVisualComponents();
    }

    private void Start()
    {
        _floatingLantern = FindObjectOfType<FloatingLantern>();

        if (_debugMode)
        {
            Debug.Log("✓ Prism Beam Ability initialized");
            Debug.Log($"  Max Range: {_maxRange}");
            Debug.Log($"  Max Reflections: {_maxReflections}");
            Debug.Log($"  Aim Assist: {_enableAimAssist}");
        }
    }

    private void SetupVisualComponents()
    {
        // Setup main beam renderer
        if (_beamRenderer == null)
        {
            GameObject beamObj = new GameObject("PrismBeam");
            beamObj.transform.SetParent(transform);
            _beamRenderer = beamObj.AddComponent<LineRenderer>();
        }

        _beamRenderer.material = _beamMaterial ?? new Material(Shader.Find("Sprites/Default"));
        _beamRenderer.startWidth = _beamWidth;
        _beamRenderer.endWidth = _beamWidth * 0.5f;
        _beamRenderer.useWorldSpace = true;
        _beamRenderer.enabled = false;

        // Setup beam light
        if (_beamLight == null)
        {
            GameObject lightObj = new GameObject("PrismBeamLight");
            lightObj.transform.SetParent(transform);
            _beamLight = lightObj.AddComponent<Light2D>();
        }

        _beamLight.lightType = Light2D.LightType.Point;
        _beamLight.intensity = 0f;
        _beamLight.pointLightInnerRadius = 0.1f;
        _beamLight.pointLightOuterRadius = 2f;
        _beamLight.color = new Color(0.8f, 0.9f, 1f);

        // Setup focus particles
        if (_focusParticles == null)
        {
            _focusParticles = CreateBeamParticleSystem("FocusParticles", new Color(0.8f, 0.9f, 1f));
        }

        // Setup intensity gradient
        if (_intensityGradient == null)
        {
            _intensityGradient = new Gradient();
            var colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.5f, 0.7f, 1f), 0f),
                new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0.5f),
                new GradientColorKey(Color.white, 1f)
            };
            var alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.3f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(1f, 1f)
            };
            _intensityGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    private ParticleSystem CreateBeamParticleSystem(string name, Color color)
    {
        GameObject particleObj = new GameObject(name);
        particleObj.transform.SetParent(transform);

        var particles = particleObj.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 3f;
        main.startSize = 0.05f;
        main.startColor = color;
        main.maxParticles = 50;

        var emission = particles.emission;
        emission.rateOverTime = 30f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        particles.Stop();
        return particles;
    }

    private void Update()
    {
        HandleInput();
        UpdateBeamState();
        UpdateVisuals();
    }

    #region Input and State Management

    private void HandleInput()
    {
        // Check if ability is available
        if (!CanActivate()) return;

        // Start charging when E is pressed
        if (Input.GetKeyDown(KeyCode.E) && !IsCharging && !IsChanneling)
        {
            StartCharging();
        }

        // Release beam when E is released or hold limit reached
        if ((Input.GetKeyUp(KeyCode.E) || ChannelTimeRemaining <= 0f) && (IsCharging || IsChanneling))
        {
            StopBeam();
        }

        // Update aim direction while charging/channeling
        if (IsCharging || IsChanneling)
        {
            UpdateAimDirection();
        }
    }

    public bool CanActivate()
    {
        if (_lanternController == null || !_lanternController.HasLantern) return false;
        if (_progressionSystem == null) return false;

        // Check mana cost
        float manaCost = _manaCostPerSecond * Time.deltaTime;
        return _lanternController.ManaPercentage * _lanternController.MaxMana >= manaCost;
    }

    private void StartCharging()
    {
        IsCharging = true;
        FocusProgress = 0f;
        ChannelTimeRemaining = _maxChannelTime;
        CurrentIntensity = 0f;

        // Update beam origin
        _beamOrigin = _floatingLantern?.GetBeamOrigin() ?? transform.position;

        // Start focus effects
        if (_focusParticles != null)
            _focusParticles.Play();

        if (_chargeSound != null && _audioSource != null)
        {
            _audioSource.clip = _chargeSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        OnBeamStarted?.Invoke();

        if (_debugMode)
            Debug.Log("🔷 Prism Beam charging started");
    }

    private void StopBeam()
    {
        IsCharging = false;
        IsChanneling = false;
        FocusProgress = 0f;
        CurrentIntensity = 0f;

        // Clear beam visualization
        _beamRenderer.enabled = false;
        _beamLight.intensity = 0f;

        // Stop effects
        if (_focusParticles != null && _focusParticles.isPlaying)
            _focusParticles.Stop();

        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        // Clear targets
        ClearCurrentTargets();
        ClearReflectionRenderers();

        OnBeamStopped?.Invoke();

        if (_debugMode)
            Debug.Log("🔷 Prism Beam stopped");
    }

    private void UpdateBeamState()
    {
        if (!IsCharging && !IsChanneling) return;

        // Update charging progress
        if (IsCharging)
        {
            FocusProgress += Time.deltaTime / _focusTime;

            if (FocusProgress >= 1f)
            {
                FocusProgress = 1f;
                IsCharging = false;
                IsChanneling = true;

                // Switch to sustain sound
                if (_beamSustainSound != null && _audioSource != null)
                {
                    _audioSource.clip = _beamSustainSound;
                    _audioSource.Play();
                }

                if (_debugMode)
                    Debug.Log("🔷 Prism Beam fully focused - now channeling");
            }
        }

        // Update channeling
        if (IsChanneling)
        {
            ChannelTimeRemaining -= Time.deltaTime;

            // Consume mana
            float manaCost = _manaCostPerSecond * Time.deltaTime;
            if (_lanternController != null)
            {
                _lanternController.ConsumeMana(manaCost);

                // Stop if out of mana
                if (_lanternController.ManaPercentage <= 0.01f)
                {
                    StopBeam();
                    return;
                }
            }

            // Update beam intensity
            CurrentIntensity = FocusProgress;

            // Perform beam casting
            PerformBeamCasting();

            // Overload warning
            if (ChannelTimeRemaining <= 2f && ChannelTimeRemaining > 1.8f)
            {
                OnOverloadWarning?.Invoke();

                if (_overloadWarningSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_overloadWarningSound);
                }
            }
        }

        // Update intensity based on focus
        CurrentIntensity = FocusProgress;
    }

    private void UpdateAimDirection()
    {
        if (_floatingLantern == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        _beamOrigin = _floatingLantern.GetBeamOrigin();
        _aimDirection = (mousePos - _beamOrigin).normalized;

        // Apply aim assist
        if (_enableAimAssist)
        {
            _aimDirection = ApplyAimAssist(_aimDirection);
        }
    }

    private Vector2 ApplyAimAssist(Vector2 originalDirection)
    {
        // Find nearby prism reflectors and targets
        var nearbyObjects = Physics2D.OverlapCircleAll(_beamOrigin, _aimAssistRange * 5f, _reflectiveLayers | _targetLayers);

        Vector2 bestDirection = originalDirection;
        float bestScore = 0f;

        foreach (var obj in nearbyObjects)
        {
            Vector2 directionToObject = (obj.transform.position - _beamOrigin).normalized;
            float angle = Vector2.Angle(originalDirection, directionToObject);

            if (angle <= 15f) // Within aim assist cone
            {
                float score = 1f - (angle / 15f); // Closer to aim = higher score

                // Boost score for targets vs reflectors
                if (LayerMaskHelper.ObjIsInLayerMask(obj.gameObject, _targetLayers))
                {
                    score *= 1.5f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = directionToObject;
                }
            }
        }

        return bestDirection;
    }

    #endregion

    #region Beam Casting and Reflection

    private void PerformBeamCasting()
    {
        ClearBeamPath();
        ClearCurrentTargets();
        CurrentReflectionCount = 0;

        Vector3 currentOrigin = _beamOrigin;
        Vector2 currentDirection = _aimDirection;
        float currentIntensity = CurrentIntensity;
        float remainingRange = _maxRange;

        _beamPath.Add(currentOrigin);

        // Trace beam through reflections
        for (int reflection = 0; reflection <= _maxReflections && remainingRange > 0 && currentIntensity > 0.1f; reflection++)
        {
            var hit = Physics2D.Raycast(currentOrigin, currentDirection, remainingRange, _reflectiveLayers | _targetLayers);

            Vector3 hitPoint = hit.collider != null ? hit.point : currentOrigin + (Vector3)(currentDirection * remainingRange);
            _beamPath.Add(hitPoint);

            if (hit.collider == null)
            {
                // Beam ends in empty space
                break;
            }

            // Process hit object
            ProcessBeamHit(hit, currentIntensity);

            // Check if this is a reflective surface
            if (LayerMaskHelper.ObjIsInLayerMask(hit.collider.gameObject, _reflectiveLayers))
            {
                var reflector = hit.collider.GetComponent<PrismReflector>();
                if (reflector != null && reflector.CanReflect(currentDirection, currentIntensity))
                {
                    // Calculate reflection
                    Vector2 reflectedDirection = reflector.GetReflectionDirection(currentDirection, hit.normal);

                    // Create reflection point
                    var reflectionPoint = new ReflectionPoint
                    {
                        position = hit.point,
                        incomingDirection = currentDirection,
                        reflectedDirection = reflectedDirection,
                        intensity = currentIntensity,
                        reflector = reflector,
                        hitTime = Time.time
                    };

                    _reflectionPoints.Add(reflectionPoint);
                    CurrentReflectionCount++;

                    // Setup for next segment
                    currentOrigin = hit.point + (Vector2)(hit.normal * 0.01f); // Offset slightly
                    currentDirection = reflectedDirection;
                    currentIntensity *= (1f - _reflectionIntensityLoss);
                    remainingRange -= hit.distance;

                    // Play reflection effects
                    PlayReflectionEffects(hit.point);

                    if (_debugMode)
                        Debug.Log($"🔷 Beam reflected by {reflector.name} (intensity now {currentIntensity:F2})");
                }
                else
                {
                    // Hit reflector but can't reflect (wrong angle, overloaded, etc.)
                    break;
                }
            }
            else
            {
                // Hit non-reflective object, beam stops
                break;
            }
        }

        // Update beam visualization
        UpdateBeamVisualization();

        // Fire reflection count changed event
        OnReflectionsChanged?.Invoke(CurrentReflectionCount);
    }

    private void ProcessBeamHit(RaycastHit2D hit, float intensity)
    {
        // Check for prism beam targets
        var target = hit.collider.GetComponent<IPrismBeamTarget>();
        if (target != null)
        {
            if (!_currentTargets.Contains(target))
            {
                _currentTargets.Add(target);
                target.OnBeamHit(this, intensity, hit.point);
                OnTargetHit?.Invoke(target);

                if (_debugMode)
                    Debug.Log($"🎯 Prism Beam hit target: {hit.collider.name} (intensity: {intensity:F2})");
            }
            else
            {
                target.OnBeamStay(this, intensity, Time.deltaTime);
            }
        }

        // Check for light interactables
        var lightInteractable = hit.collider.GetComponent<ILightInteractable>();
        if (lightInteractable != null && lightInteractable.RespondsToEffect(LightEffect.Refract))
        {
            Vector2 direction = (hit.point - (Vector2)_beamOrigin).normalized;
            lightInteractable.OnLightEnter(LightEffect.Refract, intensity, direction);
        }

        // Play target hit effects
        if (_targetHitSound != null && _audioSource != null && target != null)
        {
            _audioSource.PlayOneShot(_targetHitSound, 0.3f);
        }
    }

    private void PlayReflectionEffects(Vector3 position)
    {
        // Play reflection particles
        if (_reflectionParticles != null)
        {
            _reflectionParticles.transform.position = position;
            _reflectionParticles.Play();
        }

        // Play reflection sound
        if (_reflectionSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_reflectionSound, 0.5f);
        }
    }

    #endregion

    #region Visualization

    private void UpdateVisuals()
    {
        // Update beam light position
        if (_beamLight != null && _beamPath.Count > 0)
        {
            _beamLight.transform.position = _beamPath[0];
            _beamLight.intensity = CurrentIntensity * 0.5f;
        }

        // Update focus particles
        if (_focusParticles != null)
        {
            var emission = _focusParticles.emission;
            emission.rateOverTime = IsCharging ? 30f * FocusProgress : (IsChanneling ? 50f : 0f);
        }
    }

    private void UpdateBeamVisualization()
    {
        if (_beamPath.Count < 2)
        {
            _beamRenderer.enabled = false;
            return;
        }

        _beamRenderer.enabled = true;
        _beamRenderer.positionCount = _beamPath.Count;

        for (int i = 0; i < _beamPath.Count; i++)
        {
            _beamRenderer.SetPosition(i, _beamPath[i]);
        }

        // Update beam color based on intensity
        Color beamColor = _intensityGradient.Evaluate(CurrentIntensity);
        _beamRenderer.startColor = beamColor;
        _beamRenderer.endColor = beamColor * 0.5f;

        // Update beam width based on intensity
        float widthMultiplier = 0.5f + (CurrentIntensity * 0.5f);
        _beamRenderer.startWidth = _beamWidth * widthMultiplier;
        _beamRenderer.endWidth = _beamWidth * widthMultiplier * 0.5f;
    }

    private void ClearBeamPath()
    {
        _beamPath.Clear();
        _reflectionPoints.Clear();
    }

    private void ClearCurrentTargets()
    {
        foreach (var target in _currentTargets)
        {
            if (target != null)
            {
                target.OnBeamExit(this);
            }
        }
        _currentTargets.Clear();
    }

    private void ClearReflectionRenderers()
    {
        foreach (var renderer in _reflectionRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
    }

    #endregion

    #region Public API

    public List<Vector3> GetBeamPath() => new List<Vector3>(_beamPath);
    public List<ReflectionPoint> GetReflectionPoints() => new List<ReflectionPoint>(_reflectionPoints);
    public List<IPrismBeamTarget> GetCurrentTargets() => new List<IPrismBeamTarget>(_currentTargets);

    public void ForceStopBeam()
    {
        StopBeam();
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Prism Beam")]
    public void TestPrismBeam()
    {
        if (Application.isPlaying && CanActivate())
        {
            StartCharging();
        }
    }

    [ContextMenu("Show Beam Info")]
    public void ShowBeamInfo()
    {
        Debug.Log($"=== PRISM BEAM INFO ===");
        Debug.Log($"Is Charging: {IsCharging}");
        Debug.Log($"Is Channeling: {IsChanneling}");
        Debug.Log($"Focus Progress: {FocusProgress:P}");
        Debug.Log($"Current Intensity: {CurrentIntensity:F2}");
        Debug.Log($"Reflections: {CurrentReflectionCount}");
        Debug.Log($"Targets Hit: {_currentTargets.Count}");
        Debug.Log($"Beam Path Points: {_beamPath.Count}");
        Debug.Log($"Channel Time Remaining: {ChannelTimeRemaining:F1}s");
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (!_showReflectionPaths || !Application.isPlaying) return;

        // Draw beam path
        if (_beamPath.Count > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _beamPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_beamPath[i], _beamPath[i + 1]);
            }
        }

        // Draw reflection points
        Gizmos.color = Color.yellow;
        foreach (var reflection in _reflectionPoints)
        {
            Gizmos.DrawWireSphere(reflection.position, 0.2f);
            Gizmos.DrawRay(reflection.position, reflection.reflectedDirection * 1f);
        }

        // Draw aim assist range
        if (_showAimAssist && _enableAimAssist)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawSphere(transform.position, _aimAssistRange);
        }
    }
}

/// <summary>
/// Interface for objects that can be targeted by Prism Beam
/// </summary>
public interface IPrismBeamTarget
{
    void OnBeamHit(PrismBeamAbility beam, float intensity, Vector3 hitPoint);
    void OnBeamStay(PrismBeamAbility beam, float intensity, float deltaTime);
    void OnBeamExit(PrismBeamAbility beam);
    bool CanBeTargeted(PrismBeamAbility beam);
    float GetRequiredIntensity();
}

/// <summary>
/// Prism reflector that can bounce Prism Beams
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PrismReflector : MonoBehaviour
{
    [Header("Reflection Properties")]
    [SerializeField] private float _reflectionAngle = 45f; // Degrees
    [SerializeField] private float _minimumIntensity = 0.2f;
    [SerializeField] private float _maxIntensity = 2f;
    [SerializeField] private bool _canOverload = true;
    [SerializeField] private float _overloadTime = 3f;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _prismRenderer;
    [SerializeField] private Light2D _prismLight;
    [SerializeField] private ParticleSystem _reflectionParticles;

    [Header("Audio")]
    [SerializeField] private AudioClip _reflectionSound;
    [SerializeField] private AudioClip _overloadSound;

    private float _currentHeatLevel = 0f;
    private bool _isOverloaded = false;
    private AudioSource _audioSource;

    // Visual states
    private Color _normalColor = new Color(0.8f, 0.9f, 1f);
    private Color _activeColor = Color.cyan;
    private Color _overloadColor = Color.red;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // Ensure collider is on reflective layer
        if (!LayerMaskHelper.ObjIsInLayerMask(gameObject, 1 << 9))
        {
            gameObject.layer = 9; // Reflective layer
        }

        SetupVisuals();
    }

    private void SetupVisuals()
    {
        if (_prismRenderer == null)
            _prismRenderer = GetComponent<SpriteRenderer>();

        if (_prismRenderer != null && _prismRenderer.sprite == null)
        {
            _prismRenderer.sprite = CreatePrismSprite();
        }

        if (_prismLight == null)
        {
            GameObject lightObj = new GameObject("PrismLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _prismLight = lightObj.AddComponent<Light2D>();
        }

        _prismLight.lightType = Light2D.LightType.Point;
        _prismLight.intensity = 0.3f;
        _prismLight.pointLightInnerRadius = 0.1f;
        _prismLight.pointLightOuterRadius = 1.5f;
        _prismLight.color = _normalColor;
    }

    private Sprite CreatePrismSprite()
    {
        // Create a triangular prism sprite
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);

                // Create triangular prism shape
                bool isInsidePrism = IsInsideTriangle(pos, center, size * 0.3f);

                if (isInsidePrism)
                {
                    float distance = Vector2.Distance(pos, center);
                    float alpha = 1f - (distance / (size * 0.3f)) * 0.3f;
                    pixels[y * size + x] = new Color(0.8f, 0.9f, 1f, alpha);
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

    private bool IsInsideTriangle(Vector2 point, Vector2 center, float radius)
    {
        // Simple triangular check
        Vector2 offset = point - center;
        return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) <= radius;
    }

    private void Update()
    {
        UpdateHeatLevel();
        UpdateVisuals();
    }

    private void UpdateHeatLevel()
    {
        if (_currentHeatLevel > 0f)
        {
            _currentHeatLevel -= Time.deltaTime;

            if (_currentHeatLevel <= 0f)
            {
                _currentHeatLevel = 0f;
                if (_isOverloaded)
                {
                    _isOverloaded = false;
                    Debug.Log($"🔷 Prism {name} cooled down");
                }
            }
        }
    }

    private void UpdateVisuals()
    {
        Color targetColor;

        if (_isOverloaded)
        {
            targetColor = _overloadColor;
        }
        else if (_currentHeatLevel > 0f)
        {
            float heatRatio = _currentHeatLevel / _overloadTime;
            targetColor = Color.Lerp(_normalColor, _activeColor, heatRatio);
        }
        else
        {
            targetColor = _normalColor;
        }

        if (_prismRenderer != null)
            _prismRenderer.color = targetColor;

        if (_prismLight != null)
        {
            _prismLight.color = targetColor;
            _prismLight.intensity = 0.3f + (_currentHeatLevel / _overloadTime) * 0.7f;
        }
    }

    public bool CanReflect(Vector2 incomingDirection, float intensity)
    {
        if (_isOverloaded) return false;
        if (intensity < _minimumIntensity) return false;
        if (intensity > _maxIntensity) return false;

        return true;
    }

    public Vector2 GetReflectionDirection(Vector2 incomingDirection, Vector2 surfaceNormal)
    {
        // Calculate perfect reflection
        Vector2 reflectedDirection = Vector2.Reflect(incomingDirection, surfaceNormal);

        // Apply prism angle modification
        float angleAdjustment = _reflectionAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleAdjustment);
        float sin = Mathf.Sin(angleAdjustment);

        Vector2 adjustedDirection = new Vector2(
            reflectedDirection.x * cos - reflectedDirection.y * sin,
            reflectedDirection.x * sin + reflectedDirection.y * cos
        );

        return adjustedDirection.normalized;
    }

    public void OnBeamHit(float intensity)
    {
        _currentHeatLevel += intensity * Time.deltaTime;

        // Check for overload
        if (_canOverload && _currentHeatLevel >= _overloadTime && !_isOverloaded)
        {
            _isOverloaded = true;

            if (_overloadSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_overloadSound);
            }

            Debug.Log($"🔥 Prism {name} overloaded!");
        }

        // Play reflection effects
        if (_reflectionParticles != null && !_reflectionParticles.isPlaying)
        {
            _reflectionParticles.Play();
        }

        if (_reflectionSound != null && _audioSource != null && Time.time % 0.5f < 0.1f)
        {
            _audioSource.PlayOneShot(_reflectionSound, 0.3f);
        }
    }

    public float GetHeatLevel() => _currentHeatLevel / _overloadTime;
    public bool IsOverloaded => _isOverloaded;

    private void OnDrawGizmosSelected()
    {
        // Draw reflection angle
        Gizmos.color = Color.cyan;

        float angleRad = _reflectionAngle * Mathf.Deg2Rad;
        Vector3 direction1 = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
        Vector3 direction2 = new Vector3(Mathf.Cos(-angleRad), Mathf.Sin(-angleRad), 0f);

        Gizmos.DrawRay(transform.position, direction1 * 2f);
        Gizmos.DrawRay(transform.position, direction2 * 2f);

        // Draw heat level
        if (Application.isPlaying && _currentHeatLevel > 0f)
        {
            Gizmos.color = Color.Lerp(Color.blue, Color.red, GetHeatLevel());
            Gizmos.DrawWireSphere(transform.position, GetHeatLevel() * 0.5f + 0.1f);
        }
    }
}