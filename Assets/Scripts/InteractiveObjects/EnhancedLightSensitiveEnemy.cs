using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnhancedLightSensitiveEnemy : MonoBehaviour, ILightInteractable
{
    [Header("Enemy Behavior")]
    [SerializeField] private float _retreatDistance = 3f;
    [SerializeField] private float _retreatSpeed = 5f;
    [SerializeField] private float _returnSpeed = 2f;
    [SerializeField] private bool _freezeInLight = false;
    [SerializeField] private bool _retreatFromLight = true;

    [Header("Light Sensitivity")]
    [SerializeField] private EnhancedLanternController.LightType _vulnerableTo = EnhancedLanternController.LightType.Ember;
    [SerializeField] private bool _vulnerableToAllLight = true;

    [Header("Visual Effects")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _illuminatedColor = Color.red;

    private Vector3 _originalPosition;
    private bool _isRetreating = false;
    private bool _isFrozen = false;

    public bool IsIlluminated { get; private set; }

    private void Awake()
    {
        _originalPosition = transform.position;

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
            _spriteRenderer.color = _normalColor;
    }

    public void OnIlluminated(EnhancedLanternController lantern)
    {
        if (IsIlluminated) return;

        // Check if this enemy responds to the current light type
        if (!RespondsToLightType(lantern.CurrentLightType)) return;

        IsIlluminated = true;

        if (_freezeInLight)
        {
            _isFrozen = true;
            _isRetreating = false;
        }
        else if (_retreatFromLight)
        {
            _isRetreating = true;
            _isFrozen = false;
        }

        // Visual feedback
        if (_spriteRenderer != null)
            _spriteRenderer.color = _illuminatedColor;

        Debug.Log($"Enemy affected by {lantern.CurrentLightType}!");

        // Award essence to progression system
        var player = lantern.GetComponent<DualProgressionSystem>();
        if (player != null)
        {
            player.AddLightEssence(2); // Small essence reward
        }
    }

    public void OnLeftLight(EnhancedLanternController lantern)
    {
        if (!IsIlluminated) return;

        IsIlluminated = false;
        _isFrozen = false;
        _isRetreating = false;

        // Visual feedback
        if (_spriteRenderer != null)
            _spriteRenderer.color = _normalColor;

        Debug.Log("Enemy no longer affected by light");
    }

    public bool RespondsToLightType(EnhancedLanternController.LightType lightType)
    {
        if (_vulnerableToAllLight) return true;
        return lightType == _vulnerableTo;
    }

    private void Update()
    {
        if (_isFrozen) return; // Don't move if frozen

        if (_isRetreating)
        {
            // Simple retreat behavior - move away from player
            var player = FindObjectOfType<EnhancedLanternController>();
            if (player != null)
            {
                Vector3 playerPos = player.transform.position;
                Vector3 retreatDirection = (transform.position - playerPos).normalized;

                // Move away from player
                Vector3 targetPos = _originalPosition + retreatDirection * _retreatDistance;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, _retreatSpeed * Time.deltaTime);
            }
        }
        else if (!IsIlluminated)
        {
            // Return to original position when not illuminated
            transform.position = Vector3.MoveTowards(transform.position, _originalPosition, _returnSpeed * Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _retreatDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_originalPosition, 0.5f);
    }
}