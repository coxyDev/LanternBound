using UnityEngine;
using System.Collections;

/// <summary>
/// CORRECTED VERSION: Collectible Ancient Lantern
/// Grants ONLY basic lantern functionality (light beam, toggle, mana)
/// Does NOT grant any active abilities - those come from separate pickups
/// </summary>
public class CollectibleLantern : MonoBehaviour
{
    [Header("Discovery Settings")]
    [SerializeField] private float _discoveryRadius = 3f;
    [SerializeField] private LayerMask _playerLayer = 1 << 6; // Player layer

    [Header("Visual Effects")]
    [SerializeField] private GameObject _glowEffect;
    [SerializeField] private ParticleSystem _pickupParticles;
    [SerializeField] private Light _pickupLight;
    [SerializeField] private AudioClip _pickupSound;

    [Header("Animation")]
    [SerializeField] private float _floatHeight = 0.3f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 30f;

    [Header("Floating Lantern")]
    [SerializeField] private GameObject _floatingLanternPrefab;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    private bool _collected = false;
    private bool _isAnimating = true;
    private Vector3 _startPosition;
    private AudioSource _audioSource;

    private void Awake()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null && _pickupSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (_collected) return;

        // Floating animation
        if (_isAnimating)
        {
            AnimateFloating();
        }

        // Check for nearby player
        CheckForPlayer();
    }

    private void AnimateFloating()
    {
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);
        transform.Rotate(Vector3.forward, _rotationSpeed * Time.deltaTime);
    }

    private void CheckForPlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _discoveryRadius, _playerLayer);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                GameObject playerRoot = FindPlayerRoot(hit.gameObject);
                if (playerRoot != null)
                {
                    CollectLantern(playerRoot);
                    break;
                }
            }
        }
    }

    private GameObject FindPlayerRoot(GameObject startObject)
    {
        if (_debugMode)
            Debug.Log($"🔍 Searching for EnhancedLanternController from: {startObject.name}");

        // Check current object
        if (startObject.GetComponent<EnhancedLanternController>() != null)
        {
            if (_debugMode)
                Debug.Log($"✓ Found on: {startObject.name}");
            return startObject;
        }

        // Check parent hierarchy
        Transform current = startObject.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<EnhancedLanternController>() != null)
            {
                if (_debugMode)
                    Debug.Log($"✓ Found on parent: {current.name}");
                return current.gameObject;
            }
            current = current.parent;
        }

        // Check children
        var controller = startObject.GetComponentInChildren<EnhancedLanternController>();
        if (controller != null)
        {
            if (_debugMode)
                Debug.Log($"✓ Found on child: {controller.name}");
            return controller.gameObject;
        }

        if (_debugMode)
            Debug.LogError($"❌ No EnhancedLanternController found!");
        return null;
    }

    private void CollectLantern(GameObject player)
    {
        if (_collected) return;
        _collected = true;
        _isAnimating = false;

        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log("🔦 ANCIENT LANTERN COLLECTED");
            Debug.Log("═══════════════════════════════════");
        }

        // STEP 1: Create floating lantern visual
        GameObject floatingLantern = SpawnFloatingLantern(player);

        // STEP 2: Enable lantern controller (grants ONLY basic light beam)
        var lanternController = player.GetComponent<EnhancedLanternController>();
        if (lanternController != null)
        {
            GrantBasicLantern(lanternController, floatingLantern);
        }
        else
        {
            Debug.LogError("❌ EnhancedLanternController not found on player!");
        }

        // STEP 3: Play effects
        PlayCollectionEffects();

        // STEP 4: Destroy collectible
        Destroy(gameObject, 2f);
    }

    private GameObject SpawnFloatingLantern(GameObject player)
    {
        // Check if floating lantern already exists
        FloatingLantern existingLantern = player.GetComponentInChildren<FloatingLantern>();
        if (existingLantern != null)
        {
            if (_debugMode)
                Debug.Log("⚠️ Floating lantern already exists");
            return existingLantern.gameObject;
        }

        // Create new floating lantern
        GameObject floatingLantern;
        if (_floatingLanternPrefab != null)
        {
            floatingLantern = Instantiate(_floatingLanternPrefab, player.transform);
        }
        else
        {
            floatingLantern = new GameObject("FloatingLantern");
            floatingLantern.transform.SetParent(player.transform);
            floatingLantern.AddComponent<FloatingLantern>();
        }

        floatingLantern.transform.localPosition = new Vector3(1f, 0.5f, 0);

        if (_debugMode)
            Debug.Log($"✓ Floating lantern spawned: {floatingLantern.name}");

        return floatingLantern;
    }

    /// <summary>
    /// CRITICAL: Only grants basic lantern functionality
    /// Does NOT grant any active abilities
    /// </summary>
    private void GrantBasicLantern(EnhancedLanternController controller, GameObject floatingLantern)
    {
        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log("⚙️ GRANTING BASIC LANTERN");
            Debug.Log("═══════════════════════════════════");
        }

        // Enable the controller
        controller.enabled = true;

        // Acquire lantern (this should enable beam and mana system)
        controller.AcquireLantern();

        // Set floating lantern reference if method exists
        var setLanternMethod = controller.GetType().GetMethod("SetFloatingLantern");
        if (setLanternMethod != null)
        {
            setLanternMethod.Invoke(controller, new object[] { floatingLantern });
        }

        // IMPORTANT: Connect to DualProgressionSystem but DO NOT grant abilities
        var progressionSystem = controller.GetComponent<DualProgressionSystem>();
        if (progressionSystem != null)
        {
            progressionSystem.Initialize(controller);

            if (_debugMode)
                Debug.Log("✓ Connected to DualProgressionSystem (NO abilities auto-granted)");
        }

        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log("✓ BASIC LANTERN GRANTED");
            Debug.Log("  - Light beam: ENABLED");
            Debug.Log("  - Mana system: ENABLED");
            Debug.Log("  - Toggle (F key): ENABLED");
            Debug.Log("  - Abilities: NONE (must find separately)");
            Debug.Log("═══════════════════════════════════");
        }
    }

    private void PlayCollectionEffects()
    {
        // Play sound
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }

        // Play particles
        if (_pickupParticles != null)
        {
            _pickupParticles.Play();
        }

        // Flash light
        if (_pickupLight != null)
        {
            StartCoroutine(FlashLight());
        }

        // Hide visual mesh
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    private IEnumerator FlashLight()
    {
        float duration = 1f;
        float elapsed = 0f;
        float startIntensity = _pickupLight.intensity;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _pickupLight.intensity = Mathf.Lerp(startIntensity, 0, t);
            yield return null;
        }

        _pickupLight.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw discovery radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _discoveryRadius);
    }
}