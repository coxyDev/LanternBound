using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// MIGRATION VERSION: Updated CollectableLantern that works with the new enhanced system
/// Maintains compatibility with your existing setup while enabling advanced features
/// 
/// COMPATIBILITY: Works with both legacy and enhanced lantern controllers
/// NEW FEATURES: Advanced system integration, better visual effects
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CollectableLantern : MonoBehaviour
{
    [Header("Collection Settings")]
    [SerializeField] private bool _collected = false;
    [SerializeField] private bool _enableAdvancedFeatures = true;
    [SerializeField] private bool _debugMode = true;

    [Header("Visual Animation")]
    [SerializeField] private float _floatHeight = 0.5f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 30f;

    [Header("Light Components")]
    [SerializeField] private Light2D _collectibleLight2D;
    [SerializeField] private Light _glowLight; // Legacy 3D light support

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _ambientEffect;
    [SerializeField] private ParticleSystem _collectionEffect;
    [SerializeField] private GameObject _floatingLanternPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private AudioClip _mysticalHum;
    [SerializeField] private AudioSource _audioSource;

    [Header("Advanced System Integration")]
    [SerializeField]
    private EnhancedLanternController.LightEffect[] _grantsLightEffects =
    {
        EnhancedLanternController.LightEffect.Reveal,
        EnhancedLanternController.LightEffect.Energize
    };
    [SerializeField] private string[] _grantsAbilities = { "solar_flare" };

    private Vector3 _startPosition;
    private bool _isAnimating = true;

    private void Awake()
    {
        _startPosition = transform.position;
        SetupComponents();

        // Ensure trigger collider
        var collider = GetComponent<Collider2D>();
        collider.isTrigger = true;

        if (_debugMode)
            Debug.Log($"✓ CollectableLantern initialized at {transform.position}");
    }

    private void Start()
    {
        StartAnimations();
    }

    private void Update()
    {
        if (!_collected && _isAnimating)
        {
            AnimateCollectible();
        }
    }

    #region Setup and Animation

    private void SetupComponents()
    {
        // Setup 2D Light for new rendering pipeline
        if (_collectibleLight2D == null)
        {
            var lightObj = new GameObject("CollectibleLight2D");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _collectibleLight2D = lightObj.AddComponent<Light2D>();
        }

        _collectibleLight2D.lightType = Light2D.LightType.Point;
        _collectibleLight2D.intensity = 1f;
        _collectibleLight2D.pointLightInnerRadius = 0.2f;
        _collectibleLight2D.pointLightOuterRadius = 3f;
        _collectibleLight2D.color = new Color(1f, 0.9f, 0.6f);

        // Setup legacy 3D light if needed
        if (_glowLight == null)
        {
            var legacyLightObj = new GameObject("LegacyGlowLight");
            legacyLightObj.transform.SetParent(transform);
            legacyLightObj.transform.localPosition = Vector3.zero;
            _glowLight = legacyLightObj.AddComponent<Light>();
        }

        _glowLight.type = LightType.Point;
        _glowLight.intensity = 1f;
        _glowLight.range = 5f;
        _glowLight.color = new Color(1f, 0.9f, 0.6f);

        // Setup audio source
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3D sound

        // Setup particle effects if missing
        SetupParticleEffects();
    }

    private void SetupParticleEffects()
    {
        if (_ambientEffect == null)
        {
            var ambientObj = new GameObject("AmbientParticles");
            ambientObj.transform.SetParent(transform);
            ambientObj.transform.localPosition = Vector3.zero;
            _ambientEffect = ambientObj.AddComponent<ParticleSystem>();

            var main = _ambientEffect.main;
            main.startLifetime = 2f;
            main.startSpeed = 0.5f;
            main.startSize = 0.1f;
            main.startColor = new Color(1f, 0.9f, 0.6f, 0.7f);
            main.maxParticles = 20;

            var emission = _ambientEffect.emission;
            emission.rateOverTime = 10f;

            var shape = _ambientEffect.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
        }

        if (_collectionEffect == null)
        {
            var collectionObj = new GameObject("CollectionParticles");
            collectionObj.transform.SetParent(transform);
            collectionObj.transform.localPosition = Vector3.zero;
            _collectionEffect = collectionObj.AddComponent<ParticleSystem>();

            var main = _collectionEffect.main;
            main.startLifetime = 1f;
            main.startSpeed = 3f;
            main.startSize = 0.2f;
            main.startColor = Color.white;
            main.maxParticles = 50;

            var emission = _collectionEffect.emission;
            emission.rateOverTime = 0f; // Burst only
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 30)
            });

            _collectionEffect.Stop();
        }
    }

    private void StartAnimations()
    {
        // Start mystical humming sound
        if (_mysticalHum != null && _audioSource != null)
        {
            _audioSource.clip = _mysticalHum;
            _audioSource.loop = true;
            _audioSource.volume = 0.3f;
            _audioSource.Play();
        }

        // Start ambient particles
        if (_ambientEffect != null)
        {
            _ambientEffect.Play();
        }

        // Start pulsing light coroutine
        StartCoroutine(PulsingLight());
    }

    private void AnimateCollectible()
    {
        // Floating animation
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);

        // Rotation animation
        transform.Rotate(Vector3.forward * _rotationSpeed * Time.deltaTime);
    }

    private IEnumerator PulsingLight()
    {
        float baseIntensity = 1f;

        while (!_collected)
        {
            float pulseIntensity = baseIntensity + Mathf.Sin(Time.time * 2f) * 0.4f;

            if (_collectibleLight2D != null)
                _collectibleLight2D.intensity = pulseIntensity;

            if (_glowLight != null)
                _glowLight.intensity = pulseIntensity;

            yield return null;
        }
    }

    #endregion

    #region Collection Logic

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;

        if (_debugMode)
        {
            Debug.Log($"🔍 TRIGGER ENTERED by: {other.name}");
            Debug.Log($"  Tag: '{other.tag}'");
            Debug.Log($"  Player check: {other.CompareTag("Player")}");
        }

        if (!other.CompareTag("Player"))
        {
            if (_debugMode)
                Debug.Log($"⚠️ Not player - ignoring trigger from {other.name}");
            return;
        }

        // Find the player root GameObject with lantern controller
        GameObject playerRoot = FindPlayerRoot(other.gameObject);
        if (playerRoot != null)
        {
            CollectLantern(playerRoot);
        }
        else
        {
            Debug.LogError("❌ Could not find player root with EnhancedLanternController!");
        }
    }

    /// <summary>
    /// ENHANCED: Find the GameObject with EnhancedLanternController (supports hierarchy search)
    /// </summary>
    private GameObject FindPlayerRoot(GameObject startObject)
    {
        if (_debugMode)
            Debug.Log($"🔍 Searching for player root starting from: {startObject.name}");

        // Check current object
        if (startObject.GetComponent<EnhancedLanternController>() != null)
        {
            if (_debugMode)
                Debug.Log($"✓ Found EnhancedLanternController on: {startObject.name}");
            return startObject;
        }

        // Check parent hierarchy
        Transform current = startObject.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<EnhancedLanternController>() != null)
            {
                if (_debugMode)
                    Debug.Log($"✓ Found EnhancedLanternController on parent: {current.name}");
                return current.gameObject;
            }
            current = current.parent;
        }

        // Check children hierarchy
        var controllerInChildren = startObject.GetComponentInChildren<EnhancedLanternController>();
        if (controllerInChildren != null)
        {
            if (_debugMode)
                Debug.Log($"✓ Found EnhancedLanternController on child: {controllerInChildren.name}");
            return controllerInChildren.gameObject;
        }

        if (_debugMode)
            Debug.LogError($"❌ No EnhancedLanternController found in hierarchy of: {startObject.name}");
        return null;
    }

    private void CollectLantern(GameObject player)
    {
        if (_collected) return;

        _collected = true;
        _isAnimating = false;

        if (_debugMode)
        {
            Debug.Log("🔦 === LANTERN COLLECTION STARTED ===");
            Debug.Log($"Player Root GameObject: {player.name}");
        }

        // STEP 1: Create floating lantern
        GameObject floatingLantern = SpawnFloatingLantern(player);

        // STEP 2: Configure enhanced lantern controller
        var lanternController = player.GetComponent<EnhancedLanternController>();
        if (lanternController != null)
        {
            ConfigureEnhancedLanternController(lanternController, player, floatingLantern);
        }
        else
        {
            Debug.LogError("❌ EnhancedLanternController not found!");
        }

        // STEP 3: Play collection effects
        PlayCollectionEffects();

        // STEP 4: Destroy collectible
        Destroy(gameObject, 2f);
    }

    private GameObject SpawnFloatingLantern(GameObject player)
    {
        // FIXED: Check if a FloatingLantern already exists
        FloatingLantern existingLantern = player.GetComponentInChildren<FloatingLantern>();

        if (existingLantern != null)
        {
            if (_debugMode)
                Debug.Log($"⚠️ FloatingLantern already exists on player: {existingLantern.name}. Using existing.");

            // Set player reference and activate existing lantern
            SetFloatingLanternPlayer(existingLantern, player.transform);
            existingLantern.SetLanternActive(true);

            return existingLantern.gameObject;
        }

        GameObject floatingLantern = null;

        if (_floatingLanternPrefab != null)
        {
            // Spawn from prefab
            floatingLantern = Instantiate(_floatingLanternPrefab, player.transform);

            var floatingComponent = floatingLantern.GetComponent<FloatingLantern>();
            if (floatingComponent != null)
            {
                SetFloatingLanternPlayer(floatingComponent, player.transform);
                floatingComponent.SetLanternActive(true);
            }

            if (_debugMode)
                Debug.Log($"✓ Spawned floating lantern from prefab: {floatingLantern.name}");
        }
        else
        {
            // Create floating lantern procedurally
            floatingLantern = new GameObject("FloatingLantern");
            floatingLantern.transform.SetParent(player.transform);
            floatingLantern.transform.localPosition = new Vector3(-0.8f, 0.7f, 0f); // Behind and above

            var floatingComponent = floatingLantern.AddComponent<FloatingLantern>();
            SetFloatingLanternPlayer(floatingComponent, player.transform);
            floatingComponent.SetLanternActive(true);

            if (_debugMode)
                Debug.Log($"✓ Created floating lantern procedurally: {floatingLantern.name}");
        }

        return floatingLantern;
    }

    /// <summary>
    /// ENHANCED: Configure the enhanced lantern controller with advanced features
    /// </summary>
    private void ConfigureEnhancedLanternController(EnhancedLanternController controller, GameObject player, GameObject floatingLantern)
    {
        if (_debugMode)
        {
            Debug.Log($"🔧 Configuring Enhanced Lantern Controller");
            Debug.Log($"  Controller Enabled: {controller.enabled}");
            Debug.Log($"  Current HasLantern: {controller.HasLantern}");
        }

        // Enable controller if disabled
        if (!controller.enabled)
        {
            controller.enabled = true;
            if (_debugMode)
                Debug.Log("✓ Enabled EnhancedLanternController");
        }

        // Wait one frame then acquire lantern
        StartCoroutine(DelayedLanternAcquisition(controller, player, floatingLantern));
    }

    private IEnumerator DelayedLanternAcquisition(EnhancedLanternController controller, GameObject player, GameObject floatingLantern)
    {
        // Wait one frame to ensure all components are ready
        yield return null;

        if (_debugMode)
        {
            Debug.Log("🔦 === ATTEMPTING LANTERN ACQUISITION ===");
            Debug.Log($"Controller Valid: {controller != null}");
            Debug.Log($"Controller Enabled: {controller.enabled}");
        }

        try
        {
            // Acquire the lantern
            controller.AcquireLantern();

            if (_debugMode)
            {
                Debug.Log($"✓ AcquireLantern() called successfully");
                Debug.Log($"  HasLantern after acquisition: {controller.HasLantern}");
            }

            // Grant advanced light effects if enabled
            if (_enableAdvancedFeatures)
            {
                GrantAdvancedFeatures(controller, player);
            }

            // Initialize progression system
            var progression = player.GetComponent<DualProgressionSystem>();
            if (progression != null)
            {
                progression.Initialize(controller);

                // Grant initial abilities
                foreach (string abilityId in _grantsAbilities)
                {
                    progression.DiscoverAbility(abilityId);
                }

                if (_debugMode)
                    Debug.Log("✓ DualProgressionSystem initialized and abilities granted");
            }
            else
            {
                Debug.LogWarning("⚠️ DualProgressionSystem not found on player!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error during lantern acquisition: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }

    /// <summary>
    /// NEW: Grant advanced light effects when lantern is collected
    /// </summary>
    private void GrantAdvancedFeatures(EnhancedLanternController controller, GameObject player)
    {
        if (_debugMode)
            Debug.Log($"🌟 Granting {_grantsLightEffects.Length} advanced light effects");

        // The controller automatically has access to all effects
        // This is where you could restrict certain effects until later discovery

        // For now, all effects are available immediately
        // Future: Could implement effect discovery system here
    }

    /// <summary>
    /// Safe method to set FloatingLantern player reference
    /// </summary>
    private void SetFloatingLanternPlayer(FloatingLantern floatingLantern, Transform player)
    {
        try
        {
            // Use reflection to set the private _player field
            var playerField = typeof(FloatingLantern).GetField("_player",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (playerField != null)
            {
                playerField.SetValue(floatingLantern, player);
                if (_debugMode)
                    Debug.Log($"✓ Set FloatingLantern player reference to: {player.name}");
            }
            else
            {
                Debug.LogWarning("⚠️ Could not find _player field in FloatingLantern");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error setting FloatingLantern player reference: {e.Message}");
        }
    }

    #endregion

    #region Visual and Audio Effects

    private void PlayCollectionEffects()
    {
        if (_debugMode)
            Debug.Log("✨ Playing collection effects");

        // Play pickup sound
        if (_pickupSound != null && _audioSource != null)
        {
            _audioSource.Stop(); // Stop humming
            _audioSource.loop = false;
            _audioSource.volume = 1f;
            _audioSource.PlayOneShot(_pickupSound);
        }

        // Play collection particles
        if (_collectionEffect != null)
        {
            _collectionEffect.Play();
        }

        // Stop ambient effects
        if (_ambientEffect != null)
        {
            _ambientEffect.Stop();
        }

        // Fade out lights
        StartCoroutine(FadeOutLights());
    }

    private IEnumerator FadeOutLights()
    {
        float fadeTime = 1f;
        float elapsed = 0f;

        float startIntensity2D = _collectibleLight2D != null ? _collectibleLight2D.intensity : 0f;
        float startIntensity3D = _glowLight != null ? _glowLight.intensity : 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);

            if (_collectibleLight2D != null)
                _collectibleLight2D.intensity = startIntensity2D * alpha;

            if (_glowLight != null)
                _glowLight.intensity = startIntensity3D * alpha;

            yield return null;
        }

        // Ensure lights are off
        if (_collectibleLight2D != null)
            _collectibleLight2D.enabled = false;

        if (_glowLight != null)
            _glowLight.enabled = false;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Collection")]
    public void TestCollection()
    {
        if (Application.isPlaying && !_collected)
        {
            // Find player for testing
            var player = FindObjectOfType<EnhancedLanternController>();
            if (player != null)
            {
                CollectLantern(player.gameObject);
            }
            else
            {
                Debug.LogWarning("No EnhancedLanternController found for testing");
            }
        }
    }

    [ContextMenu("Show Lantern Info")]
    public void ShowLanternInfo()
    {
        Debug.Log($"=== COLLECTABLE LANTERN INFO ===");
        Debug.Log($"Position: {transform.position}");
        Debug.Log($"Collected: {_collected}");
        Debug.Log($"Advanced Features: {_enableAdvancedFeatures}");
        Debug.Log($"Grants Light Effects: {_grantsLightEffects.Length}");
        Debug.Log($"Grants Abilities: {_grantsAbilities.Length}");

        foreach (var effect in _grantsLightEffects)
        {
            Debug.Log($"  - Light Effect: {effect}");
        }

        foreach (var ability in _grantsAbilities)
        {
            Debug.Log($"  - Ability: {ability}");
        }
    }

    #endregion
}