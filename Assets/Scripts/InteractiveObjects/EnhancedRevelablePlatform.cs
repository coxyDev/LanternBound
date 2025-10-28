using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// CORRECTED VERSION: Fixed to match current ILightInteractable interface
/// This version uses OnLightEnter/Stay/Exit instead of the old OnIlluminated/OnLeftLight
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class EnhancedRevealablePlatform : MonoBehaviour, ILightInteractable
{
    [Header("Visual States")]
    [SerializeField] private Color _hiddenColor = new Color(1, 1, 1, 0.1f);
    [SerializeField] private Color _visibleColor = Color.white;

    [Header("Layer Management - CRITICAL FOR GROUND DETECTION")]
    [SerializeField] private bool _changeLayerWhenRevealed = true;
    [Tooltip("Set this to 'Ground' (Layer 3) so isGrounded works")]
    [SerializeField] private string _groundLayerName = "Ground";
    [Tooltip("Current layer name - usually 'PuzzleNodes' (Layer 6)")]
    [SerializeField] private string _hiddenLayerName = "PuzzleNodes";

    [Header("Light Response Settings")]
    [SerializeField]
    private EnhancedLanternController.LightEffect[] _acceptedEffects =
        { EnhancedLanternController.LightEffect.Reveal };
    [SerializeField] private float _minimumIntensity = 0.3f;

    [Header("2D Lighting")]
    [SerializeField] private Light2D _platformLight2D;
    [SerializeField] private float _lightIntensity = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    // Components
    private SpriteRenderer _renderer;
    private BoxCollider2D _collider;
    private int _originalLayer;
    private int _groundLayer;

    // ILightInteractable Properties
    public bool IsCurrentlyIlluminated { get; private set; }
    public EnhancedLanternController.LightEffect CurrentActiveEffect { get; private set; }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<BoxCollider2D>();

        // Get layer indices
        _originalLayer = LayerMask.NameToLayer(_hiddenLayerName);
        _groundLayer = LayerMask.NameToLayer(_groundLayerName);

        // Validate layers exist
        if (_originalLayer == -1)
        {
            Debug.LogError($"❌ Layer '{_hiddenLayerName}' not found! Create it in Unity's Layer settings.");
            _originalLayer = 6; // Fallback to layer 6
        }

        if (_groundLayer == -1)
        {
            Debug.LogError($"❌ Layer '{_groundLayerName}' not found! Using Layer 3 as fallback.");
            _groundLayer = 3; // Fallback to layer 3
        }

        // Setup platform light if exists
        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = false;
        }

        // Start hidden
        SetHidden();

        if (_debugMode)
        {
            Debug.Log($"✓ EnhancedRevealablePlatform initialized: {gameObject.name}");
            Debug.Log($"  Original Layer: {_hiddenLayerName} (Index: {_originalLayer})");
            Debug.Log($"  Ground Layer: {_groundLayerName} (Index: {_groundLayer})");
            Debug.Log($"  Change Layer When Revealed: {_changeLayerWhenRevealed}");
        }
    }

    #region ILightInteractable Implementation - NEW INTERFACE

    /// <summary>
    /// FIXED: Matches current ILightInteractable interface
    /// </summary>
    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        if (_debugMode)
        {
            Debug.Log($"💡 {gameObject.name} - OnLightEnter called");
            Debug.Log($"   Effect: {effect}, Intensity: {intensity:F2}");
        }

        // Check if we respond to this effect
        if (!RespondsToEffect(effect))
        {
            if (_debugMode)
                Debug.Log($"⚠️ {gameObject.name} doesn't respond to {effect}");
            return;
        }

        // Check intensity threshold
        if (intensity < GetMinimumIntensity(effect))
        {
            if (_debugMode)
                Debug.Log($"⚠️ {gameObject.name} intensity too low: {intensity:F2} < {_minimumIntensity}");
            return;
        }

        // Reveal the platform
        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;
        SetVisible();

        if (_debugMode)
        {
            Debug.Log($"✅ {gameObject.name} REVEALED!");
            Debug.Log($"   Collider Enabled: {_collider.enabled}");
            Debug.Log($"   Layer: {LayerMask.LayerToName(gameObject.layer)} (Index: {gameObject.layer})");
        }
    }

    /// <summary>
    /// FIXED: Continuous light effect handling
    /// </summary>
    public void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        // Optional: Could add pulsing or other continuous effects here
        // For now, just maintain visibility
    }

    /// <summary>
    /// FIXED: Matches current ILightInteractable interface
    /// </summary>
    public void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        if (_debugMode)
        {
            Debug.Log($"🌑 {gameObject.name} - OnLightExit called");
            Debug.Log($"   Effect: {effect}");
        }

        // Hide the platform
        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = EnhancedLanternController.LightEffect.Reveal;
        SetHidden();

        if (_debugMode)
        {
            Debug.Log($"✅ {gameObject.name} HIDDEN!");
            Debug.Log($"   Collider Enabled: {_collider.enabled}");
            Debug.Log($"   Layer: {LayerMask.LayerToName(gameObject.layer)} (Index: {gameObject.layer})");
        }
    }

    /// <summary>
    /// Check if this platform responds to the given effect
    /// </summary>
    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        foreach (var acceptedEffect in _acceptedEffects)
        {
            if (acceptedEffect == effect)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get minimum intensity required
    /// </summary>
    public float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _minimumIntensity;
    }

    #endregion

    #region Visual State Management

    private void SetVisible()
    {
        if (_renderer != null)
            _renderer.color = _visibleColor;

        if (_collider != null)
            _collider.enabled = true;

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = true;
            _platformLight2D.intensity = _lightIntensity;
        }

        // CRITICAL: Change to Ground layer so isGrounded detection works
        if (_changeLayerWhenRevealed)
        {
            gameObject.layer = _groundLayer;

            if (_debugMode)
                Debug.Log($"🔄 Platform {gameObject.name} switched to Ground layer ({_groundLayer})");
        }
    }

    private void SetHidden()
    {
        if (_renderer != null)
            _renderer.color = _hiddenColor;

        if (_collider != null)
            _collider.enabled = false;

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = false;
        }

        // Change back to original layer
        if (_changeLayerWhenRevealed)
        {
            gameObject.layer = _originalLayer;

            if (_debugMode)
                Debug.Log($"🔄 Platform {gameObject.name} switched to {_hiddenLayerName} layer ({_originalLayer})");
        }
    }

    #endregion

    #region Inspector Validation

    private void OnValidate()
    {
        // Auto-find components in editor
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();

        if (_collider == null)
            _collider = GetComponent<BoxCollider2D>();

        if (_platformLight2D == null)
            _platformLight2D = GetComponentInChildren<Light2D>();
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!_debugMode) return;

        // Draw a wireframe box showing platform bounds
        Gizmos.color = IsCurrentlyIlluminated ? Color.green : Color.red;

        if (_collider != null)
        {
            Gizmos.DrawWireCube(_collider.bounds.center, _collider.bounds.size);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }
    }

    #endregion
}