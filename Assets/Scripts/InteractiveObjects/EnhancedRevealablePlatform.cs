using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class EnhancedRevealablePlatform : MonoBehaviour, ILightInteractable
{
    [Header("Visual States")]
    [SerializeField] private Color _hiddenColor = new Color(1, 1, 1, 0.1f);
    [SerializeField] private Color _visibleColor = Color.white;
    [SerializeField] private bool _startHidden = true;

    [Header("2D Lighting")]
    [SerializeField] private Light2D _platformLight2D;

    [Header("Light Requirements")]
    [SerializeField] private EnhancedLanternController.LightType _requiredLightType = EnhancedLanternController.LightType.Ember;
    [SerializeField] private bool _acceptAnyLightType = true;

    private SpriteRenderer _renderer;
    private BoxCollider2D _collider;

    public bool IsIlluminated { get; private set; }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<BoxCollider2D>();

        // Start in appropriate state
        if (_startHidden)
        {
            SetHidden();
        }
        else
        {
            SetVisible();
        }

        if (_platformLight2D == null)
        {
            _platformLight2D = gameObject.AddComponent<Light2D>();
        }

        _platformLight2D.lightType = Light2D.LightType.Point;
        _platformLight2D.intensity = 0.8f;
        _platformLight2D.pointLightInnerRadius = 0.2f;
        _platformLight2D.pointLightOuterRadius = 3f;
        _platformLight2D.color = Color.cyan;
        _platformLight2D.enabled = false; // Hidden by default

    }

    public void OnIlluminated(EnhancedLanternController lantern)
    {
        if (IsIlluminated) return;

        // Check if this platform responds to the current light type
        if (!RespondsToLightType(lantern.CurrentLightType)) return;

        IsIlluminated = true;
        SetVisible();

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = true;
        }

        Debug.Log($"Platform revealed by {lantern.CurrentLightType}!");
    }

    public void OnLeftLight(EnhancedLanternController lantern)
    {
        if (!IsIlluminated) return;

        IsIlluminated = false;

        if (_startHidden)
        {
            SetHidden();
            Debug.Log("Platform hidden again");
        }

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = false;
        }
    }

    public bool RespondsToLightType(EnhancedLanternController.LightType lightType)
    {
        if (_acceptAnyLightType) return true;
        return lightType == _requiredLightType;
    }

    private void SetVisible()
    {
        _renderer.color = _visibleColor;
        _collider.enabled = true;
    }

    private void SetHidden()
    {
        _renderer.color = _hiddenColor;
        _collider.enabled = false;
    }

    // Editor helper
    [ContextMenu("Test Reveal")]
    public void TestReveal()
    {
        SetVisible();
        IsIlluminated = true;
    }

    [ContextMenu("Test Hide")]
    public void TestHide()
    {
        SetHidden();
        IsIlluminated = false;
    }
}