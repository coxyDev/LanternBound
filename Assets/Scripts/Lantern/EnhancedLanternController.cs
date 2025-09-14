using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Updated EnhancedLanternController that works with FloatingLantern instead of player-attached lights
/// </summary>
public class EnhancedLanternController : MonoBehaviour
{
    [Header("Player Inner Light Properties - RUNTIME ADJUSTABLE")]
    [SerializeField] private float _innerLightIntensity = 1.2f;
    [SerializeField] private float _innerLightInnerRadius = 0.2f;
    [SerializeField] private float _innerLightOuterRadius = 2f;
    [SerializeField] private Color _innerLightColor = new Color(1f, 0.8f, 0.6f);

    [Header("Light Type System")]
    [SerializeField] private LightType _currentLightType = LightType.None;
    [SerializeField] private List<LightType> _discoveredLightTypes = new List<LightType>();
    [SerializeField] private LightTypeData[] _lightTypeConfigurations;

    [Header("Inner Light Progression")]
    [SerializeField] private float _innerLightStrength = 0.1f;
    [SerializeField] private float _maxInnerLightStrength = 5.0f;

    [Header("Mana System")]
    [SerializeField] private float _currentMana = 100f;
    [SerializeField] private float _maxMana = 100f;
    [SerializeField] private float _manaRegenRate = 10f;
    [SerializeField] private bool _allowManaRegen = true;

    [Header("Beam Detection")]
    [SerializeField] private LayerMask _lightInteractionLayers = -1;

    [Header("Character Glow")]
    [SerializeField] private Light _characterAmbientLight;
    [SerializeField] private ParticleSystem _innerLightParticles;
    [SerializeField] private SpriteRenderer _characterGlow;

    [Header("Input")]
    [SerializeField] private bool _useMouseAiming = true;
    [SerializeField] private bool _useControllerAiming = true;

    [Header("Audio & Effects")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private ParticleSystem _lightEmissionEffect;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    [Header("Light Detection Debug")]
    [SerializeField] private bool _showDebugRays = true;
    [SerializeField] private bool _verboseLightDetection = true;

    // References to external systems
    private Light2D _playerInnerLight2D;
    private FloatingLantern _floatingLantern;

    // Public Properties
    public bool IsLanternActive { get; private set; }
    public bool HasLantern { get; private set; }
    public LightType CurrentLightType => _currentLightType;
    public float ManaPercentage => _maxMana > 0 ? _currentMana / _maxMana : 0f;
    public float MaxMana => _maxMana;
    public float InnerLightPercentage => _maxInnerLightStrength > 0 ? _innerLightStrength / _maxInnerLightStrength : 0f;

    // Events
    public System.Action<LightType> OnLightTypeChanged;
    public System.Action<float> OnManaChanged;
    public System.Action<float> OnInnerLightChanged;
    public System.Action<ILightInteractable> OnObjectIlluminated;
    public System.Action<ILightInteractable> OnObjectLeftLight;
    public System.Action OnLanternAcquired;
    public System.Action<LightType> OnLightTypeDiscovered;

    // Current illuminated objects
    private List<ILightInteractable> _currentlyIlluminated = new List<ILightInteractable>();
    private List<ILightInteractable> _previouslyIlluminated = new List<ILightInteractable>();

    // Light type definitions
    public enum LightType
    {
        None,           // No lantern
        Ember,          // First light - weak, basic
        Radiance,       // Standard bright light
        SolarFlare,     // Intense, damages enemies
        MoonBeam,       // Reveals hidden secrets
        Starlight,      // Pierces through darkness barriers
        PrismaticLight, // Multi-colored, affects different elements
        VoidLight       // Advanced - reveals AND damages
    }

    [System.Serializable]
    public class LightTypeData
    {
        public LightType lightType;
        public string displayName;
        [TextArea(2, 3)]
        public string description;

        [Header("Beam Properties")]
        public float baseRange = 10f;
        public float baseWidth = 30f; // Degrees
        public Color lightColor = Color.white;
        public float baseIntensity = 1f;

        [Header("Mana Costs")]
        public float activationCost = 5f;
        public float sustainCost = 2f;
        public bool requiresContinuousMana = true;

        [Header("Special Properties")]
        public bool canRevealHidden = true;
        public bool canRepelEnemies = true;
        public bool canActivateShrines = true;
        public bool hasPiercing = false;
        public float baseDamage = 0f;

        [Header("Audio")]
        public AudioClip activationSound;
        public AudioClip sustainSound;
        public AudioClip deactivationSound;
    }

    private void Awake()
    {
        InitializeLanternSystem();
        SetupPlayerInnerLight();
    }

    private void Update()
    {
        HandleInput();
        UpdateManaSystem();
        UpdateLightProperties();

        if (IsLanternActive && _floatingLantern != null)
        {
            UpdateLanternBeam();
        }

        UpdateInnerLightEffects();
    }

    #region Initialization

    private void SetupPlayerInnerLight()
    {
        // Setup ONLY the player's inner light (not the lantern light)
        if (_playerInnerLight2D == null)
        {
            GameObject innerLightObj = new GameObject("PlayerInnerLight2D");
            innerLightObj.transform.SetParent(transform);
            innerLightObj.transform.localPosition = Vector3.zero;
            _playerInnerLight2D = innerLightObj.AddComponent<Light2D>();
        }

        _playerInnerLight2D.lightType = Light2D.LightType.Point;
        _playerInnerLight2D.intensity = _innerLightIntensity;
        _playerInnerLight2D.pointLightInnerRadius = _innerLightInnerRadius;
        _playerInnerLight2D.pointLightOuterRadius = _innerLightOuterRadius;
        _playerInnerLight2D.color = _innerLightColor;
        _playerInnerLight2D.enabled = true; // Always on for inner light

        if (_debugMode)
        {
            Debug.Log("✓ Player inner light setup complete");
            Debug.Log($"  Inner Light: {_innerLightIntensity} intensity, {_innerLightOuterRadius} radius");
        }
    }

    private void InitializeLanternSystem()
    {
        // Setup initial state
        IsLanternActive = false;
        HasLantern = false;

        // Setup audio
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        // Setup default light configurations if empty
        if (_lightTypeConfigurations == null || _lightTypeConfigurations.Length == 0)
        {
            CreateDefaultLightConfigurations();
        }

        // Update effects
        UpdateInnerLightEffects();

        Debug.Log("✓ Lantern system initialized - waiting for acquisition");
    }

    private void CreateDefaultLightConfigurations()
    {
        _lightTypeConfigurations = new LightTypeData[]
        {
            new LightTypeData
            {
                lightType = LightType.Ember,
                displayName = "Ember Light",
                description = "A weak but steady flame",
                baseRange = 8f,
                baseWidth = 25f,
                lightColor = new Color(1f, 0.7f, 0.3f),
                baseIntensity = 0.8f,
                activationCost = 3f,
                sustainCost = 1f,
                requiresContinuousMana = true
            },
            new LightTypeData
            {
                lightType = LightType.Radiance,
                displayName = "Radiant Light",
                description = "Pure, bright illumination",
                baseRange = 12f,
                baseWidth = 35f,
                lightColor = Color.white,
                baseIntensity = 1.2f,
                activationCost = 5f,
                sustainCost = 2f,
                requiresContinuousMana = true
            }
        };
    }

    #endregion

    #region Runtime Light Property Updates

    private void UpdateLightProperties()
    {
        // Update inner light properties in real-time
        if (_playerInnerLight2D != null)
        {
            _playerInnerLight2D.intensity = _innerLightIntensity;
            _playerInnerLight2D.pointLightInnerRadius = _innerLightInnerRadius;
            _playerInnerLight2D.pointLightOuterRadius = _innerLightOuterRadius;
            _playerInnerLight2D.color = _innerLightColor;
        }
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        if (!HasLantern)
        {
            if (_debugMode && InputManager.LanternTogglePressed)
            {
                Debug.Log("❌ Lantern toggle pressed but player doesn't have lantern yet!");
            }
            return;
        }

        // Lantern toggle
        if (InputManager.LanternTogglePressed)
        {
            if (_debugMode)
            {
                Debug.Log($"🔦 Lantern toggle - Currently Active: {IsLanternActive}");
            }

            if (IsLanternActive)
                DeactivateLantern();
            else
                ActivateLantern();
        }

        // Cycle light types (mouse wheel)
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f && _discoveredLightTypes.Count > 1)
        {
            CycleLightType(scrollInput > 0f);
        }

        // Number key shortcuts for light types
        for (int i = 1; i <= 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                int lightIndex = i - 1;
                if (lightIndex < _discoveredLightTypes.Count)
                {
                    SwitchToLightType(_discoveredLightTypes[lightIndex]);
                }
            }
        }
    }

    #endregion

    #region Lantern Acquisition and Light Discovery

    /// <summary>
    /// Call when player first finds the lantern
    /// </summary>
    public void AcquireLantern()
    {
        if (HasLantern)
        {
            if (_debugMode)
                Debug.LogWarning("⚠️ Player already has lantern!");
            return;
        }

        // Find the floating lantern that was created for this player
        if (_floatingLantern == null)
        {
            _floatingLantern = FindObjectOfType<FloatingLantern>();

            if (_debugMode)
            {
                Debug.Log($"FloatingLantern search result: {(_floatingLantern != null ? "Found" : "Not Found")}");
            }
        }

        HasLantern = true;

        // Start with Ember light
        DiscoverLightType(LightType.Ember, true);

        // Initial inner light boost
        IncreaseInnerLight(0.5f);

        OnLanternAcquired?.Invoke();

        if (_debugMode)
        {
            Debug.Log("✨ LANTERN ACQUIRED SUCCESSFULLY!");
            Debug.Log($"  Has Lantern: {HasLantern}");
            Debug.Log($"  Current Light Type: {_currentLightType}");
            Debug.Log($"  Discovered Light Types: {_discoveredLightTypes.Count}");
            Debug.Log($"  FloatingLantern Reference: {(_floatingLantern != null ? "Connected" : "Missing")}");
        }
    }

    /// <summary>
    /// Discover a new light type
    /// </summary>
    public void DiscoverLightType(LightType newLightType, bool autoSwitch = false)
    {
        if (_discoveredLightTypes.Contains(newLightType)) return;

        var lightData = GetLightTypeData(newLightType);
        if (lightData == null) return;

        _discoveredLightTypes.Add(newLightType);

        if (autoSwitch || _currentLightType == LightType.None)
        {
            SwitchToLightType(newLightType);
        }

        // Increase inner light
        IncreaseInnerLight(0.3f);

        // Play discovery effects
        PlayLightDiscoveryEffect(newLightType);

        OnLightTypeDiscovered?.Invoke(newLightType);

        if (_debugMode)
        {
            Debug.Log($"🌟 New light discovered: {lightData.displayName}!");
        }
    }

    #endregion

    #region Light Type Management

    public void SwitchToLightType(LightType lightType)
    {
        if (!_discoveredLightTypes.Contains(lightType)) return;

        _currentLightType = lightType;
        OnLightTypeChanged?.Invoke(lightType);

        var lightData = GetLightTypeData(lightType);
        if (_debugMode)
        {
            Debug.Log($"Switched to {lightData?.displayName ?? lightType.ToString()}");
        }
    }

    private void CycleLightType(bool forward = true)
    {
        if (_discoveredLightTypes.Count <= 1) return;

        int currentIndex = _discoveredLightTypes.IndexOf(_currentLightType);
        int nextIndex;

        if (forward)
            nextIndex = (currentIndex + 1) % _discoveredLightTypes.Count;
        else
            nextIndex = (currentIndex - 1 + _discoveredLightTypes.Count) % _discoveredLightTypes.Count;

        SwitchToLightType(_discoveredLightTypes[nextIndex]);
    }

    private LightTypeData GetLightTypeData(LightType lightType)
    {
        foreach (var data in _lightTypeConfigurations)
        {
            if (data.lightType == lightType)
                return data;
        }
        return null;
    }

    #endregion

    #region Inner Light and Progression

    private void IncreaseInnerLight(float amount)
    {
        float oldStrength = _innerLightStrength;
        _innerLightStrength = Mathf.Min(_innerLightStrength + amount, _maxInnerLightStrength);

        OnInnerLightChanged?.Invoke(InnerLightPercentage);

        // Increase max mana based on inner light growth
        float manaIncrease = (amount / _maxInnerLightStrength) * 50f;
        _maxMana += manaIncrease;
        _currentMana = _maxMana; // Restore to full

        if (_debugMode)
        {
            Debug.Log($"Inner Light increased: {oldStrength:F2} → {_innerLightStrength:F2}");
        }
    }

    #endregion

    #region Lantern Activation

    public void ActivateLantern()
    {
        if (!HasLantern)
        {
            if (_debugMode)
                Debug.Log("❌ Cannot activate lantern - not acquired yet!");
            return;
        }

        if (IsLanternActive)
        {
            if (_debugMode)
                Debug.Log("⚠️ Lantern already active!");
            return;
        }

        if (_currentLightType == LightType.None)
        {
            if (_debugMode)
                Debug.Log("❌ Cannot activate lantern - no light type selected!");
            return;
        }

        var lightData = GetLightTypeData(_currentLightType);
        if (lightData == null)
        {
            if (_debugMode)
                Debug.Log("❌ Cannot activate lantern - invalid light type data!");
            return;
        }

        // Check mana cost
        float activationCost = lightData.activationCost;
        if (_currentMana < activationCost)
        {
            Debug.Log("❌ Not enough mana to activate lantern!");
            return;
        }

        // Activate floating lantern
        if (_floatingLantern != null)
        {
            _floatingLantern.SetLanternActive(true);
        }
        else
        {
            Debug.LogWarning("⚠️ No FloatingLantern found!");
        }

        // Consume mana
        ConsumeMana(activationCost);

        IsLanternActive = true;
        PlayLightActivationEffect(lightData);

        if (_debugMode)
        {
            Debug.Log($"✅ LANTERN ACTIVATED: {lightData.displayName}");
            Debug.Log($"  FloatingLantern Active: {_floatingLantern != null}");
            Debug.Log($"  Mana Remaining: {_currentMana:F1}/{_maxMana:F1}");
        }
    }

    public void DeactivateLantern()
    {
        if (!IsLanternActive)
        {
            if (_debugMode)
                Debug.Log("⚠️ Lantern already inactive!");
            return;
        }

        IsLanternActive = false;
        ClearIlluminatedObjects();

        // Deactivate floating lantern
        if (_floatingLantern != null)
        {
            _floatingLantern.SetLanternActive(false);
        }

        var lightData = GetLightTypeData(_currentLightType);
        if (lightData?.deactivationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(lightData.deactivationSound);
        }

        if (_debugMode)
        {
            Debug.Log("🔦 Lantern deactivated");
        }
    }

    #endregion

    #region Mana System

    private void UpdateManaSystem()
    {
        if (!HasLantern) return;

        // Consume mana if lantern is active
        if (IsLanternActive)
        {
            var lightData = GetLightTypeData(_currentLightType);
            if (lightData != null && lightData.requiresContinuousMana)
            {
                float sustainCost = lightData.sustainCost * Time.deltaTime;

                if (_currentMana >= sustainCost)
                {
                    ConsumeMana(sustainCost);
                }
                else
                {
                    DeactivateLantern();
                    Debug.Log("🔦 Lantern deactivated - out of mana!");
                }
            }
        }

        // Regenerate mana
        if (_allowManaRegen && _currentMana < _maxMana)
        {
            _currentMana = Mathf.Min(_currentMana + _manaRegenRate * Time.deltaTime, _maxMana);
            OnManaChanged?.Invoke(ManaPercentage);
        }
    }

    public void ConsumeMana(float amount)
    {
        _currentMana = Mathf.Max(_currentMana - amount, 0f);
        OnManaChanged?.Invoke(ManaPercentage);
    }

    public void RestoreMana(float amount)
    {
        _currentMana = Mathf.Min(_currentMana + amount, _maxMana);
        OnManaChanged?.Invoke(ManaPercentage);
    }

    #endregion

    #region Beam System

    private void UpdateLanternBeam()
    {
        if (_floatingLantern == null)
        {
            if (_debugMode)
                Debug.LogWarning("⚠️ FloatingLantern is null - cannot update beam");
            return;
        }

        Vector2 beamDirection = GetBeamDirection();
        var lightData = GetLightTypeData(_currentLightType);
        if (lightData == null)
        {
            if (_debugMode)
                Debug.LogWarning("⚠️ Light data is null - cannot update beam");
            return;
        }

        // Update floating lantern beam visuals
        _floatingLantern.UpdateBeam(beamDirection, lightData.baseRange, lightData.lightColor);

        // Perform enhanced light detection
        PerformLightDetection(beamDirection, lightData);
    }

    private Vector2 GetBeamDirection()
    {
        if (_useMouseAiming && Input.mousePosition != Vector3.zero)
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            Vector3 beamOrigin = _floatingLantern != null ? _floatingLantern.GetBeamOrigin() : transform.position;
            Vector2 direction = ((Vector2)mouseWorldPos - (Vector2)beamOrigin);

            if (direction.magnitude > 0.1f)
                return direction.normalized;
        }

        // Controller aiming
        if (_useControllerAiming && InputManager.RightStickInput.magnitude > 0.3f)
        {
            return InputManager.RightStickInput.normalized;
        }

        // Default to right
        return Vector2.right;
    }

    private void PerformLightDetection(Vector2 beamDirection, LightTypeData lightData)
    {
        // Clear previous illumination
        _previouslyIlluminated.Clear();
        _previouslyIlluminated.AddRange(_currentlyIlluminated);
        _currentlyIlluminated.Clear();

        Vector3 beamOrigin = _floatingLantern.GetBeamOrigin();
        float range = lightData.baseRange;
        float width = lightData.baseWidth;

        if (_verboseLightDetection)
        {
            Debug.Log($"🔍 LIGHT DETECTION: Origin={beamOrigin}, Direction={beamDirection}, Range={range}, Width={width}");
            Debug.Log($"  Layer Mask: {_lightInteractionLayers.value}");
        }

        // Cast rays within beam cone
        float halfAngle = width * 0.5f;
        int rayCount = Mathf.Max(5, Mathf.RoundToInt(width / 5f));

        if (_verboseLightDetection)
        {
            Debug.Log($"  Casting {rayCount} rays with half-angle: {halfAngle}°");
        }

        int raysHit = 0;
        List<string> hitObjects = new List<string>();

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount > 1 ? (float)i / (rayCount - 1) : 0.5f;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 rayDirection = RotateVector2(beamDirection, currentAngle);

            RaycastHit2D hit = Physics2D.Raycast(beamOrigin, rayDirection, range, _lightInteractionLayers);

            // Debug rays
            if (_showDebugRays)
            {
                Color rayColor = hit.collider != null ? Color.green : Color.red;
                float rayDistance = hit.collider != null ? hit.distance : range;
                Debug.DrawRay(beamOrigin, rayDirection * rayDistance, rayColor, 0.1f);
            }

            if (hit.collider != null)
            {
                raysHit++;
                if (!hitObjects.Contains(hit.collider.name))
                {
                    hitObjects.Add(hit.collider.name);
                }

                var lightInteractable = hit.collider.GetComponent<ILightInteractable>();
                if (lightInteractable != null && !_currentlyIlluminated.Contains(lightInteractable))
                {
                    _currentlyIlluminated.Add(lightInteractable);

                    if (_verboseLightDetection)
                    {
                        Debug.Log($"💡 RAY HIT: {hit.collider.name} at distance {hit.distance:F2}");
                        Debug.Log($"  Has ILightInteractable: ✓");
                        Debug.Log($"  Object Layer: {hit.collider.gameObject.layer} ({LayerMask.LayerToName(hit.collider.gameObject.layer)})");
                    }
                }
                else if (lightInteractable == null && _verboseLightDetection)
                {
                    Debug.Log($"⚠️ RAY HIT: {hit.collider.name} but no ILightInteractable component found!");
                }
            }
        }

        if (_verboseLightDetection)
        {
            Debug.Log($"🎯 LIGHT DETECTION SUMMARY: {raysHit}/{rayCount} rays hit objects");
            Debug.Log($"  Hit Objects: {string.Join(", ", hitObjects)}");
            Debug.Log($"  Interactable Objects Found: {_currentlyIlluminated.Count}");
        }

        ProcessIlluminationEvents();
    }

    private void ProcessIlluminationEvents()
    {
        // Newly illuminated objects
        foreach (var illuminated in _currentlyIlluminated)
        {
            if (!_previouslyIlluminated.Contains(illuminated))
            {
                illuminated.OnIlluminated(this);
                OnObjectIlluminated?.Invoke(illuminated);

                if (_verboseLightDetection)
                {
                    Debug.Log($"✨ OBJECT ILLUMINATED: {illuminated} - calling OnIlluminated()");
                }
            }
        }

        // Objects that left the light
        foreach (var previously in _previouslyIlluminated)
        {
            if (!_currentlyIlluminated.Contains(previously))
            {
                previously.OnLeftLight(this);
                OnObjectLeftLight?.Invoke(previously);

                if (_verboseLightDetection)
                {
                    Debug.Log($"🌑 OBJECT LEFT LIGHT: {previously} - calling OnLeftLight()");
                }
            }
        }
    }

    #endregion


    #region Visual Effects

    private void UpdateInnerLightEffects()
    {
        // Character ambient light
        if (_characterAmbientLight != null)
        {
            float targetIntensity = 0.2f + (_innerLightStrength / _maxInnerLightStrength) * 0.8f;
            _characterAmbientLight.intensity = Mathf.Lerp(_characterAmbientLight.intensity, targetIntensity, Time.deltaTime * 2f);

            Color targetColor = Color.Lerp(new Color(0.8f, 0.6f, 0.4f), Color.white, _innerLightStrength / _maxInnerLightStrength);
            _characterAmbientLight.color = targetColor;
        }

        // Inner light particles
        if (_innerLightParticles != null)
        {
            var emission = _innerLightParticles.emission;
            emission.rateOverTime = (_innerLightStrength / _maxInnerLightStrength) * 10f;
        }

        // Character glow
        if (_characterGlow != null)
        {
            Color glowColor = _characterGlow.color;
            glowColor.a = (_innerLightStrength / _maxInnerLightStrength) * 0.3f;
            _characterGlow.color = glowColor;
        }
    }

    private void PlayLightActivationEffect(LightTypeData lightData)
    {
        if (_audioSource != null && lightData.activationSound != null)
        {
            _audioSource.PlayOneShot(lightData.activationSound);
        }

        if (_lightEmissionEffect != null)
        {
            var main = _lightEmissionEffect.main;
            main.startColor = lightData.lightColor;
            _lightEmissionEffect.Play();
        }
    }

    private void PlayLightDiscoveryEffect(LightType lightType)
    {
        var lightData = GetLightTypeData(lightType);
        if (lightData == null) return;

        // Visual burst effect
        if (_lightEmissionEffect != null)
        {
            var main = _lightEmissionEffect.main;
            main.startColor = lightData.lightColor;
            var burst = _lightEmissionEffect.emission;
            burst.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 20)
            });
            _lightEmissionEffect.Play();
        }

        // Increase character glow temporarily
        StartCoroutine(DiscoveryGlowEffect(lightData.lightColor));
    }

    private IEnumerator DiscoveryGlowEffect(Color lightColor)
    {
        if (_characterAmbientLight == null) yield break;

        Color originalColor = _characterAmbientLight.color;
        float originalIntensity = _characterAmbientLight.intensity;

        // Bright flash
        _characterAmbientLight.color = lightColor;
        _characterAmbientLight.intensity = originalIntensity * 3f;

        yield return new WaitForSeconds(0.5f);

        // Fade back
        float elapsed = 0f;
        float duration = 2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            _characterAmbientLight.color = Color.Lerp(lightColor, originalColor, t);
            _characterAmbientLight.intensity = Mathf.Lerp(originalIntensity * 3f, originalIntensity, t);

            yield return null;
        }

        _characterAmbientLight.color = originalColor;
        _characterAmbientLight.intensity = originalIntensity;
    }

    #endregion

    #region Utilities

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

    private void ClearIlluminatedObjects()
    {
        foreach (var illuminated in _currentlyIlluminated)
        {
            illuminated.OnLeftLight(this);
            OnObjectLeftLight?.Invoke(illuminated);
        }
        _currentlyIlluminated.Clear();
    }

    #endregion

    #region Public API

    public bool HasDiscovered(LightType lightType)
    {
        return _discoveredLightTypes.Contains(lightType);
    }

    public string GetCurrentLightTypeName()
    {
        var lightData = GetLightTypeData(_currentLightType);
        return lightData?.displayName ?? "Unknown";
    }

    public List<LightType> GetDiscoveredLightTypes()
    {
        return new List<LightType>(_discoveredLightTypes);
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Test Light Detection")]
    public void DebugTestLightDetection()
    {
        if (!IsLanternActive)
        {
            Debug.Log("❌ Cannot test light detection - lantern not active");
            return;
        }

        Debug.Log("=== LIGHT DETECTION TEST ===");
        Debug.Log($"Floating Lantern: {(_floatingLantern != null ? "✓" : "❌")}");

        if (_floatingLantern != null)
        {
            Vector3 beamOrigin = _floatingLantern.GetBeamOrigin();
            Vector2 beamDirection = GetBeamDirection();
            Debug.Log($"Beam Origin: {beamOrigin}");
            Debug.Log($"Beam Direction: {beamDirection}");

            // Test direct raycast to mouse position
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;
            Vector2 toMouse = ((Vector2)mouseWorldPos - (Vector2)beamOrigin).normalized;

            RaycastHit2D testHit = Physics2D.Raycast(beamOrigin, toMouse, 15f, _lightInteractionLayers);
            if (testHit.collider != null)
            {
                Debug.Log($"✓ Test ray hit: {testHit.collider.name} at {testHit.distance:F2}");
                Debug.Log($"  Has ILightInteractable: {(testHit.collider.GetComponent<ILightInteractable>() != null ? "✓" : "❌")}");
            }
            else
            {
                Debug.Log("❌ Test ray hit nothing");
            }
        }
    }

    [ContextMenu("Debug: Show Layer Info")]
    public void DebugShowLayerInfo()
    {
        Debug.Log("=== LAYER CONFIGURATION ===");
        Debug.Log($"Light Interaction Layers: {_lightInteractionLayers.value}");

        // Show which layers are included
        for (int i = 0; i < 32; i++)
        {
            if ((_lightInteractionLayers.value & (1 << i)) != 0)
            {
                Debug.Log($"  Layer {i}: {LayerMask.LayerToName(i)} ✓");
            }
        }

        // Check platform layers
        var platforms = FindObjectsOfType<EnhancedRevealablePlatform>();
        Debug.Log($"\nFound {platforms.Length} platforms:");
        foreach (var platform in platforms)
        {
            int platformLayer = platform.gameObject.layer;
            bool isIncluded = (_lightInteractionLayers.value & (1 << platformLayer)) != 0;
            Debug.Log($"  {platform.name}: Layer {platformLayer} ({LayerMask.LayerToName(platformLayer)}) {(isIncluded ? "✓ Included" : "❌ Not Included")}");
        }
    }

    #endregion
}