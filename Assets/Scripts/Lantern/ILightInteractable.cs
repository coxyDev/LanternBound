using UnityEngine;

/// <summary>
/// Completely fixed ILightInteractable interface and components
/// </summary>
public interface ILightInteractable
{
    void OnIlluminated(EnhancedLanternController lantern);
    void OnLeftLight(EnhancedLanternController lantern);
    bool IsIlluminated { get; }
    bool RespondsToLightType(EnhancedLanternController.LightType lightType);
}

/// <summary>
/// FIXED: Light Essence pickup - currency for passive upgrades
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightEssencePickup : MonoBehaviour
{
    [Header("Essence Configuration")]
    [SerializeField] private int _essenceValue = 5;
    [SerializeField] private EssenceType _essenceType = EssenceType.Standard;

    [Header("Visual Design")]
    [SerializeField] private SpriteRenderer _essenceSprite;
    [SerializeField] private ParticleSystem _glitterEffect;
    [SerializeField] private Light _essenceLight;

    [Header("Animation")]
    [SerializeField] private float _floatHeight = 0.2f;
    [SerializeField] private float _floatSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 90f;

    public enum EssenceType
    {
        Standard,   // 1-5 essence
        Greater,    // 10-15 essence  
        Major,      // 20-25 essence
        Legendary   // 50+ essence
    }

    private Vector3 _startPosition;
    private AudioSource _audioSource;

    private void Awake()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        GetComponent<Collider2D>().isTrigger = true;
        SetupVisualsByType();
    }

    private void SetupVisualsByType()
    {
        Color essenceColor = _essenceType switch
        {
            EssenceType.Standard => new Color(0.6f, 0.9f, 1f),   // Light blue
            EssenceType.Greater => new Color(0.9f, 0.6f, 1f),    // Purple
            EssenceType.Major => new Color(1f, 0.8f, 0.3f),      // Gold
            EssenceType.Legendary => new Color(1f, 0.3f, 0.3f),  // Red
            _ => Color.white
        };

        if (_essenceSprite != null)
            _essenceSprite.color = essenceColor;

        if (_essenceLight != null)
        {
            _essenceLight.color = essenceColor;
            _essenceLight.intensity = 0.5f + (int)_essenceType * 0.3f;
        }

        if (_glitterEffect != null)
        {
            var main = _glitterEffect.main;
            main.startColor = essenceColor;
        }
    }

    private void Update()
    {
        AnimateEssence();
    }

    private void AnimateEssence()
    {
        // Float
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);

        // Rotate
        transform.Rotate(Vector3.forward * _rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // FIXED: Use DualProgressionSystem instead of non-existent CurrencyManager
        var progressionSystem = other.GetComponent<DualProgressionSystem>();
        if (progressionSystem != null)
        {
            progressionSystem.AddLightEssence(_essenceValue);

            // Play collection effect
            if (_audioSource != null)
            {
                _audioSource.Play();
            }

            Debug.Log($"Collected {_essenceValue} Light Essence!");
            Destroy(gameObject, 0.1f);
        }
    }
}