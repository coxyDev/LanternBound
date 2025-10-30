using UnityEngine;

/// <summary>
/// Bridge component: Forwards light detection to parent platform
/// Put this on the LightDetector child GameObject
/// </summary>
public class LightDetectorBridge : MonoBehaviour, ILightInteractable
{
    private ILightInteractable _parentInteractable;

    private void Awake()
    {
        // Find the ILightInteractable on parent
        _parentInteractable = GetComponentInParent<ILightInteractable>();

        if (_parentInteractable == null)
        {
            Debug.LogError($"❌ LightDetectorBridge on {name} couldn't find ILightInteractable on parent!");
        }
        else
        {
            Debug.Log($"✓ LightDetectorBridge connected to {(_parentInteractable as MonoBehaviour)?.name}");
        }
    }

    // Forward all interface calls to parent
    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        _parentInteractable?.OnLightEnter(effect, intensity, direction);
    }

    public void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        _parentInteractable?.OnLightStay(effect, intensity, direction, deltaTime);
    }

    public void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        _parentInteractable?.OnLightExit(effect);
    }

    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        return _parentInteractable?.RespondsToEffect(effect) ?? false;
    }

    public float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _parentInteractable?.GetMinimumIntensity(effect) ?? 0.5f;
    }

    public bool IsCurrentlyIlluminated => _parentInteractable?.IsCurrentlyIlluminated ?? false;

    public EnhancedLanternController.LightEffect CurrentActiveEffect =>
        _parentInteractable?.CurrentActiveEffect ?? EnhancedLanternController.LightEffect.Reveal;
}