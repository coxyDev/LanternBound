using UnityEngine;

/// <summary>
/// SIMPLIFIED VERSION: Uses explicit serialized references instead of auto-discovery
/// This makes debugging much easier - just drag and drop in Inspector
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnhancedRevealablePlatform : MonoBehaviour, ILightInteractable
{
    [Header("⚠️ REQUIRED REFERENCES - DRAG AND DROP")]
    [Tooltip("The physical collider that players stand on - gets disabled when hidden")]
    [SerializeField] private Collider2D _physicalCollider;

    [Tooltip("The trigger collider for light detection - always stays enabled")]
    [SerializeField] private Collider2D _lightDetectionCollider;

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
    [SerializeField] private SpriteRenderer _spriteRenderer;

    // State
    private bool _isRevealed = false;
    private Color _targetColor;

    // ILightInteractable Properties
    public bool IsCurrentlyIlluminated { get; private set; }
    public EnhancedLanternController.LightEffect CurrentActiveEffect { get; private set; }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // Get both colliders on this GameObject
        Collider2D[] colliders = GetComponents<Collider2D>();

        Debug.Log("═══ Platform Collider Setup ═══");
        Debug.Log($"Found {colliders.Length} colliders on {gameObject.name}");

        foreach (var col in colliders)
        {
            if (col.isTrigger)
            {
                _lightDetectionCollider = col;
                Debug.Log($"  Trigger collider (light detection): {col.GetType().Name}");
            }
            else
            {
                _physicalCollider = col;
                Debug.Log($"  Physical collider: {col.GetType().Name}");
            }
        }

        // CRITICAL: Ignore trigger collider for player physics
        if (_lightDetectionCollider != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // Get ALL colliders on player (including children like head detector)
                Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();

                Debug.Log($"Found {playerColliders.Length} player colliders to ignore");

                foreach (var playerCol in playerColliders)
                {
                    Physics2D.IgnoreCollision(_lightDetectionCollider, playerCol, true);
                    Debug.Log($"  ✓ Ignoring collision: {playerCol.name}");
                }

                Debug.Log("✓ Light detection trigger now ignores ALL player colliders");
            }
            else
            {
                Debug.LogWarning("⚠️ Player not found - couldn't set up collision ignore");
            }
        }

        Debug.Log("═══════════════════════════");

        // Validate references
        if (_physicalCollider == null)
        {
            Debug.LogError($"❌ {gameObject.name}: No physical collider found!");
        }

        if (_lightDetectionCollider == null)
        {
            Debug.LogError($"❌ {gameObject.name}: No trigger collider found!");
        }

        // Set initial state
        if (_startHidden)
        {
            SetHiddenState(true);
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
        Color newColor = Color.Lerp(current, _targetColor, Time.deltaTime * _fadeSpeed);

        // Debug color changes
        if (_debugMode && Vector4.Distance(current, newColor) > 0.01f)
        {
            Debug.Log($"🎨 Color alpha: {current.a:F2} → {newColor.a:F2} (target: {_targetColor.a:F2})");
        }

        _spriteRenderer.color = newColor;
    }

    #region ILightInteractable Implementation

    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log($"💡 LIGHT ENTER: {gameObject.name}");
            Debug.Log($"  Effect: {effect}");
            Debug.Log($"  Intensity: {intensity:F2}");
        }

        if (!RespondsToEffect(effect) || intensity < GetMinimumIntensity(effect))
        {
            if (_debugMode)
                Debug.LogWarning($"❌ Ignoring light - effect/intensity check failed");
            return;
        }

        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;

        // Check if intensity is above threshold to reveal
        if (intensity >= _revealThreshold && !_isRevealed)
        {
            if (_debugMode)
                Debug.Log($"🎯 Calling RevealPlatform()");
            RevealPlatform();
        }

        // CHANGED: Only update color if already revealed
        // Don't interfere with the reveal animation
        if (_isRevealed && _spriteRenderer.color.a > 0.9f) // Only after fade-in completes
        {
            _targetColor = Color.Lerp(_revealedColor, _illuminatedColor, intensity);
        }

        if (_debugMode)
            Debug.Log("═══════════════════════════════════");
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
        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log($"🌑 LIGHT EXIT: {gameObject.name}");
            Debug.Log($"  Effect: {effect}");
            Debug.Log($"  Was Revealed: {_isRevealed}");
        }

        if (!IsCurrentlyIlluminated || CurrentActiveEffect != effect)
        {
            if (_debugMode)
                Debug.Log($"❌ Ignoring exit - not currently illuminated or wrong effect");
            return;
        }

        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = EnhancedLanternController.LightEffect.Reveal;

        // Hide platform when light is removed
        if (_isRevealed)
        {
            if (_debugMode)
                Debug.Log($"🎯 Calling HidePlatform()");
            HidePlatform();
        }

        if (_debugMode)
            Debug.Log("═══════════════════════════════════");
    }

    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        foreach (var acceptedEffect in _acceptedEffects)
        {
            if (acceptedEffect == effect)
                return true;
        }
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
        if (_debugMode)
        {
            Debug.Log("═══ REVEAL PLATFORM ═══");
            Debug.Log($"  _isRevealed before: {_isRevealed}");
        }

        if (_isRevealed)
        {
            if (_debugMode)
                Debug.Log("  Already revealed, skipping");
            return;
        }

        _isRevealed = true;
        _targetColor = _revealedColor;

        if (_debugMode)
        {
            Debug.Log($"  Target color set to: {_targetColor}");
            Debug.Log($"  Current sprite color: {_spriteRenderer.color}");
        }

        // Enable physical collision
        if (_disableCollisionWhenHidden && _physicalCollider != null)
        {
            if (_debugMode)
                Debug.Log($"  Physical collider before: {_physicalCollider.enabled}");

            _physicalCollider.enabled = true;

            if (_debugMode)
            {
                Debug.Log($"  Physical collider after: {_physicalCollider.enabled}");
                Debug.Log($"  ✓ Physical collision ENABLED");
            }
        }
        else if (_debugMode)
        {
            Debug.LogWarning($"  ❌ Collider not enabled:");
            Debug.LogWarning($"    _disableCollisionWhenHidden: {_disableCollisionWhenHidden}");
            Debug.LogWarning($"    _physicalCollider: {_physicalCollider}");
        }

        if (_debugMode)
            Debug.Log($"✨ PLATFORM REVEALED: {gameObject.name}");
    }

    private void HidePlatform()
    {
        if (_debugMode)
        {
            Debug.Log("═══ HIDE PLATFORM ═══");
            Debug.Log($"  _isRevealed before: {_isRevealed}");
        }

        if (!_isRevealed)
        {
            if (_debugMode)
                Debug.Log("  Already hidden, skipping");
            return;
        }

        _isRevealed = false;
        _targetColor = _hiddenColor;

        // Disable physical collision
        if (_disableCollisionWhenHidden && _physicalCollider != null)
        {
            _physicalCollider.enabled = false;

            if (_debugMode)
                Debug.Log($"  ✓ Physical collision DISABLED");
        }

        if (_debugMode)
            Debug.Log($"🌑 PLATFORM HIDDEN: {gameObject.name}");
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

        // Disable physical collision
        if (_disableCollisionWhenHidden && _physicalCollider != null)
        {
            _physicalCollider.enabled = false;
        }

        // Light detection collider always enabled
        if (_lightDetectionCollider != null)
        {
            _lightDetectionCollider.enabled = true;
        }

        if (_disableCollisionWhenHidden && _physicalCollider != null)
        {
            Debug.Log($"[SetHiddenState] Physical collider BEFORE: {_physicalCollider.enabled}");
            _physicalCollider.enabled = false;
            Debug.Log($"[SetHiddenState] Physical collider AFTER: {_physicalCollider.enabled}");
            Debug.Log($"[SetHiddenState] Collider type: {_physicalCollider.GetType().Name}");
        }

        if (_debugMode)
        {
            Debug.Log($"🌑 Platform initialized as HIDDEN: {gameObject.name}");
            Debug.Log($"  Sprite alpha: {_spriteRenderer.color.a}");
            Debug.Log($"  Physical collider enabled: {_physicalCollider?.enabled}");
            Debug.Log($"  Light detection enabled: {_lightDetectionCollider?.enabled}");
        }
    }

    private void SetRevealedState(bool immediate)
    {
        _isRevealed = true;

        if (immediate)
        {
            _spriteRenderer.color = _revealedColor;
        }

        _targetColor = _revealedColor;

        if (_physicalCollider != null)
        {
            _physicalCollider.enabled = true;
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

    #region Validation

    private void OnValidate()
    {
        // Ensure fade speed is positive
        if (_fadeSpeed < 0.1f) _fadeSpeed = 0.1f;

        // Ensure threshold is between 0 and 1
        _revealThreshold = Mathf.Clamp01(_revealThreshold);

        // Warn if colliders not assigned
        if (_physicalCollider == null)
        {
            Debug.LogWarning($"{gameObject.name}: Physical Collider not assigned!");
        }

        if (_lightDetectionCollider == null)
        {
            Debug.LogWarning($"{gameObject.name}: Light Detection Collider not assigned!");
        }
    }

    #endregion
}