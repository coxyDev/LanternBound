using UnityEngine;

/// <summary>
/// NEW INTERFACE: Enhanced light interaction system supporting 9 different light effects
/// Replaces simple OnLightEnter/Exit with sophisticated effect-based interactions
/// 
/// MIGRATION NOTE: All your existing light-interactive objects need to implement this interface
/// </summary>
public interface ILightInteractable
{
    /// <summary>
    /// Called when light first touches this object
    /// </summary>
    /// <param name="effect">Type of light effect (Reveal, Energize, Stun, etc.)</param>
    /// <param name="intensity">Light intensity (0-2+, varies by effect)</param>
    /// <param name="direction">Direction the light is coming from</param>
    void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction);

    /// <summary>
    /// Called continuously while light is affecting this object
    /// </summary>
    /// <param name="effect">Type of light effect</param>
    /// <param name="intensity">Light intensity</param>
    /// <param name="direction">Direction the light is coming from</param>
    /// <param name="deltaTime">Time since last update</param>
    void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime);

    /// <summary>
    /// Called when light stops affecting this object
    /// </summary>
    /// <param name="effect">Type of light effect that ended</param>
    void OnLightExit(EnhancedLanternController.LightEffect effect);

    /// <summary>
    /// Check if this object responds to a specific light effect
    /// </summary>
    /// <param name="effect">Effect to check</param>
    /// <returns>True if this object responds to the effect</returns>
    bool RespondsToEffect(EnhancedLanternController.LightEffect effect);

    /// <summary>
    /// Get the minimum intensity required for this effect to work
    /// </summary>
    /// <param name="effect">Effect to check</param>
    /// <returns>Minimum intensity needed (0-1+)</returns>
    float GetMinimumIntensity(EnhancedLanternController.LightEffect effect);

    /// <summary>
    /// Current state for UI and gameplay feedback
    /// </summary>
    bool IsCurrentlyIlluminated { get; }

    /// <summary>
    /// Currently active light effect (if any)
    /// </summary>
    EnhancedLanternController.LightEffect CurrentActiveEffect { get; }
}

/// <summary>
/// MIGRATION HELPER: Base class that provides default implementations
/// Use this for easy migration of existing light-interactive objects
/// </summary>
public abstract class LightInteractableBase : MonoBehaviour, ILightInteractable
{
    [Header("Light Interaction Settings")]
    [SerializeField] protected EnhancedLanternController.LightEffect[] _acceptedEffects = { EnhancedLanternController.LightEffect.Reveal };
    [SerializeField] protected float _minimumIntensity = 0.5f;
    [SerializeField] protected bool _debugMode = true;

    public bool IsCurrentlyIlluminated { get; protected set; }
    public EnhancedLanternController.LightEffect CurrentActiveEffect { get; protected set; }

    /// <summary>
    /// Override this to handle light entering (replaces old OnLightEnter)
    /// </summary>
    protected abstract void HandleLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction);

    /// <summary>
    /// Override this to handle continuous light (new functionality)
    /// </summary>
    protected virtual void HandleLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        // Default: do nothing during continuous light
    }

    /// <summary>
    /// Override this to handle light leaving (replaces old OnLightExit)
    /// </summary>
    protected abstract void HandleLightExit(EnhancedLanternController.LightEffect effect);

    #region ILightInteractable Implementation

    public virtual void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        if (!RespondsToEffect(effect) || intensity < GetMinimumIntensity(effect))
        {
            if (_debugMode)
                Debug.Log($"{name} ignoring {effect} (intensity: {intensity:F2})");
            return;
        }

        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;

        HandleLightEnter(effect, intensity, direction);

        if (_debugMode)
            Debug.Log($"💡 {name} illuminated with {effect} (intensity: {intensity:F2})");
    }

    public virtual void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        if (!IsCurrentlyIlluminated || CurrentActiveEffect != effect) return;

        HandleLightStay(effect, intensity, direction, deltaTime);
    }

    public virtual void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        if (!IsCurrentlyIlluminated || CurrentActiveEffect != effect) return;

        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = EnhancedLanternController.LightEffect.Reveal; // Reset to default

        HandleLightExit(effect);

        if (_debugMode)
            Debug.Log($"🌑 {name} left light ({effect})");
    }

    public virtual bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        foreach (var acceptedEffect in _acceptedEffects)
        {
            if (acceptedEffect == effect)
                return true;
        }
        return false;
    }

    public virtual float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _minimumIntensity;
    }

    #endregion

    #region LEGACY SUPPORT METHODS

    /// <summary>
    /// LEGACY SUPPORT: Call this from your existing OnLightEnter() methods
    /// </summary>
    protected void CallLegacyOnLightEnter()
    {
        OnLightEnter(EnhancedLanternController.LightEffect.Reveal, 1f, Vector2.zero);
    }

    /// <summary>
    /// LEGACY SUPPORT: Call this from your existing OnLightExit() methods
    /// </summary>
    protected void CallLegacyOnLightExit()
    {
        OnLightExit(EnhancedLanternController.LightEffect.Reveal);
    }

    #endregion
}