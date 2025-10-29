using UnityEngine;

/// <summary>
/// INTERFACE-CORRECTED VERSION: Matches actual ILightInteractable interface
/// Properly implements OnLightEnter/Stay/Exit with correct signatures
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class EnhancedRevealablePlatform : MonoBehaviour, ILightInteractable
{
    [Header("Visibility Settings")]
    [SerializeField] private bool _startHidden = true;
    [SerializeField] private float _revealThreshold = 0.3f;
    [SerializeField] private float _fadeSpeed = 2f;

    [Header("Visual Settings")]
    [SerializeField] private Color _hiddenColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color _revealedColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color _illuminatedColor = new Color(1f, 1f, 0.8f, 1f);

    [Header("Collision Settings")]
    [SerializeField] private bool _disableCollisionWhenHidden = true;

    [Header("Light Response")]
    [SerializeField]
    private EnhancedLanternController.LightEffect[] _acceptedEffects =
        { EnhancedLanternController.LightEffect.Reveal };
    [SerializeField] private float _minimumIntensity = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    // Components
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;

    // State
    private bool _isRevealed = false;
    private Color _targetColor;

    // ILightInteractable Properties (REQUIRED BY INTERFACE)
    public bool IsCurrentlyIlluminated { get; private set; }
    public EnhancedLanternController.LightEffect CurrentActiveEffect { get; private set; }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();

        // Set initial state
        if (_startHidden)
        {
            SetHiddenState(true);  // This MUST disable collider
        }
        else
        {
            SetRevealedState(true);
        }
    }

    private void Update()
    {
        // Smooth color transition
        Color current = _spriteRenderer.color;
        _spriteRenderer.color = Color.Lerp(current, _targetColor, Time.deltaTime * _fadeSpeed);
    }

    #region ILightInteractable Implementation (CORRECT SIGNATURES)

    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        Debug.Log($"🔦 LIGHT HIT: {gameObject.name}");

        if (!RespondsToEffect(effect) || intensity < GetMinimumIntensity(effect))
            return;

        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;

        if (_debugMode)
            Debug.Log($"💡 Platform illuminated | Effect: {effect} | Intensity: {intensity:F2}");

        // Check if intensity is above threshold to reveal
        if (intensity >= _revealThreshold && !_isRevealed)
        {
            RevealPlatform();
        }

        // Update color based on illumination
        if (_isRevealed)
        {
            _targetColor = Color.Lerp(_revealedColor, _illuminatedColor, intensity);
        }
    }

    public void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        if (!IsCurrentlyIlluminated || CurrentActiveEffect != effect)
            return;

        // Maintain revealed state while lit
        if (intensity >= _revealThreshold)
        {
            _targetColor = Color.Lerp(_revealedColor, _illuminatedColor, intensity);
        }
    }

    public void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        if (!IsCurrentlyIlluminated || CurrentActiveEffect != effect)
            return;

        if (_debugMode)
            Debug.Log($"🌑 Platform left light | Effect: {effect}");

        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = EnhancedLanternController.LightEffect.Reveal;

        // Hide platform when light is removed
        if (_isRevealed)
        {
            HidePlatform();
        }
    }

    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        Debug.Log($"═══ RespondsToEffect Check ═══");
        Debug.Log($"Platform: {gameObject.name}");
        Debug.Log($"Effect being checked: {effect}");
        Debug.Log($"Accepted effects: {string.Join(", ", _acceptedEffects)}");

        foreach (var acceptedEffect in _acceptedEffects)
        {
            if (acceptedEffect == effect)
                return true;
        }
       
        Debug.Log($"✗ NO MATCH! Platform does not accept {effect}");
        return false;
    }

    public float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _minimumIntensity;
    }

    #endregion

    #region Reveal/Hide Logic

    private void RevealPlatform()
    {
        if (_isRevealed) return;

        _isRevealed = true;
        _targetColor = _revealedColor;

        // Enable collision
        if (_disableCollisionWhenHidden && _collider != null)
        {
            _collider.enabled = true;
        }

        if (_debugMode)
            Debug.Log($"✨ Platform REVEALED: {gameObject.name}");
    }

    private void HidePlatform()
    {
        if (!_isRevealed) return;

        _isRevealed = false;
        _targetColor = _hiddenColor;

        // Disable collision
        if (_disableCollisionWhenHidden && _collider != null)
        {
            _collider.enabled = false;
        }

        if (_debugMode)
            Debug.Log($"🌑 Platform HIDDEN: {gameObject.name}");
    }

    private void SetHiddenState(bool immediate)
    {
        _isRevealed = false;
        IsCurrentlyIlluminated = false;

        if (immediate)
        {
            _spriteRenderer.color = _hiddenColor;
        }

        _targetColor = _hiddenColor;

        if (_disableCollisionWhenHidden && _collider != null)
        {
            _collider.enabled = false;
        }

        if (_debugMode)
            Debug.Log($"🌑 Platform initialized as HIDDEN: {gameObject.name}");
    }

    private void SetRevealedState(bool immediate)
    {
        _isRevealed = true;

        if (immediate)
        {
            _spriteRenderer.color = _revealedColor;
        }

        _targetColor = _revealedColor;

        if (_collider != null)
        {
            _collider.enabled = true;
        }

        if (_debugMode)
            Debug.Log($"✨ Platform initialized as REVEALED: {gameObject.name}");
    }

    #endregion

    #region Public API

    public void ForceReveal()
    {
        IsCurrentlyIlluminated = true;
        RevealPlatform();
    }

    public void ForceHide()
    {
        IsCurrentlyIlluminated = false;
        HidePlatform();
    }

    public bool IsRevealed => _isRevealed;

    #endregion
}