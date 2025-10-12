using UnityEngine;
using System.Collections;

/// <summary>
/// LanternBound Particle Aura System
/// Visual manifestation of the mystical bond between player and lantern
/// 
/// BONDED CONCEPT: Particles only exist when lantern is bonded
/// PROGRESSION: Grows with bond strength and active lantern usage
/// NARRATIVE: Represents energy flowing between player and lantern
/// </summary>
public class LanternBondedParticleAura : MonoBehaviour
{
    private const ParticleSystemShapeType circleEdge = ParticleSystemShapeType.CircleEdge;
    [Header("Bond Particle Settings")]
    [SerializeField] private float _baseEmissionRate = 0f; // No particles until bonded
    [SerializeField] private float _bondedBaseEmission = 5f;
    [SerializeField] private float _maxBondedEmission = 30f;
    [SerializeField] private float _baseParticleSize = 0.04f;
    [SerializeField] private float _maxParticleSize = 0.15f;
    [SerializeField] private float _baseAuraRadius = 0.6f;
    [SerializeField] private float _maxAuraRadius = 2.8f;
    [SerializeField] private int _baseMaxParticles = 0; // No particles until bonded
    [SerializeField] private int _bondedBaseParticles = 12;
    [SerializeField] private int _maxBondedParticles = 60;

    [Header("Bond Visual Response")]
    [SerializeField] private bool _enableUsageBursts = true;
    [SerializeField] private bool _enableBondStreams = true; // Particles flow toward/from lantern
    [SerializeField] private bool _pulseWithBondStrength = true;
    [SerializeField] private float _pulseMagnitude = 0.4f;
    [SerializeField] private float _pulseSpeed = 1f;

    [Header("Lantern Connection Effects")]
    [SerializeField] private bool _enableLanternStreams = true;
    [SerializeField] private float _streamIntensity = 0.3f;
    [SerializeField] private float _streamSpeed = 2f;
    [SerializeField] private Transform _lanternTransform; // Auto-found if null

    [Header("Bond Strength Tiers")]
    [SerializeField]
    private BondTier[] _bondTiers = new BondTier[]
    {
        new BondTier { Name = "Tentative Connection", BondThreshold = 0f, EffectType = BondEffectType.None },
        new BondTier { Name = "Growing Bond", BondThreshold = 0.2f, EffectType = BondEffectType.Gentle },
        new BondTier { Name = "Strong Bond", BondThreshold = 0.4f, EffectType = BondEffectType.Flowing },
        new BondTier { Name = "Deep Bond", BondThreshold = 0.6f, EffectType = BondEffectType.Swirling },
        new BondTier { Name = "Soul Bond", BondThreshold = 0.8f, EffectType = BondEffectType.Radiant },
        new BondTier { Name = "Perfect Unity", BondThreshold = 1f, EffectType = BondEffectType.Transcendent }
    };

    [Header("Component References")]
    [SerializeField] private ParticleSystem _bondParticles;
    [SerializeField] private ParticleSystem _connectionStreams;
    [SerializeField] private LanternBoundPlayerAura _playerAura;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _showBondInfo = true;

    // Component references
    private DualProgressionSystem _progressionSystem;
    private EnhancedLanternController _lanternController;

    // Bond state
    private bool _isSystemActive = false;
    private float _currentBondStrength = 0f;
    private BondTier _currentTier;
    private int _lastUsageCount = 0;

    // Visual state
    private Coroutine _pulseCoroutine;
    private Coroutine _streamEffectCoroutine;
    private bool _particlesInitialized = false;

    [System.Serializable]
    public class BondTier
    {
        public string Name;
        [Range(0f, 1f)] public float BondThreshold;
        public BondEffectType EffectType;
    }

    public enum BondEffectType
    {
        None,
        Gentle,      // Slow floating particles
        Flowing,     // Directional movement
        Swirling,    // Orbital motion
        Radiant,     // Burst effects
        Transcendent // Complex layered effects
    }

    private void Awake()
    {
        SetupComponents();
        SetupBondTracking();
    }

    private void Start()
    {
        InitializeParticleSystem();
    }

    private void Update()
    {
        UpdateBondTracking();
        UpdateParticleEffects();
    }

    #region Setup and Initialization

    private void SetupComponents()
    {
        // Get or create main bond particle system
        if (_bondParticles == null)
        {
            var particleObj = new GameObject("LanternBondParticles");
            particleObj.transform.SetParent(transform);
            particleObj.transform.localPosition = Vector3.zero;
            _bondParticles = particleObj.AddComponent<ParticleSystem>();
        }

        // Get or create connection stream system
        if (_connectionStreams == null)
        {
            var streamObj = new GameObject("LanternConnectionStreams");
            streamObj.transform.SetParent(transform);
            streamObj.transform.localPosition = Vector3.zero;
            _connectionStreams = streamObj.AddComponent<ParticleSystem>();
        }

        // Get player aura reference
        if (_playerAura == null)
            _playerAura = GetComponent<LanternBoundPlayerAura>();

        // Find lantern transform if not assigned
        if (_lanternTransform == null)
        {
            var lanternController = GetComponent<EnhancedLanternController>();
            if (lanternController != null)
            {
                // Look for lantern child object
                _lanternTransform = lanternController.transform.Find("Lantern");
                if (_lanternTransform == null)
                {
                    // Create placeholder lantern position
                    var lanternObj = new GameObject("LanternPosition");
                    lanternObj.transform.SetParent(transform);
                    lanternObj.transform.localPosition = new Vector3(0.5f, 0.3f, 0f);
                    _lanternTransform = lanternObj.transform;
                }
            }
        }

        if (_debugMode)
            Debug.Log("✓ LanternBond Particle Aura components setup");
    }

    private void SetupBondTracking()
    {
        _progressionSystem = GetComponent<DualProgressionSystem>();
        _lanternController = GetComponent<EnhancedLanternController>();

        // Subscribe to lantern bonding
        if (_lanternController != null)
        {
            _lanternController.OnLanternAcquired += OnLanternBonded;
        }

        // Subscribe to usage events for burst effects
        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityUsed += OnAbilityUsed;
            _progressionSystem.OnAbilityDiscovered += OnAbilityDiscovered;
        }
    }

    private void InitializeParticleSystem()
    {
        ConfigureBondParticleSystem();
        ConfigureConnectionStreamSystem();

        // Start with particles disabled
        _bondParticles.Stop();
        _connectionStreams.Stop();

        _particlesInitialized = true;

        if (_debugMode)
            Debug.Log("✓ Bond particle systems initialized (inactive until bonded)");
    }

    private void ConfigureBondParticleSystem()
    {
        var main = _bondParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = _baseParticleSize;
        main.startColor = new Color(1f, 0.95f, 0.8f, 0.6f);
        main.maxParticles = _bondedBaseParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.prewarm = false;

        var emission = _bondParticles.emission;
        emission.rateOverTime = _baseEmissionRate; // Start at 0

        var shape = _bondParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = _baseAuraRadius;
        shape.radiusThickness = 0.7f;

        var velocityOverLifetime = _bondParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);

        var sizeOverLifetime = _bondParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(0.4f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = _bondParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient lifetimeGradient = new Gradient();
        lifetimeGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = lifetimeGradient;
    }

    private void ConfigureConnectionStreamSystem()
    {
        var main = _connectionStreams.main;
        main.startLifetime = 1.5f;
        main.startSpeed = 1f;
        main.startSize = 0.03f;
        main.startColor = new Color(1f, 1f, 0.9f, 0.7f);
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = _connectionStreams.emission;
        emission.rateOverTime = 0f; // Only used for directed streams

        var shape = _connectionStreams.shape;
        shape.shapeType = circleEdge;
        shape.radius = 0.1f;
    }

    #endregion

    #region Bond Tracking

    private void UpdateBondTracking()
    {
        if (!_isSystemActive || !_particlesInitialized) return;

        // Get current bond strength from player aura
        if (_playerAura != null && _playerAura.IsLanternBonded)
        {
            _currentBondStrength = _playerAura.BondStrength;
            UpdateBondTier();
            UpdateParticleConfiguration();
        }

        // Track usage for burst effects
        if (_playerAura != null)
        {
            int currentUsageCount = _playerAura.TotalAbilityUsages;
            if (currentUsageCount > _lastUsageCount)
            {
                _lastUsageCount = currentUsageCount;
                if (_enableUsageBursts)
                    TriggerUsageBurstInternal();
            }
        }
    }

    private void UpdateBondTier()
    {
        BondTier newTier = _bondTiers[0];

        for (int i = _bondTiers.Length - 1; i >= 0; i--)
        {
            if (_currentBondStrength >= _bondTiers[i].BondThreshold)
            {
                newTier = _bondTiers[i];
                break;
            }
        }

        if (_currentTier.Name != newTier.Name)
        {
            _currentTier = newTier;
            OnBondTierChanged();
        }
    }

    #endregion

    #region Particle Configuration

    private void UpdateParticleConfiguration()
    {
        if (!_isSystemActive || !_particlesInitialized) return;

        // Lerp particle properties based on bond strength
        float t = _currentBondStrength;

        // Emission rate
        float targetEmissionRate = Mathf.Lerp(_bondedBaseEmission, _maxBondedEmission, t);
        var emission = _bondParticles.emission;
        emission.rateOverTime = targetEmissionRate;

        // Particle size
        float targetSize = Mathf.Lerp(_baseParticleSize, _maxParticleSize, t);
        var main = _bondParticles.main;
        main.startSize = targetSize;

        // Max particles
        int targetMaxParticles = Mathf.RoundToInt(Mathf.Lerp(_bondedBaseParticles, _maxBondedParticles, t));
        main.maxParticles = targetMaxParticles;

        // Aura radius
        float targetRadius = Mathf.Lerp(_baseAuraRadius, _maxAuraRadius, t);
        var shape = _bondParticles.shape;
        shape.radius = targetRadius;

        // Color intensity based on bond strength
        Color bondColor = new Color(1f, 0.95f + (t * 0.05f), 0.8f + (t * 0.2f), 0.6f + (t * 0.3f));
        main.startColor = bondColor;

        // Velocity increases with stronger bond
        var velocity = _bondParticles.velocityOverLifetime;
        velocity.radial = new ParticleSystem.MinMaxCurve(0.2f + (t * 0.3f), 0.6f + (t * 0.8f));
    }

    #endregion

    #region Visual Effects

    private void UpdateParticleEffects()
    {
        if (!_isSystemActive || !_particlesInitialized) return;

        // Apply current tier's special effect
        ApplyBondEffect(_currentTier.EffectType);

        // Update connection streams if enabled
        if (_enableLanternStreams && _lanternTransform != null)
        {
            UpdateConnectionStreams();
        }
    }

    private void ApplyBondEffect(BondEffectType effectType)
    {
        switch (effectType)
        {
            case BondEffectType.Gentle:
                // Soft floating particles
                ApplyGentleEffect();
                break;

            case BondEffectType.Flowing:
                // Directional particle flow
                ApplyFlowingEffect();
                break;

            case BondEffectType.Swirling:
                // Orbital motion around player
                ApplySwirlingEffect();
                break;

            case BondEffectType.Radiant:
                // Periodic burst effects
                if (Random.value < 0.01f)
                    TriggerRadiantBurst();
                break;

            case BondEffectType.Transcendent:
                // Complex layered effects
                ApplyTranscendentEffect();
                break;
        }
    }

    private void ApplyGentleEffect()
    {
        // Subtle pulsing
        if (_pulseWithBondStrength && _pulseCoroutine == null)
            _pulseCoroutine = StartCoroutine(GentlePulsing());
    }

    private void ApplyFlowingEffect()
    {
        // Directional velocity toward/away from lantern
        if (_lanternTransform != null)
        {
            Vector3 directionToLantern = (_lanternTransform.position - transform.position).normalized;
            var velocityOverLifetime = _bondParticles.velocityOverLifetime;

            // Create flowing motion
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(directionToLantern.x * 0.2f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(directionToLantern.y * 0.2f);
        }
    }

    private void ApplySwirlingEffect()
    {
        // Orbital motion (requires more complex setup)
        var forceOverLifetime = _bondParticles.forceOverLifetime;
        forceOverLifetime.enabled = true;
        forceOverLifetime.space = ParticleSystemSimulationSpace.Local;

        // Create swirling force
        float swirl = Mathf.Sin(Time.time * _pulseSpeed) * _currentBondStrength;
        forceOverLifetime.x = new ParticleSystem.MinMaxCurve(swirl * 2f);
        forceOverLifetime.y = new ParticleSystem.MinMaxCurve(Mathf.Cos(Time.time * _pulseSpeed) * _currentBondStrength * 2f);
    }

    private void ApplyTranscendentEffect()
    {
        // Combination of all effects
        ApplySwirlingEffect();

        if (Random.value < 0.02f)
            TriggerRadiantBurst();

        if (Random.value < 0.005f)
            TriggerConnectionPulse();
    }

    private void UpdateConnectionStreams()
    {
        if (_currentBondStrength < 0.3f) return; // Only show streams at higher bond levels

        // Emit stream particles toward lantern
        if (Random.value < _streamIntensity * _currentBondStrength)
        {
            Vector3 directionToLantern = (_lanternTransform.position - transform.position).normalized;

            _connectionStreams.Emit(new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                velocity = directionToLantern * _streamSpeed,
                startLifetime = 1.5f,
                startSize = 0.04f,
                startColor = new Color(1f, 1f, 0.9f, _currentBondStrength)
            }, 1);
        }
    }

    private IEnumerator GentlePulsing()
    {
        while (_isSystemActive && _currentTier.EffectType == BondEffectType.Gentle)
        {
            float pulseValue = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
            float pulsedEmission = _bondedBaseEmission * (1f + (pulseValue * _pulseMagnitude));

            var emission = _bondParticles.emission;
            emission.rateOverTime = pulsedEmission;

            yield return null;
        }

        _pulseCoroutine = null;
    }

    private void TriggerUsageBurstInternal()
    {
        if (!_isSystemActive) return;

        // Burst of particles when abilities are used
        int burstCount = Mathf.RoundToInt(5 + (_currentBondStrength * 10));
        _bondParticles.Emit(burstCount);

        if (_debugMode)
            Debug.Log($"🔗 Usage burst triggered: {burstCount} particles");
    }

    private void TriggerRadiantBurst()
    {
        // Dramatic burst effect for high bond levels
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = new Vector3(
                Mathf.Cos(i * Mathf.PI * 2f / 8f),
                Mathf.Sin(i * Mathf.PI * 2f / 8f),
                0f
            );

            _bondParticles.Emit(new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                velocity = direction * 2f,
                startLifetime = 2f,
                startSize = _maxParticleSize,
                startColor = new Color(1f, 1f, 1f, _currentBondStrength)
            }, 1);
        }
    }

    private void TriggerConnectionPulse()
    {
        if (_lanternTransform == null) return;

        // Pulse of energy between player and lantern
        StartCoroutine(ConnectionPulseEffect());
    }

    private IEnumerator ConnectionPulseEffect()
    {
        Vector3 lanternDirection = (_lanternTransform.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, _lanternTransform.position);

        // Emit particles along the connection line
        for (int i = 0; i < 10; i++)
        {
            Vector3 position = Vector3.Lerp(Vector3.zero, lanternDirection * distance, i / 9f);

            _connectionStreams.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = 1f,
                startSize = 0.06f,
                startColor = new Color(1f, 1f, 1f, 0.9f)
            }, 1);

            yield return new WaitForSeconds(0.05f);
        }
    }

    #endregion

    #region Event Handlers

    private void OnLanternBonded()
    {
        _isSystemActive = true;

        // Start bond particle system
        _bondParticles.Play();
        if (_enableLanternStreams)
            _connectionStreams.Play();

        // Start visual effects
        StartCoroutine(BondFormationEffect());

        if (_debugMode)
            Debug.Log("🔗 Bond particle system activated!");
    }

    private void OnAbilityUsed(LightAbility ability)
    {
        if (_enableUsageBursts)
            TriggerUsageBurstInternal();
    }

    private void OnAbilityDiscovered(LightAbility ability)
    {
        // Major bond strengthening visual
        StartCoroutine(BondStrengtheningEffect());
    }

    private void OnBondTierChanged()
    {
        if (_debugMode)
            Debug.Log($"✨ Bond tier advanced: {_currentTier.Name} - {_currentTier.EffectType}");

        // Visual celebration of tier advancement
        StartCoroutine(TierAdvancementEffect());
    }

    private IEnumerator BondFormationEffect()
    {
        // Dramatic effect when bond first forms
        for (int i = 0; i < 3; i++)
        {
            TriggerRadiantBurst();
            yield return new WaitForSeconds(0.3f);
        }

        if (_enableLanternStreams)
            TriggerConnectionPulse();
    }

    private IEnumerator BondStrengtheningEffect()
    {
        // Enhanced particle emission for bond strengthening
        var emission = _bondParticles.emission;
        float originalRate = emission.rateOverTime.constant;

        emission.rateOverTime = originalRate * 4f;
        TriggerRadiantBurst();

        yield return new WaitForSeconds(2f);

        emission.rateOverTime = originalRate;
    }

    private IEnumerator TierAdvancementEffect()
    {
        // Major celebration for tier advancement
        for (int i = 0; i < 5; i++)
        {
            TriggerRadiantBurst();
            if (_enableLanternStreams)
                TriggerConnectionPulse();
            yield return new WaitForSeconds(0.4f);
        }

        // Update particle configuration for new tier
        UpdateParticleConfiguration();
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Check if bond particle system is active
    /// </summary>
    public bool IsSystemActive => _isSystemActive;

    /// <summary>
    /// Get current bond strength being visualized
    /// </summary>
    public float CurrentBondStrength => _currentBondStrength;

    /// <summary>
    /// Get current bond tier
    /// </summary>
    public BondTier CurrentTier => _currentTier;

    /// <summary>
    /// Manually trigger a usage burst (for testing)
    /// </summary>
    public void TriggerUsageBurst()
    {
        if (_isSystemActive && _enableUsageBursts)
            TriggerUsageBurstInternal();
    }

    /// <summary>
    /// Force update particle configuration
    /// </summary>
    [ContextMenu("Force Update Particles")]
    public void ForceUpdateParticles()
    {
        if (_isSystemActive && _particlesInitialized)
        {
            UpdateParticleConfiguration();

            if (_debugMode)
                Debug.Log("🔄 Forced particle update complete");
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
        }
    }
}