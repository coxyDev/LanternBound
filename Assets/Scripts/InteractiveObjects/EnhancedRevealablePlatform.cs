using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class EnhancedRevealablePlatform : MonoBehaviour, ILightInteractable
{
    [Header("Visual States")]
    [SerializeField] private Color _hiddenColor = new Color(1, 1, 1, 0.1f);
    [SerializeField] private Color _visibleColor = Color.white;
    [SerializeField] private bool _startHidden = true;

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
    }

    public void OnIlluminated(EnhancedLanternController lantern)
    {
        if (IsIlluminated) return;

        // Check if this platform responds to the current light type
        if (!RespondsToLightType(lantern.CurrentLightType)) return;

        IsIlluminated = true;
        SetVisible();

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