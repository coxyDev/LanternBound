using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// LanternBound Player Aura System
/// Represents the mystical bond between player and lantern
/// 
/// PRE-LANTERN: Tiny survival glow (navigation only)
/// POST-LANTERN: Growing aura that reflects lantern mastery
/// BONDED CONCEPT: Aura only exists through lantern connection
/// </summary>
public class LanternBoundPlayerAura : MonoBehaviour
{
    [Header("Survival Glow (Pre-Lantern)")]
    [SerializeField] private float _survivalGlowIntensity = 1f;
    [SerializeField] private float _survivalGlowRadius = 1.5f;
    [SerializeField] private Color _survivalGlowColor = new Color(0.8f, 0.7f, 0.6f, 0.4f);

    [Header("Bonded Aura Settings (Post-Lantern)")]
    [SerializeField] private float _baseBondedIntensity = 0.15f;
    [SerializeField] private float _maxBondedIntensity = 0.8f; // Weaker than lantern
    [SerializeField] private float _baseBondedRadius = 0.8f;
    [SerializeField] private float _maxBondedRadius = 2.2f;
    [SerializeField] private Color _bondedGlowColor = new Color(1f, 0.95f, 0.8f, 0.7f);

    [Header("Bond Strength Progression")]
    [SerializeField] private float _abilityUsageWeight = 0.4f; // Active usage most important
    [SerializeField] private float _abilityDiscoveryWeight = 0.3f; // Discovery second
    [SerializeField] private float _upgradeWeight = 0.2f; // Upgrades support bond
    [SerializeField] private float _lanternTimeWeight = 0.1f; // Time bonded least important

    [Header("Bond Decay")]
    [SerializeField] private bool _enableBondDecay = true;
    [SerializeField] private float _decayRate = 0.1f; // Bond weakens without usage
    [SerializeField] private float _minimumBondStrength = 0.2f; // Never goes below base

    [Header("Visual Effects")]
    [SerializeField] private bool _enablePulsing = true;
    [SerializeField] private float _pulseSpeed = 1.2f;
    [SerializeField] private float _pulseStrength = 0.2f;
    [SerializeField] private bool _showBondingEffect = true;

    [Header("Component References")]
    [SerializeField] private Light2D _auraLight2D;
    [SerializeField] private SpriteRenderer _playerRenderer;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _showBondInfo = true;

    // Component references
    private DualProgressionSystem _progressionSystem;
    private EnhancedLanternController _lanternController;

    // Bond state
    private bool _isLanternBonded = false;
    private float _bondStrength = 0f; // 0-1 representing connection strength
    private float _targetBondStrength = 0f;
    private float _timeBonded = 0f;

    // Usage tracking
    private int _totalAbilityUsages = 0;
    private int _discoveredAbilities = 0;
    private int _purchasedUpgrades = 0;
    private float _lastUsageTime = 0f;

    // Visual state
    private float _currentIntensity;
    private float _currentRadius;
    private Coroutine _pulseCoroutine;
    private Coroutine _bondingEffectCoroutine;

    private void Awake()
    {
        SetupComponents();
        SetupProgressionTracking();
    }

    private void Start()
    {
        InitializeSurvivalGlow();
    }

    private void Update()
    {
        UpdateBondStrength();
        UpdateAuraVisuals();
    }

    #region Setup and Initialization

    private void SetupComponents()
    {
        // Get player renderer
        if (_playerRenderer == null)
            _playerRenderer = GetComponent<SpriteRenderer>();

        // Setup aura light
        if (_auraLight2D == null)
        {
            var lightObj = new GameObject("LanternBoundAura");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _auraLight2D = lightObj.AddComponent<Light2D>();
        }

        ConfigureLightComponent();

        if (_debugMode)
            Debug.Log("✓ LanternBound Player Aura components setup");
    }

    private void ConfigureLightComponent()
    {
        _auraLight2D.lightType = Light2D.LightType.Point;
        _auraLight2D.pointLightInnerRadius = 0.05f;
        _auraLight2D.enabled = true;

        // Start with survival glow settings
        _auraLight2D.intensity = _survivalGlowIntensity;
        _auraLight2D.pointLightOuterRadius = _survivalGlowRadius;
        _auraLight2D.color = _survivalGlowColor;
    }

    private void SetupProgressionTracking()
    {
        _progressionSystem = GetComponent<DualProgressionSystem>();
        _lanternController = GetComponent<EnhancedLanternController>();

        // Subscribe to lantern bonding
        if (_lanternController != null)
        {
            _lanternController.OnLanternAcquired += OnLanternBonded;
        }

        // Subscribe to progression events for bond strength
        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityUsed += OnAbilityUsed;
            _progressionSystem.OnAbilityDiscovered += OnAbilityDiscovered;
            _progressionSystem.OnUpgradePurchased += OnUpgradePurchased;
        }
    }

    private void InitializeSurvivalGlow()
    {
        // Start with minimal survival glow
        _currentIntensity = _survivalGlowIntensity;
        _currentRadius = _survivalGlowRadius;

        if (_debugMode)
            Debug.Log("✓ Survival glow active - seeking the lantern...");
    }

    #endregion

    #region Lantern Bonding

    private void OnLanternBonded()
    {
        if (_isLanternBonded) return;

        _isLanternBonded = true;
        _timeBonded = 0f;

        // Start bonding visual effect
        if (_showBondingEffect)
            _bondingEffectCoroutine = StartCoroutine(BondingEffect());

        // Start pulsing for bonded aura
        if (_enablePulsing)
            _pulseCoroutine = StartCoroutine(PulsingBondedAura());

        // Transition to bonded aura
        CalculateBondStrength();

        if (_debugMode)
            Debug.Log("🔗 Lantern bonded! Player aura awakened!");
    }

    private IEnumerator BondingEffect()
    {
        // Dramatic visual effect when bonding occurs
        float duration = 2f;
        float originalIntensity = _currentIntensity;

        // Surge of light as bond forms
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float progress = t / duration;
            float surgeIntensity = originalIntensity + (Mathf.Sin(progress * Mathf.PI * 3f) * 0.3f);
            _auraLight2D.intensity = surgeIntensity;

            // Color shift from survival to bonded
            Color currentColor = Color.Lerp(_survivalGlowColor, _bondedGlowColor, progress);
            _auraLight2D.color = currentColor;

            yield return null;
        }

        if (_debugMode)
            Debug.Log("✨ Bonding effect complete - mystical connection established");
    }

    #endregion

    #region Bond Strength Calculation

    private void CalculateBondStrength()
    {
        if (!_isLanternBonded)
        {
            _targetBondStrength = 0f;
            return;
        }

        // Calculate bond strength from multiple factors
        float usageContribution = Mathf.Clamp01(_totalAbilityUsages / 50f) * _abilityUsageWeight;
        float discoveryContribution = Mathf.Clamp01(_discoveredAbilities / 8f) * _abilityDiscoveryWeight;
        float upgradeContribution = Mathf.Clamp01(_purchasedUpgrades / 12f) * _upgradeWeight;
        float timeContribution = Mathf.Clamp01(_timeBonded / 300f) * _lanternTimeWeight; // 5 minutes

        float rawBondStrength = usageContribution + discoveryContribution + upgradeContribution + timeContribution;

        // Apply decay if no recent usage
        if (_enableBondDecay && Time.time - _lastUsageTime > 30f) // 30 seconds
        {
            float decayAmount = _decayRate * Time.deltaTime;
            rawBondStrength = Mathf.Max(_minimumBondStrength, rawBondStrength - decayAmount);
        }

        _targetBondStrength = Mathf.Clamp01(rawBondStrength);

        if (_debugMode && _showBondInfo)
        {
            Debug.Log($"🔗 Bond: {_targetBondStrength:F2} | Usage:{usageContribution:F2} Discovery:{discoveryContribution:F2} Upgrades:{upgradeContribution:F2} Time:{timeContribution:F2}");
        }
    }

    private void UpdateBondStrength()
    {
        if (_isLanternBonded)
        {
            _timeBonded += Time.deltaTime;

            // Smoothly interpolate bond strength
            _bondStrength = Mathf.Lerp(_bondStrength, _targetBondStrength, 2f * Time.deltaTime);

            // Recalculate periodically
            if (Time.frameCount % 60 == 0) // Once per second at 60fps
                CalculateBondStrength();
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateAuraVisuals()
    {
        if (!_isLanternBonded)
        {
            // Keep survival glow static
            return;
        }

        // Calculate bonded aura properties based on bond strength
        _currentIntensity = Mathf.Lerp(_baseBondedIntensity, _maxBondedIntensity, _bondStrength);
        _currentRadius = Mathf.Lerp(_baseBondedRadius, _maxBondedRadius, _bondStrength);

        // Apply to light component (pulsing handled separately)
        if (_pulseCoroutine == null) // Only if not pulsing
        {
            _auraLight2D.intensity = _currentIntensity;
        }
        _auraLight2D.pointLightOuterRadius = _currentRadius;
        _auraLight2D.color = Color.Lerp(_survivalGlowColor, _bondedGlowColor, _bondStrength);
    }

    private IEnumerator PulsingBondedAura()
    {
        while (_isLanternBonded && _enablePulsing)
        {
            float pulsePhase = Mathf.Sin(Time.time * _pulseSpeed) * _pulseStrength;
            float pulsedIntensity = _currentIntensity + (pulsePhase * _currentIntensity);

            _auraLight2D.intensity = Mathf.Max(0f, pulsedIntensity);

            yield return null;
        }
    }

    #endregion

    #region Event Handlers

    private void OnAbilityUsed(LightAbility ability)
    {
        if (!_isLanternBonded) return;

        _totalAbilityUsages++;
        _lastUsageTime = Time.time;

        // Trigger brief intensity surge for ability usage
        StartCoroutine(AbilityUsageSurge());

        if (_debugMode)
            Debug.Log($"🔗 Bond strengthened by ability usage: {ability.DisplayName} (Total: {_totalAbilityUsages})");
    }

    private void OnAbilityDiscovered(LightAbility ability)
    {
        if (!_isLanternBonded) return;

        _discoveredAbilities++;

        // Major bond strengthening effect
        StartCoroutine(BondStrengtheningEffect(1.5f, 1.8f));

        if (_debugMode)
            Debug.Log($"🔗 Bond deepened by ability discovery: {ability.DisplayName}");
    }

    private void OnUpgradePurchased(PassiveUpgrade upgrade)
    {
        if (!_isLanternBonded) return;

        _purchasedUpgrades++;

        // Moderate bond strengthening effect
        StartCoroutine(BondStrengtheningEffect(1f, 1.4f));

        if (_debugMode)
            Debug.Log($"🔗 Bond enhanced by upgrade: {upgrade.DisplayName}");
    }

    private IEnumerator AbilityUsageSurge()
    {
        float originalIntensity = _currentIntensity;
        float surgeIntensity = originalIntensity * 1.3f;

        // Quick surge up
        for (float t = 0; t < 0.1f; t += Time.deltaTime)
        {
            float intensity = Mathf.Lerp(originalIntensity, surgeIntensity, t / 0.1f);
            if (_pulseCoroutine == null)
                _auraLight2D.intensity = intensity;
            yield return null;
        }

        // Fade back down
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            float intensity = Mathf.Lerp(surgeIntensity, originalIntensity, t / 0.3f);
            if (_pulseCoroutine == null)
                _auraLight2D.intensity = intensity;
            yield return null;
        }
    }

    private IEnumerator BondStrengtheningEffect(float duration, float intensityMultiplier)
    {
        float originalIntensity = _currentIntensity;
        float targetIntensity = originalIntensity * intensityMultiplier;

        // Strengthen bond visual
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentMultiplier = Mathf.Lerp(intensityMultiplier, 1f, t);

            if (_pulseCoroutine == null)
                _auraLight2D.intensity = originalIntensity * currentMultiplier;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Check if player is bonded to lantern
    /// </summary>
    public bool IsLanternBonded => _isLanternBonded;

    /// <summary>
    /// Get current bond strength (0-1)
    /// </summary>
    public float BondStrength => _bondStrength;

    /// <summary>
    /// Get current aura intensity
    /// </summary>
    public float CurrentIntensity => _currentIntensity;

    /// <summary>
    /// Get current aura radius
    /// </summary>
    public float CurrentRadius => _currentRadius;

    /// <summary>
    /// Get time bonded with lantern
    /// </summary>
    public float TimeBonded => _timeBonded;

    /// <summary>
    /// Get total ability usages (bond activity measure)
    /// </summary>
    public int TotalAbilityUsages => _totalAbilityUsages;

    /// <summary>
    /// Force update bond strength calculation
    /// </summary>
    [ContextMenu("Force Update Bond")]
    public void ForceUpdateBond()
    {
        if (_isLanternBonded)
        {
            CalculateBondStrength();
            UpdateAuraVisuals();

            if (_debugMode)
                Debug.Log("🔄 Forced bond update complete");
        }
    }

    /// <summary>
    /// Debug method to simulate lantern loss (testing)
    /// </summary>
    [ContextMenu("Debug: Simulate Lantern Loss")]
    public void DebugSimulateLanternLoss()
    {
        if (Application.isPlaying && _isLanternBonded)
        {
            _isLanternBonded = false;
            _bondStrength = 0f;
            _targetBondStrength = 0f;

            // Return to survival glow
            _auraLight2D.intensity = _survivalGlowIntensity;
            _auraLight2D.pointLightOuterRadius = _survivalGlowRadius;
            _auraLight2D.color = _survivalGlowColor;

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }

            Debug.Log("🔗 Lantern bond severed - returned to survival glow");
        }
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_lanternController != null)
            _lanternController.OnLanternAcquired -= OnLanternBonded;

        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityUsed -= OnAbilityUsed;
            _progressionSystem.OnAbilityDiscovered -= OnAbilityDiscovered;
            _progressionSystem.OnUpgradePurchased -= OnUpgradePurchased;
        }
    }
}