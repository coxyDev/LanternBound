using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// LightReceptor — ancient stone mechanism activated by the lantern's Energize effect.
/// 
/// Three states:
///   Hidden    — covered by corruption, not interactable. Visually dim.
///   Revealed  — corruption cleared, waiting for lantern beam. Glows faintly.
///   Activated — Energize beam received, fires OnActivated. Permanently lit.
/// 
/// SETUP:
///   1. Place this component on the receptor GameObject (ancient carved stone sprite).
///   2. Find the CorruptionLayer covering this receptor. In its Inspector, wire
///      CorruptionLayer.OnFullyCleared to this receptor's Reveal() method,
///      OR drag the CorruptionLayer into _coveringCorruption and this script
///      subscribes automatically in Start().
///   3. The LightReceptorGate listens to this receptor's OnActivated event.
///      Wire it in the gate's Inspector or let the gate find receptors automatically.
/// 
/// PULSE MECHANIC:
///   The LanternPulse script (separate) queries IsRevealed and transform.position
///   from nearby receptors. This receptor exposes both — no coupling needed.
/// 
/// SHADER:
///   Assign a material with a _GlowIntensity float property to drive the glow
///   visual as the receptor transitions between states. Built in URP Shader Graph.
///   Alternatively, drive a Light2D directly — both are supported.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LightReceptor : MonoBehaviour, ILightInteractable
{
    public enum ReceptorState
    {
        Hidden,
        Revealed,
        Activated
    }

    #region Serialized Fields

    [Header("Covering Corruption")]
    [Tooltip("The CorruptionLayer sitting on top of this receptor. " +
             "Script subscribes to its OnFullyCleared event automatically. " +
             "Leave empty if you prefer to call Reveal() manually.")]
    [SerializeField] private CorruptionLayer _coveringCorruption;

    [Header("Light Response")]
    [Tooltip("Receptors respond to Energize by default. Change only if design requires it.")]
    [SerializeField] private EnhancedLanternController.LightEffect _activationEffect =
        EnhancedLanternController.LightEffect.Energize;
    [SerializeField] private float _minimumIntensity = 0.3f;
    [Tooltip("Seconds the beam must continuously illuminate the receptor to activate it. " +
             "0 = instant activation on beam contact.")]
    [SerializeField] private float _activationHoldTime = 0.8f;

    [Header("Visual — Shader")]
    [Tooltip("Float property on this renderer's material driving glow (0=off, 1=full). " +
             "Build in URP Shader Graph. Leave empty string to skip shader drive.")]
    [SerializeField] private string _glowPropertyName = "_GlowIntensity";

    [Header("Visual — Lighting")]
    [Tooltip("URP 2D Point Light on this receptor. Intensity driven by state.")]
    [SerializeField] private Light2D _receptorLight;
    [SerializeField] private float _hiddenLightIntensity = 0f;
    [SerializeField] private float _revealedLightIntensity = 0.4f;
    [SerializeField] private float _activatedLightIntensity = 1.2f;
    [SerializeField] private float _lightTransitionSpeed = 2f;

    [Header("Visual — Colours")]
    [Tooltip("Sprite colour in each state.")]
    [SerializeField] private Color _hiddenColour = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color _revealedColour = new Color(0.6f, 0.8f, 1f, 1f);
    [SerializeField] private Color _activatedColour = new Color(1f, 0.95f, 0.6f, 1f);
    [SerializeField] private float _colourTransitionSpeed = 3f;

    [Header("Particles")]
    [Tooltip("Looping ambient glow particles. Play rate increases when revealed.")]
    [SerializeField] private ParticleSystem _ambientParticles;
    [Tooltip("One-shot burst on activation.")]
    [SerializeField] private ParticleSystem _activationBurst;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _revealSound;
    [SerializeField] private AudioClip _activatingSound;   // Loop while being charged
    [SerializeField] private AudioClip _activatedSound;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    #endregion

    #region ILightInteractable

    public bool IsCurrentlyIlluminated { get; private set; }
    public EnhancedLanternController.LightEffect CurrentActiveEffect { get; private set; }

    #endregion

    #region State & Properties

    public ReceptorState CurrentState { get; private set; } = ReceptorState.Hidden;

    /// <summary>
    /// True once corruption is cleared. Exposed for LanternPulse proximity queries.
    /// </summary>
    public bool IsRevealed => CurrentState == ReceptorState.Revealed;

    /// <summary>
    /// True once Energize beam has activated this receptor.
    /// </summary>
    public bool IsActivated => CurrentState == ReceptorState.Activated;

    #endregion

    #region Events

    /// <summary>
    /// Fired once when this receptor is fully activated.
    /// LightReceptorGate subscribes to count how many receptors are lit.
    /// </summary>
    public System.Action<LightReceptor> OnActivated;

    #endregion

    #region Private

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;

    private float _activationTimer = 0f;
    private bool _beamIsOnReceptor = false;

    private float _targetLightIntensity = 0f;
    private Color _targetColour;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _propertyBlock = new MaterialPropertyBlock();

        ApplyStateVisuals(instant: true);
    }

    private void Start()
    {
        // Subscribe to covering corruption if assigned
        if (_coveringCorruption != null)
        {
            _coveringCorruption.OnFullyCleared += Reveal;

            if (_debugMode)
                Debug.Log($"✓ LightReceptor '{name}' subscribed to CorruptionLayer '{_coveringCorruption.name}'");
        }
        else if (_debugMode)
        {
            Debug.Log($"ℹ️ LightReceptor '{name}': No covering corruption assigned. " +
                      "Call Reveal() manually or wire via Inspector event.");
        }
    }

    private void OnDestroy()
    {
        if (_coveringCorruption != null)
            _coveringCorruption.OnFullyCleared -= Reveal;
    }

    private void Update()
    {
        if (CurrentState == ReceptorState.Activated) return;

        UpdateActivationCharge();
        UpdateVisualTransitions();
    }

    #endregion

    #region State Transitions

    /// <summary>
    /// Called by CorruptionLayer.OnFullyCleared when covering corruption is cleared.
    /// Transitions receptor from Hidden to Revealed.
    /// </summary>
    public void Reveal()
    {
        if (CurrentState != ReceptorState.Hidden) return;

        CurrentState = ReceptorState.Revealed;
        _targetLightIntensity = _revealedLightIntensity;
        _targetColour = _revealedColour;

        if (_ambientParticles != null)
            _ambientParticles.Play();

        if (_audioSource != null && _revealSound != null)
            _audioSource.PlayOneShot(_revealSound);

        if (_debugMode)
            Debug.Log($"💎 LightReceptor '{name}' revealed — waiting for Energize beam");
    }

    private void Activate()
    {
        if (CurrentState != ReceptorState.Revealed) return;

        CurrentState = ReceptorState.Activated;
        _activationTimer = 0f;
        _beamIsOnReceptor = false;
        IsCurrentlyIlluminated = false;

        _targetLightIntensity = _activatedLightIntensity;
        _targetColour = _activatedColour;

        // Set shader to full glow immediately
        SetShaderGlow(1f);

        if (_ambientParticles != null)
        {
            var emission = _ambientParticles.emission;
            emission.rateOverTime = emission.rateOverTime.constant * 2f;
        }

        if (_activationBurst != null)
            _activationBurst.Play();

        if (_audioSource != null)
        {
            if (_activatingSound != null && _audioSource.isPlaying)
                _audioSource.Stop();
            if (_activatedSound != null)
                _audioSource.PlayOneShot(_activatedSound);
        }

        OnActivated?.Invoke(this);

        if (_debugMode)
            Debug.Log($"⚡ LightReceptor '{name}' ACTIVATED");
    }

    #endregion

    #region Activation Charge

    private void UpdateActivationCharge()
    {
        if (CurrentState != ReceptorState.Revealed) return;

        if (_beamIsOnReceptor)
        {
            _activationTimer += Time.deltaTime;

            // Drive a charging glow via shader while holding
            float chargeProgress = _activationHoldTime > 0
                ? Mathf.Clamp01(_activationTimer / _activationHoldTime)
                : 1f;
            SetShaderGlow(Mathf.Lerp(0.3f, 0.9f, chargeProgress));

            // Play activation loop sound
            if (_audioSource != null && _activatingSound != null && !_audioSource.isPlaying)
                _audioSource.PlayOneShot(_activatingSound);

            if (_activationTimer >= _activationHoldTime)
                Activate();
        }
        else
        {
            // Beam left before full charge — drain timer back
            _activationTimer = Mathf.MoveTowards(_activationTimer, 0f, Time.deltaTime * 2f);
            float chargeProgress = _activationHoldTime > 0
                ? Mathf.Clamp01(_activationTimer / _activationHoldTime)
                : 0f;
            SetShaderGlow(Mathf.Lerp(0.1f, 0.9f, chargeProgress));

            if (_audioSource != null && _activatingSound != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }
    }

    #endregion

    #region Visuals

    private void UpdateVisualTransitions()
    {
        // Smooth colour transition
        _spriteRenderer.color = Color.Lerp(
            _spriteRenderer.color, _targetColour, Time.deltaTime * _colourTransitionSpeed);

        // Smooth light intensity transition
        if (_receptorLight != null)
        {
            _receptorLight.intensity = Mathf.Lerp(
                _receptorLight.intensity, _targetLightIntensity, Time.deltaTime * _lightTransitionSpeed);
        }
    }

    private void ApplyStateVisuals(bool instant = false)
    {
        Color targetColour;
        float targetLight;

        switch (CurrentState)
        {
            case ReceptorState.Hidden:
                targetColour = _hiddenColour;
                targetLight = _hiddenLightIntensity;
                SetShaderGlow(0f);
                break;
            case ReceptorState.Revealed:
                targetColour = _revealedColour;
                targetLight = _revealedLightIntensity;
                SetShaderGlow(0.1f);
                break;
            case ReceptorState.Activated:
                targetColour = _activatedColour;
                targetLight = _activatedLightIntensity;
                SetShaderGlow(1f);
                break;
            default:
                targetColour = _hiddenColour;
                targetLight = _hiddenLightIntensity;
                break;
        }

        _targetColour = targetColour;
        _targetLightIntensity = targetLight;

        if (instant)
        {
            _spriteRenderer.color = targetColour;
            if (_receptorLight != null)
                _receptorLight.intensity = targetLight;
        }
    }

    private void SetShaderGlow(float value)
    {
        if (string.IsNullOrEmpty(_glowPropertyName)) return;

        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(_glowPropertyName, value);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    #endregion

    #region ILightInteractable Implementation

    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        if (CurrentState != ReceptorState.Revealed) return;
        if (!RespondsToEffect(effect) || intensity < _minimumIntensity) return;

        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;
        _beamIsOnReceptor = true;

        if (_debugMode)
            Debug.Log($"💡 LightReceptor '{name}' — Energize beam entered, charging " +
                      $"(hold time: {_activationHoldTime}s)");
    }

    public void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity,
        Vector2 direction, float deltaTime)
    {
        if (CurrentState != ReceptorState.Revealed) return;
        IsCurrentlyIlluminated = true;
        _beamIsOnReceptor = true;
    }

    public void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        if (CurrentState == ReceptorState.Activated) return;

        IsCurrentlyIlluminated = false;
        _beamIsOnReceptor = false;

        if (_debugMode && CurrentState == ReceptorState.Revealed)
            Debug.Log($"🌑 LightReceptor '{name}' — beam left" +
                      (_activationTimer > 0 ? $", charge draining ({_activationTimer:F1}s)" : ""));
    }

    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        return effect == _activationEffect;
    }

    public float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _minimumIntensity;
    }

    #endregion

    #region Debug Visualisation

    private void OnDrawGizmosSelected()
    {
        // Draw activation radius indicator
        Gizmos.color = CurrentState == ReceptorState.Revealed
            ? new Color(0.4f, 0.8f, 1f, 0.3f)
            : new Color(0.3f, 0.3f, 0.3f, 0.2f);

        Gizmos.DrawWireSphere(transform.position, 0.4f);

        // Draw charge progress in scene view during play
        if (Application.isPlaying && CurrentState == ReceptorState.Revealed && _activationTimer > 0f)
        {
            Gizmos.color = Color.yellow;
            float progress = _activationHoldTime > 0 ? _activationTimer / _activationHoldTime : 1f;
            Gizmos.DrawWireSphere(transform.position, 0.4f * progress);
        }
    }

    #endregion
}
