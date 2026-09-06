using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// CorruptionLayer — visual and interactive corruption surface.
/// 
/// SETUP:
///   1. Your surface geometry (wall, floor, platform) has its own SpriteRenderer — leave it alone.
///   2. Add a child GameObject to that surface. Assign your Photoshop corruption art sprite
///      to a SpriteRenderer on that child, with a higher sorting order so it sits on top.
///   3. Place this component anywhere convenient in the hierarchy (parent or child).
///      Drag the corruption child's SpriteRenderer into _corruptionRenderer.
///   4. Assign a material to that SpriteRenderer that exposes a float property named
///      _DissolveAmount (0=fully corrupt, 1=fully clear). Build this in URP Shader Graph.
///      Export your corruption art from Photoshop as PNG with transparency so the clean
///      surface underneath shows through as dissolve increases.
/// 
/// Clearing is PERMANENT by default — once cleared, stays cleared.
/// 
/// Two modes:
///   BeamTriggered  — implements ILightInteractable, cleared by lantern beam (LightEffect.Reveal)
///   ProximityBased — cleared progressively by player walking past (story wall mode)
/// 
/// Events:
///   OnFullyCleared — fired once when fully cleared. Wire to gates, narrative triggers, etc.
/// </summary>
public class CorruptionLayer : MonoBehaviour, ILightInteractable
{
    public enum ClearingMode
    {
        BeamTriggered,   // Cleared by lantern beam
        ProximityBased   // Cleared by player walking past — story wall mode
    }

    #region Serialized Fields

    [Header("Corruption Art")]
    [Tooltip("The SpriteRenderer holding your Photoshop corruption art layer. " +
             "This is a child object sitting on top of your surface geometry.")]
    [SerializeField] private SpriteRenderer _corruptionRenderer;

    [Header("Clearing Mode")]
    [SerializeField] private ClearingMode _clearingMode = ClearingMode.BeamTriggered;

    [Header("Shader Settings")]
    [Tooltip("Float property name on the corruption material. Must match exactly.")]
    [SerializeField] private string _dissolvePropertyName = "_DissolveAmount";
    [Tooltip("How fast corruption clears when actively illuminated or in proximity range. Units per second.")]
    [SerializeField] private float _clearSpeed = 1.5f;
    [Tooltip("If true, cleared corruption stays cleared when beam leaves. Recommended on for the opening act.")]
    [SerializeField] private bool _permanentClearing = true;
    [Tooltip("How fast corruption returns if permanentClearing is false.")]
    [SerializeField] private float _returnSpeed = 0.3f;

    [Header("Proximity Settings (ProximityBased mode only)")]
    [Tooltip("Auto-found at runtime if left empty.")]
    [SerializeField] private Transform _player;
    [Tooltip("How far ahead of the player the corruption begins to clear. World units.")]
    [SerializeField] private float _clearLeadDistance = 3f;
    [Tooltip("Full width of this corruption surface in world units.")]
    [SerializeField] private float _surfaceWidth = 10f;
    [Tooltip("World X position of the left edge of this corruption surface.")]
    [SerializeField] private float _clearStartX = 0f;

    [Header("Light Response (BeamTriggered mode only)")]
    [SerializeField] private EnhancedLanternController.LightEffect[] _acceptedEffects =
        { EnhancedLanternController.LightEffect.Reveal };
    [SerializeField] private float _minimumIntensity = 0.2f;

    [Header("Particles")]
    [Tooltip("Looping ParticleSystem for ambient corruption atmosphere. Stops when fully cleared.")]
    [SerializeField] private ParticleSystem _corruptionParticles;
    [Tooltip("One-shot burst played when corruption is fully cleared.")]
    [SerializeField] private ParticleSystem _clearBurstParticles;
    [Tooltip("Emission rate multiplier during active clearing.")]
    [SerializeField] private float _clearingEmissionMultiplier = 3f;

    [Header("Optional Reveal Light")]
    [Tooltip("URP 2D point light that fades in as corruption clears. Leave empty if not needed.")]
    [SerializeField] private Light2D _revealLight;
    [SerializeField] private float _revealLightMaxIntensity = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    #endregion

    #region ILightInteractable

    public bool IsCurrentlyIlluminated { get; private set; }
    public EnhancedLanternController.LightEffect CurrentActiveEffect { get; private set; }

    #endregion

    #region Private State

    private MaterialPropertyBlock _propertyBlock;

    private float _dissolveAmount = 0f;  // 0 = fully corrupt, 1 = fully clear
    private bool _isFullyCleared = false;
    private bool _isBeingCleared = false;

    private float _baseParticleEmission = 0f;

    #endregion

    #region Events

    /// <summary>
    /// Fired once when fully cleared. Subscribe from LightReceptorGate or narrative triggers.
    /// </summary>
    public System.Action OnFullyCleared;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_corruptionRenderer == null)
        {
            Debug.LogError($"❌ CorruptionLayer '{name}': _corruptionRenderer is not assigned. " +
                           "Drag your corruption art SpriteRenderer into this field.");
            enabled = false;
            return;
        }

        _propertyBlock = new MaterialPropertyBlock();

        if (_corruptionParticles != null)
        {
            var emission = _corruptionParticles.emission;
            _baseParticleEmission = emission.rateOverTime.constant;
        }

        SetDissolveAmount(0f);

        if (_debugMode)
            Debug.Log($"✓ CorruptionLayer '{name}' initialised | Mode: {_clearingMode}");
    }

    private void Start()
    {
        if (_clearingMode == ClearingMode.ProximityBased && _player == null)
        {
            var pm = FindObjectOfType<PlayerMovement>();
            if (pm != null)
                _player = pm.transform;
            else
                Debug.LogWarning($"⚠️ CorruptionLayer '{name}': ProximityBased but no player found.");
        }

        if (_corruptionParticles != null && !_corruptionParticles.isPlaying)
            _corruptionParticles.Play();

        if (_revealLight != null)
            _revealLight.intensity = 0f;
    }

    private void Update()
    {
        if (_isFullyCleared) return;

        if (_clearingMode == ClearingMode.ProximityBased)
            UpdateProximityClearing();
        else
            UpdateBeamClearing();

        UpdateParticleEmission();
        UpdateRevealLight();
    }

    #endregion

    #region Clearing Logic

    private void UpdateProximityClearing()
    {
        if (_player == null) return;

        // Map player X (plus lead distance) to 0–1 progress across the surface width.
        // Player walks left to right; clearing follows just ahead.
        float playerEffectiveX = _player.position.x + _clearLeadDistance;
        float targetDissolve = Mathf.InverseLerp(_clearStartX, _clearStartX + _surfaceWidth, playerEffectiveX);
        targetDissolve = Mathf.Clamp01(targetDissolve);

        // Only move forward — proximity clearing never retreats
        if (targetDissolve > _dissolveAmount)
        {
            float newAmount = Mathf.MoveTowards(_dissolveAmount, targetDissolve, _clearSpeed * Time.deltaTime);
            SetDissolveAmount(newAmount);
        }

        if (_dissolveAmount >= 1f)
            TriggerFullyClear();
    }

    private void UpdateBeamClearing()
    {
        if (_isBeingCleared)
        {
            float newAmount = Mathf.MoveTowards(_dissolveAmount, 1f, _clearSpeed * Time.deltaTime);
            SetDissolveAmount(newAmount);

            if (_dissolveAmount >= 1f)
                TriggerFullyClear();
        }
        else if (!_permanentClearing && _dissolveAmount > 0f)
        {
            float newAmount = Mathf.MoveTowards(_dissolveAmount, 0f, _returnSpeed * Time.deltaTime);
            SetDissolveAmount(newAmount);
        }
    }

    private void TriggerFullyClear()
    {
        if (_isFullyCleared) return;

        _isFullyCleared = true;
        SetDissolveAmount(1f);

        if (_corruptionParticles != null)
            _corruptionParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (_clearBurstParticles != null)
            _clearBurstParticles.Play();

        if (_revealLight != null)
            _revealLight.intensity = _revealLightMaxIntensity;

        OnFullyCleared?.Invoke();

        if (_debugMode)
            Debug.Log($"✨ CorruptionLayer '{name}' fully cleared");
    }

    #endregion

    #region Shader / Visual

    private void SetDissolveAmount(float amount)
    {
        _dissolveAmount = Mathf.Clamp01(amount);

        // MaterialPropertyBlock — shared material asset, per-instance property values.
        // No material instancing; all corruption objects can share one material.
        _corruptionRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(_dissolvePropertyName, _dissolveAmount);
        _corruptionRenderer.SetPropertyBlock(_propertyBlock);
    }

    private void UpdateParticleEmission()
    {
        if (_corruptionParticles == null || _isFullyCleared) return;

        var emission = _corruptionParticles.emission;

        if (_isBeingCleared || (_clearingMode == ClearingMode.ProximityBased && _dissolveAmount > 0f))
            emission.rateOverTime = _baseParticleEmission * _clearingEmissionMultiplier;
        else
            emission.rateOverTime = _baseParticleEmission * (1f - _dissolveAmount);
    }

    private void UpdateRevealLight()
    {
        if (_revealLight == null) return;
        _revealLight.intensity = _dissolveAmount * _revealLightMaxIntensity;
    }

    #endregion

    #region ILightInteractable Implementation

    public void OnLightEnter(EnhancedLanternController.LightEffect effect, float intensity, Vector2 direction)
    {
        if (_clearingMode != ClearingMode.BeamTriggered) return;
        if (!RespondsToEffect(effect) || intensity < _minimumIntensity) return;
        if (_isFullyCleared) return;

        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;
        _isBeingCleared = true;

        if (_debugMode)
            Debug.Log($"💡 Corruption '{name}' — beam entered, clearing");
    }

    public void OnLightStay(EnhancedLanternController.LightEffect effect, float intensity,
        Vector2 direction, float deltaTime)
    {
        if (_clearingMode != ClearingMode.BeamTriggered || _isFullyCleared) return;
        IsCurrentlyIlluminated = true;
    }

    public void OnLightExit(EnhancedLanternController.LightEffect effect)
    {
        if (_clearingMode != ClearingMode.BeamTriggered) return;

        IsCurrentlyIlluminated = false;
        _isBeingCleared = false;

        if (_debugMode && !_isFullyCleared)
            Debug.Log($"🌑 Corruption '{name}' — beam left" +
                      (_permanentClearing ? " (holding)" : " (returning)"));
    }

    public bool RespondsToEffect(EnhancedLanternController.LightEffect effect)
    {
        foreach (var accepted in _acceptedEffects)
            if (accepted == effect) return true;
        return false;
    }

    public float GetMinimumIntensity(EnhancedLanternController.LightEffect effect)
    {
        return _minimumIntensity;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Force-clear without beam or proximity. Use for cutscenes or editor testing.
    /// </summary>
    public void ForceClear() => TriggerFullyClear();

    public float ClearProgress => _dissolveAmount;
    public bool IsFullyCleared => _isFullyCleared;

    #endregion

    #region Debug Visualisation

    private void OnDrawGizmosSelected()
    {
        if (_clearingMode != ClearingMode.ProximityBased) return;

        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.4f);
        Vector3 surfaceCenter = new Vector3(_clearStartX + _surfaceWidth * 0.5f, transform.position.y, 0f);
        Gizmos.DrawWireCube(surfaceCenter, new Vector3(_surfaceWidth, 1f, 0f));

        if (Application.isPlaying && _player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(_player.position.x, transform.position.y - 0.5f, 0f),
                new Vector3(_player.position.x + _clearLeadDistance, transform.position.y - 0.5f, 0f)
            );
        }
    }

    #endregion
}
