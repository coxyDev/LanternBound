using UnityEngine;
using System.Collections;
using System.Collections.Generic; 
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

    private Vector3 _startPosition;
    private bool _collected = false;
    private AudioSource _audioSource;

    private void Awake()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        if (_collectibleLight2D == null)
        {
            _collectibleLight2D = gameObject.AddComponent<Light2D>();
        }

        _collectibleLight2D.lightType = Light2D.LightType.Point;
        _collectibleLight2D.intensity = 1f;
        _collectibleLight2D.pointLightInnerRadius = 0.5f;
        _collectibleLight2D.pointLightOuterRadius = 4f;
        _collectibleLight2D.color = Color.yellow;

        // Setup glow light
        if (_glowLight == null)
            _glowLight = GetComponent<Light>();

        if (_glowLight != null)
        {
            _glowLight.color = Color.yellow;
            _glowLight.intensity = 2f;
            _glowLight.range = 5f;
            _glowLight.type = UnityEngine.LightType.Point;
        }

        // Ensure we have a trigger collider
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.isTrigger = true;

        StartCoroutine(PulsingLight());
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
        if (_collected || !other.CompareTag("Player")) return;

        CollectLantern(other.gameObject);
    }

    private void CollectLantern(GameObject player)
    {
        _collected = true;

        // Enable and initialize lantern controller
        var lanternController = player.GetComponent<EnhancedLanternController>();
        if (lanternController != null)
        {
            lanternController.enabled = true;
            lanternController.AcquireLantern();
        }

        // Initialize progression system with lantern
        var progression = player.GetComponent<DualProgressionSystem>();
        if (progression != null)
        {
            progression.Initialize(lanternController);
        }

        // Play effects
        if (_pickupSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }

        if (_pickupEffect != null)
        {
            _pickupEffect.Play();
        }

        Debug.Log("✨ Ancient Lantern Acquired! Press F to activate.");

        // Destroy after a short delay
        Destroy(gameObject, 0.5f);
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
    }
}