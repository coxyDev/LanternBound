using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider2D))]
public class CollectibleLantern : MonoBehaviour
{
    [Header("2D Lighting")]
    [SerializeField] private Light2D _collectibleLight2D;

    [Header("Pickup Effects")]
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private ParticleSystem _pickupEffect;
    [SerializeField] private Light _glowLight;

    [Header("Animation")]
    [SerializeField] private float _floatHeight = 0.3f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 30f;

    [Header("Floating Lantern Integration")]
    [SerializeField] private GameObject _floatingLanternPrefab;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    private Vector3 _startPosition;
    private bool _collected = false;
    private AudioSource _audioSource;

    private void Awake()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        SetupLighting();
        SetupCollider();
        StartCoroutine(PulsingLight());

        if (_debugMode)
        {
            Debug.Log($"✓ CollectibleLantern initialized at {transform.position}");
            Debug.Log($"  Has FloatingLanternPrefab: {_floatingLanternPrefab != null}");
        }
    }

    private void SetupLighting()
    {
        // Setup 2D Light
        if (_collectibleLight2D == null)
        {
            _collectibleLight2D = gameObject.AddComponent<Light2D>();
        }

        _collectibleLight2D.lightType = Light2D.LightType.Point;
        _collectibleLight2D.intensity = 1f;
        _collectibleLight2D.pointLightInnerRadius = 0.5f;
        _collectibleLight2D.pointLightOuterRadius = 4f;
        _collectibleLight2D.color = Color.yellow;

        // Setup 3D glow light for compatibility
        if (_glowLight == null)
            _glowLight = GetComponent<Light>();

        if (_glowLight != null)
        {
            _glowLight.color = Color.yellow;
            _glowLight.intensity = 2f;
            _glowLight.range = 5f;
            _glowLight.type = UnityEngine.LightType.Point;
        }
    }

    private void SetupCollider()
    {
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;

            if (_debugMode)
            {
                Debug.Log($"✓ Collider setup: {collider.GetType().Name}, IsTrigger: {collider.isTrigger}");
            }
        }
        else
        {
            Debug.LogError("❌ No Collider2D found on CollectibleLantern!");
        }
    }

    private void Update()
    {
        if (!_collected)
        {
            AnimateCollectible();
        }
    }

    private void AnimateCollectible()
    {
        // Floating animation
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);

        // Rotation animation
        transform.Rotate(Vector3.forward * _rotationSpeed * Time.deltaTime);

        // Pulsing glow
        if (_glowLight != null)
        {
            float pulseIntensity = 1f + Mathf.Sin(Time.time * _floatSpeed * 1.5f) * 0.3f;
            _glowLight.intensity = pulseIntensity;
        }
    }

    private IEnumerator PulsingLight()
    {
        float baseIntensity = 1f;

        while (!_collected && _collectibleLight2D != null)
        {
            float pulseIntensity = baseIntensity + Mathf.Sin(Time.time * 2f) * 0.3f;
            _collectibleLight2D.intensity = pulseIntensity;
            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;

        if (_debugMode)
        {
            Debug.Log($"🔍 TRIGGER ENTERED by: {other.name}");
            Debug.Log($"  Other Tag: '{other.tag}'");
            Debug.Log($"  Other GameObject: '{other.gameObject.name}'");
            Debug.Log($"  Player Tag Check: {other.CompareTag("Player")}");
        }

        if (!other.CompareTag("Player"))
        {
            if (_debugMode)
            {
                Debug.Log($"⚠️ Not player - ignoring trigger from {other.name}");
            }
            return;
        }

        // FIXED: Find the actual player root GameObject
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
    /// FIXED: Search hierarchy to find the GameObject with EnhancedLanternController
    /// </summary>
    private GameObject FindPlayerRoot(GameObject startObject)
    {
        if (_debugMode)
        {
            Debug.Log($"🔍 Searching for player root starting from: {startObject.name}");
        }

        // Check current object
        if (startObject.GetComponent<EnhancedLanternController>() != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found EnhancedLanternController on: {startObject.name}");
            }
            return startObject;
        }

        // Check parent hierarchy
        Transform current = startObject.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<EnhancedLanternController>() != null)
            {
                if (_debugMode)
                {
                    Debug.Log($"✓ Found EnhancedLanternController on parent: {current.name}");
                }
                return current.gameObject;
            }
            current = current.parent;
        }

        // Check children hierarchy
        var controllerInChildren = startObject.GetComponentInChildren<EnhancedLanternController>();
        if (controllerInChildren != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found EnhancedLanternController on child: {controllerInChildren.name}");
            }
            return controllerInChildren.gameObject;
        }

        if (_debugMode)
        {
            Debug.LogError($"❌ No EnhancedLanternController found in hierarchy of: {startObject.name}");
        }
        return null;
    }

    private void CollectLantern(GameObject player)
    {
        if (_collected) return;

        _collected = true;

        if (_debugMode)
        {
            Debug.Log("🔦 === LANTERN COLLECTION STARTED ===");
            Debug.Log($"Player Root GameObject: {player.name}");
        }

        // STEP 1: Spawn floating lantern
        GameObject floatingLantern = SpawnFloatingLantern(player);

        // STEP 2: Find and configure lantern controller
        var lanternController = player.GetComponent<EnhancedLanternController>();
        if (lanternController != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found EnhancedLanternController on {player.name}");
                Debug.Log($"  Controller Enabled: {lanternController.enabled}");
                Debug.Log($"  Current HasLantern: {lanternController.HasLantern}");
            }

            // Enable controller if disabled
            if (!lanternController.enabled)
            {
                lanternController.enabled = true;
                if (_debugMode)
                {
                    Debug.Log("✓ Enabled EnhancedLanternController");
                }
            }

            // CRITICAL: Wait one frame before acquiring lantern
            StartCoroutine(DelayedLanternAcquisition(lanternController, player, floatingLantern));
        }
        else
        {
            Debug.LogError("❌ EnhancedLanternController not found even after hierarchy search!");
        }

        // STEP 3: Play effects immediately
        PlayPickupEffects();

        // STEP 4: Destroy collectible after delay
        Destroy(gameObject, 1f);
    }

    private GameObject SpawnFloatingLantern(GameObject player)
    {
        GameObject floatingLantern = null;

        if (_floatingLanternPrefab != null)
        {
            // Spawn prefab
            floatingLantern = Instantiate(_floatingLanternPrefab);

            var floatingLanternComponent = floatingLantern.GetComponent<FloatingLantern>();
            if (floatingLanternComponent != null)
            {
                // FIXED: Set player reference using our safe method
                SetFloatingLanternPlayer(floatingLanternComponent, player.transform);
            }

            if (_debugMode)
            {
                Debug.Log($"✓ Spawned floating lantern from prefab: {floatingLantern.name}");
            }
        }
        else
        {
            // Create floating lantern procedurally
            floatingLantern = new GameObject("FloatingLantern");
            var floatingLanternComponent = floatingLantern.AddComponent<FloatingLantern>();

            // FIXED: Set player reference using our safe method
            SetFloatingLanternPlayer(floatingLanternComponent, player.transform);

            if (_debugMode)
            {
                Debug.Log($"✓ Created floating lantern procedurally: {floatingLantern.name}");
            }
        }

        return floatingLantern;
    }

    /// <summary>
    /// FIXED: Safe method to set FloatingLantern player reference
    /// </summary>
    private void SetFloatingLanternPlayer(FloatingLantern floatingLantern, Transform player)
    {
        try
        {
            var playerField = typeof(FloatingLantern).GetField("_player",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (playerField != null)
            {
                playerField.SetValue(floatingLantern, player);
                if (_debugMode)
                {
                    Debug.Log($"✓ Set FloatingLantern player reference to: {player.name}");
                }
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

    private IEnumerator DelayedLanternAcquisition(EnhancedLanternController controller, GameObject player, GameObject floatingLantern)
    {
        // Wait one frame to ensure all components are ready
        yield return null;

        if (_debugMode)
        {
            Debug.Log("🔦 === ATTEMPTING LANTERN ACQUISITION ===");
            Debug.Log($"Controller Valid: {controller != null}");
            Debug.Log($"Controller Enabled: {controller.enabled}");
            Debug.Log($"Current HasLantern Before: {controller.HasLantern}");
        }

        // Acquire lantern
        try
        {
            controller.AcquireLantern();

            if (_debugMode)
            {
                Debug.Log($"✓ AcquireLantern() called");
                Debug.Log($"Current HasLantern After: {controller.HasLantern}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error calling AcquireLantern(): {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }

        // Initialize progression system
        var progression = player.GetComponent<DualProgressionSystem>();
        if (progression != null)
        {
            try
            {
                progression.Initialize(controller);

                if (_debugMode)
                {
                    Debug.Log("✓ DualProgressionSystem initialized");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error initializing DualProgressionSystem: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ DualProgressionSystem not found on player!");
        }

        // Final status check
        if (_debugMode)
        {
            Debug.Log("🔦 === LANTERN ACQUISITION COMPLETE ===");
            Debug.Log($"Final HasLantern: {controller.HasLantern}");
            Debug.Log($"Final Light Type: {controller.CurrentLightType}");
            Debug.Log($"Floating Lantern Created: {floatingLantern != null}");
            Debug.Log("✨ ANCIENT LANTERN ACQUIRED! Press F to activate.");
        }
    }

    private void PlayPickupEffects()
    {
        // Play sound
        if (_pickupSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
            if (_debugMode)
            {
                Debug.Log("🔊 Pickup sound played");
            }
        }

        // Play particle effect
        if (_pickupEffect != null)
        {
            _pickupEffect.Play();
            if (_debugMode)
            {
                Debug.Log("✨ Pickup particles played");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);

        // Show float range
        Gizmos.color = Color.cyan;
        Vector3 topPos = _startPosition + Vector3.up * _floatHeight;
        Vector3 bottomPos = _startPosition - Vector3.up * _floatHeight;
        Gizmos.DrawLine(topPos, bottomPos);

        // Show trigger area
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            Gizmos.color = Color.green;
            if (collider is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(transform.position, box.size);
            }
            else if (collider is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere(transform.position, circle.radius);
            }
        }
    }
}