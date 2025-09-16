using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Master integration system that connects all LanternBound systems together
/// Handles system initialization, ability integration, and gameplay coordination
/// </summary>
public class LanternBoundSystemIntegrator : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private EnhancedLanternController _lanternController;
    [SerializeField] private EnhancedLightEffectsController _lightEffectsController;
    [SerializeField] private DualProgressionSystem _progressionSystem;
    [SerializeField] private SolarFlareAbility _solarFlareAbility;

    [Header("Integration Settings")]
    [SerializeField] private bool _autoDiscoverComponents = true;
    [SerializeField] private bool _enableSystemCommunication = true;
    [SerializeField] private bool _debugSystemStatus = true;

    [Header("Ability Integration")]
    [SerializeField] private List<AbilityBinding> _abilityBindings = new List<AbilityBinding>();

    [Header("Event Coordination")]
    [SerializeField] private bool _coordinateAudioEvents = true;
    [SerializeField] private bool _coordinateVisualEffects = true;
    [SerializeField] private bool _coordinatePuzzleEvents = true;

    [Header("Performance Settings")]
    [SerializeField] private float _systemUpdateRate = 0.1f;
    [SerializeField] private int _maxSimultaneousEffects = 10;

    [System.Serializable]
    public class AbilityBinding
    {
        public string abilityId;
        public KeyCode activationKey;
        public LightEffect primaryEffect;
        public bool requiresCharging;
        public float manaCost;
        public float cooldownTime;
        public MonoBehaviour abilityScript; // Reference to the actual ability script
    }

    // System state tracking
    public bool AllSystemsReady { get; private set; }
    public bool PlayerHasLantern { get; private set; }
    public int ActiveAbilitiesCount { get; private set; }
    public Dictionary<string, float> SystemPerformanceMetrics { get; private set; }

    // Event coordination
    private Queue<GameplayEvent> _eventQueue;
    private Dictionary<LightEffect, List<ILightInteractable>> _effectTargets;
    private Coroutine _systemUpdateCoroutine;

    [System.Serializable]
    public class GameplayEvent
    {
        public GameplayEventType eventType;
        public Vector3 position;
        public float intensity;
        public string targetId;
        public object data;
        public float timestamp;

        public enum GameplayEventType
        {
            AbilityActivated,
            NodeActivated,
            EnemyStunned,
            GateOpened,
            LightEffectTriggered,
            PuzzleSolved,
            EssenceGained
        }
    }

    private void Awake()
    {
        InitializeIntegrationSystems();

        if (_autoDiscoverComponents)
        {
            DiscoverSystemComponents();
        }

        ValidateSystemSetup();
        SetupEventCoordination();
    }

    private void Start()
    {
        StartSystemIntegration();
        _systemUpdateCoroutine = StartCoroutine(SystemUpdateLoop());

        if (_debugSystemStatus)
        {
            Debug.Log("🔗 LanternBound System Integrator started");
            LogSystemStatus();
        }
    }

    private void OnDestroy()
    {
        if (_systemUpdateCoroutine != null)
        {
            StopCoroutine(_systemUpdateCoroutine);
        }

        UnsubscribeFromEvents();
    }

    #region System Discovery and Setup

    private void InitializeIntegrationSystems()
    {
        SystemPerformanceMetrics = new Dictionary<string, float>();
        _eventQueue = new Queue<GameplayEvent>();
        _effectTargets = new Dictionary<LightEffect, List<ILightInteractable>>();

        // Initialize effect target tracking
        var allEffects = System.Enum.GetValues(typeof(LightEffect));
        foreach (LightEffect effect in allEffects)
        {
            _effectTargets[effect] = new List<ILightInteractable>();
        }
    }

    private void DiscoverSystemComponents()
    {
        // Auto-discover core systems
        if (_lanternController == null)
            _lanternController = FindObjectOfType<EnhancedLanternController>();

        if (_lightEffectsController == null)
            _lightEffectsController = FindObjectOfType<EnhancedLightEffectsController>();

        if (_progressionSystem == null)
            _progressionSystem = FindObjectOfType<DualProgressionSystem>();

        if (_solarFlareAbility == null)
            _solarFlareAbility = FindObjectOfType<SolarFlareAbility>();

        // Auto-discover ability bindings
        DiscoverAbilityBindings();

        if (_debugSystemStatus)
        {
            Debug.Log($"🔍 System Discovery Complete:");
            Debug.Log($"  Lantern Controller: {(_lanternController != null ? "✓" : "❌")}");
            Debug.Log($"  Light Effects Controller: {(_lightEffectsController != null ? "✓" : "❌")}");
            Debug.Log($"  Progression System: {(_progressionSystem != null ? "✓" : "❌")}");
            Debug.Log($"  Solar Flare Ability: {(_solarFlareAbility != null ? "✓" : "❌")}");
            Debug.Log($"  Ability Bindings: {_abilityBindings.Count}");
        }
    }

    private void DiscoverAbilityBindings()
    {
        _abilityBindings.Clear();

        // Solar Flare binding
        if (_solarFlareAbility != null)
        {
            _abilityBindings.Add(new AbilityBinding
            {
                abilityId = "solar_flare",
                activationKey = KeyCode.Q,
                primaryEffect = LightEffect.Stun,
                requiresCharging = true,
                manaCost = 25f,
                cooldownTime = 8f,
                abilityScript = _solarFlareAbility
            });
        }

        // Add more abilities as they're discovered
        // This is where Prism Beam, Glowing Rift, etc. would be added
    }

    private void ValidateSystemSetup()
    {
        List<string> missingComponents = new List<string>();

        if (_lanternController == null)
            missingComponents.Add("EnhancedLanternController");

        if (_lightEffectsController == null)
            missingComponents.Add("EnhancedLightEffectsController");

        if (_progressionSystem == null)
            missingComponents.Add("DualProgressionSystem");

        AllSystemsReady = missingComponents.Count == 0;

        if (!AllSystemsReady)
        {
            Debug.LogError($"❌ Missing critical components: {string.Join(", ", missingComponents)}");
        }
        else if (_debugSystemStatus)
        {
            Debug.Log("✅ All critical systems validated");
        }
    }

    #endregion

    #region Event Coordination Setup

    private void SetupEventCoordination()
    {
        if (!_enableSystemCommunication) return;

        // Subscribe to lantern events
        if (_lanternController != null)
        {
            _lanternController.OnLanternAcquired += HandleLanternAcquired;
            _lanternController.OnLightTypeChanged += HandleLightTypeChanged;
            _lanternController.OnObjectIlluminated += HandleObjectIlluminated;
            _lanternController.OnObjectLeftLight += HandleObjectLeftLight;
        }

        // Subscribe to progression events
        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityDiscovered += HandleAbilityDiscovered;
            _progressionSystem.OnUpgradePurchased += HandleUpgradePurchased;
            _progressionSystem.OnEssenceChanged += HandleEssenceChanged;
        }

        // Subscribe to solar flare events
        if (_solarFlareAbility != null)
        {
            _solarFlareAbility.OnSolarFlareCharged += HandleSolarFlareCharged;
            _solarFlareAbility.OnNodesActivated += HandleNodesActivated;
            _solarFlareAbility.OnEnemiesStunned += HandleEnemiesStunned;
        }

        // Subscribe to puzzle events
        LightPuzzleNode.OnNodeActivated += HandleNodeActivated;
        LightPuzzleNode.OnNodeDeactivated += HandleNodeDeactivated;
        MultiNodePuzzleGate.OnGateOpened += HandleGateOpened;
        MultiNodePuzzleGate.OnGateClosed += HandleGateClosed;

        // Subscribe to enemy events
        LurkerEnemy.OnLurkerStunned += HandleEnemyStunned;
        LurkerEnemy.OnLurkerDissolved += HandleEnemyDissolved;
        LurkerEnemy.OnLurkerDetectedPlayer += HandleEnemyDetectedPlayer;
    }

    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from all events to prevent memory leaks
        if (_lanternController != null)
        {
            _lanternController.OnLanternAcquired -= HandleLanternAcquired;
            _lanternController.OnLightTypeChanged -= HandleLightTypeChanged;
            _lanternController.OnObjectIlluminated -= HandleObjectIlluminated;
            _lanternController.OnObjectLeftLight -= HandleObjectLeftLight;
        }

        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityDiscovered -= HandleAbilityDiscovered;
            _progressionSystem.OnUpgradePurchased -= HandleUpgradePurchased;
            _progressionSystem.OnEssenceChanged -= HandleEssenceChanged;
        }

        if (_solarFlareAbility != null)
        {
            _solarFlareAbility.OnSolarFlareCharged -= HandleSolarFlareCharged;
            _solarFlareAbility.OnNodesActivated -= HandleNodesActivated;
            _solarFlareAbility.OnEnemiesStunned -= HandleEnemiesStunned;
        }

        LightPuzzleNode.OnNodeActivated -= HandleNodeActivated;
        LightPuzzleNode.OnNodeDeactivated -= HandleNodeDeactivated;
        MultiNodePuzzleGate.OnGateOpened -= HandleGateOpened;
        MultiNodePuzzleGate.OnGateClosed -= HandleGateClosed;

        LurkerEnemy.OnLurkerStunned -= HandleEnemyStunned;
        LurkerEnemy.OnLurkerDissolved -= HandleEnemyDissolved;
        LurkerEnemy.OnLurkerDetectedPlayer -= HandleEnemyDetectedPlayer;
    }

    #endregion

    #region System Integration

    private void StartSystemIntegration()
    {
        if (!AllSystemsReady) return;

        // Connect light effects controller to lantern controller
        if (_lightEffectsController != null && _lanternController != null)
        {
            // The light effects controller should track the lantern's state
            StartCoroutine(SynchronizeLightSystems());
        }

        // Initialize ability progression integration
        IntegrateAbilityProgression();

        // Setup default ability bindings
        SetupDefaultAbilities();

        if (_debugSystemStatus)
        {
            Debug.Log("🔗 System integration completed");
        }
    }

    private IEnumerator SynchronizeLightSystems()
    {
        while (true)
        {
            yield return new WaitForSeconds(_systemUpdateRate);

            if (_lanternController != null && _lightEffectsController != null)
            {
                // Update light effects based on lantern state
                if (_lanternController.IsLanternActive)
                {
                    var currentLightType = _lanternController.CurrentLightType;
                    var correspondingEffect = ConvertLightTypeToEffect(currentLightType);

                    if (_lightEffectsController.GetCurrentEffectData()?.effectType != correspondingEffect)
                    {
                        _lightEffectsController.SetLightEffect(correspondingEffect);
                    }
                }
            }
        }
    }

    private LightEffect ConvertLightTypeToEffect(EnhancedLanternController.LightType lightType)
    {
        return lightType switch
        {
            EnhancedLanternController.LightType.Ember => LightEffect.Reveal,
            EnhancedLanternController.LightType.Radiance => LightEffect.Energize,
            EnhancedLanternController.LightType.SolarFlare => LightEffect.Stun,
            EnhancedLanternController.LightType.MoonBeam => LightEffect.Reveal,
            EnhancedLanternController.LightType.Starlight => LightEffect.Purify,
            EnhancedLanternController.LightType.PrismaticLight => LightEffect.Refract,
            EnhancedLanternController.LightType.VoidLight => LightEffect.Stealth,
            _ => LightEffect.Reveal
        };
    }

    private void IntegrateAbilityProgression()
    {
        if (_progressionSystem == null) return;

        // Check if player already has abilities and sync them
        var discoveredAbilities = _progressionSystem.GetDiscoveredAbilities();

        foreach (var ability in discoveredAbilities)
        {
            EnableAbilityFromProgression(ability.AbilityId);
        }
    }

    private void SetupDefaultAbilities()
    {
        // Enable Solar Flare by default for testing
        if (_solarFlareAbility != null && _progressionSystem != null)
        {
            _progressionSystem.DiscoverAbility("solar_flare");
        }
    }

    #endregion

    #region System Update Loop

    private IEnumerator SystemUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(_systemUpdateRate);

            UpdateSystemMetrics();
            ProcessEventQueue();
            CheckSystemHealth();
            UpdateActiveAbilitiesCount();
        }
    }

    private void UpdateSystemMetrics()
    {
        SystemPerformanceMetrics["LanternActive"] = _lanternController != null && _lanternController.IsLanternActive ? 1f : 0f;
        SystemPerformanceMetrics["ManaPercentage"] = _lanternController?.ManaPercentage ?? 0f;
        SystemPerformanceMetrics["LightEssence"] = _progressionSystem?.GetLightEssence() ?? 0f;
        SystemPerformanceMetrics["DiscoveredAbilities"] = _progressionSystem?.GetDiscoveredAbilities().Count ?? 0f;
        SystemPerformanceMetrics["EventQueueSize"] = _eventQueue.Count;
    }

    private void ProcessEventQueue()
    {
        int eventsProcessed = 0;
        int maxEventsPerFrame = 5; // Prevent frame drops

        while (_eventQueue.Count > 0 && eventsProcessed < maxEventsPerFrame)
        {
            var gameEvent = _eventQueue.Dequeue();
            ProcessGameplayEvent(gameEvent);
            eventsProcessed++;
        }
    }

    private void CheckSystemHealth()
    {
        // Check if any critical systems have been destroyed
        if (_lanternController == null || _progressionSystem == null)
        {
            Debug.LogWarning("⚠️ Critical system component missing - attempting recovery");
            DiscoverSystemComponents();
        }
    }

    private void UpdateActiveAbilitiesCount()
    {
        ActiveAbilitiesCount = 0;

        foreach (var binding in _abilityBindings)
        {
            if (binding.abilityScript != null && binding.abilityScript.enabled)
            {
                ActiveAbilitiesCount++;
            }
        }
    }

    #endregion

    #region Event Handlers

    private void HandleLanternAcquired()
    {
        PlayerHasLantern = true;
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.LightEffectTriggered,
            position = transform.position,
            timestamp = Time.time
        });

        if (_debugSystemStatus)
            Debug.Log("🔦 Player acquired lantern - systems now fully active");
    }

    private void HandleLightTypeChanged(EnhancedLanternController.LightType newType)
    {
        // Update light effects controller to match
        if (_lightEffectsController != null)
        {
            var effect = ConvertLightTypeToEffect(newType);
            _lightEffectsController.SetLightEffect(effect);
        }

        if (_debugSystemStatus)
            Debug.Log($"💡 Light type changed to: {newType}");
    }

    private void HandleObjectIlluminated(ILightInteractable obj)
    {
        if (obj.CurrentActiveEffect != LightEffect.Reveal)
        {
            if (!_effectTargets[obj.CurrentActiveEffect].Contains(obj))
            {
                _effectTargets[obj.CurrentActiveEffect].Add(obj);
            }
        }
    }

    private void HandleObjectLeftLight(ILightInteractable obj)
    {
        foreach (var effectList in _effectTargets.Values)
        {
            effectList.Remove(obj);
        }
    }

    private void HandleAbilityDiscovered(LightAbility ability)
    {
        EnableAbilityFromProgression(ability.AbilityId);

        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.AbilityActivated,
            targetId = ability.AbilityId,
            timestamp = Time.time
        });

        if (_debugSystemStatus)
            Debug.Log($"⭐ New ability discovered: {ability.DisplayName}");
    }

    private void HandleUpgradePurchased(PassiveUpgrade upgrade)
    {
        if (_debugSystemStatus)
            Debug.Log($"📈 Upgrade purchased: {upgrade.DisplayName}");
    }

    private void HandleEssenceChanged(int newAmount)
    {
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.EssenceGained,
            data = newAmount,
            timestamp = Time.time
        });
    }

    private void HandleSolarFlareCharged()
    {
        if (_debugSystemStatus)
            Debug.Log("☀️ Solar Flare charged and ready");
    }

    private void HandleNodesActivated(int nodeCount)
    {
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.NodeActivated,
            data = nodeCount,
            timestamp = Time.time
        });

        if (_debugSystemStatus)
            Debug.Log($"🔵 Solar Flare activated {nodeCount} nodes");
    }

    private void HandleEnemiesStunned(int enemyCount)
    {
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.EnemyStunned,
            data = enemyCount,
            timestamp = Time.time
        });

        if (_debugSystemStatus)
            Debug.Log($"⚡ Solar Flare stunned {enemyCount} enemies");
    }

    private void HandleNodeActivated(LightPuzzleNode node)
    {
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.NodeActivated,
            position = node.transform.position,
            targetId = node.GetNodeId(),
            timestamp = Time.time
        });
    }

    private void HandleNodeDeactivated(LightPuzzleNode node)
    {
        // Handle node deactivation if needed
    }

    private void HandleGateOpened(MultiNodePuzzleGate gate)
    {
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.GateOpened,
            position = gate.transform.position,
            targetId = gate.GetGateId(),
            timestamp = Time.time
        });

        if (_debugSystemStatus)
            Debug.Log($"🚪 Gate opened: {gate.GetGateId()}");
    }

    private void HandleGateClosed(MultiNodePuzzleGate gate)
    {
        if (_debugSystemStatus)
            Debug.Log($"🚪 Gate closed: {gate.GetGateId()}");
    }

    private void HandleEnemyStunned(LurkerEnemy enemy)
    {
        QueueEvent(new GameplayEvent
        {
            eventType = GameplayEvent.GameplayEventType.EnemyStunned,
            position = enemy.transform.position,
            targetId = enemy.name,
            timestamp = Time.time
        });
    }

    private void HandleEnemyDissolved(LurkerEnemy enemy)
    {
        // Award bonus essence for dissolving enemy
        if (_progressionSystem != null)
        {
            _progressionSystem.AddLightEssence(5); // Bonus for defeating rather than just stunning
        }
    }

    private void HandleEnemyDetectedPlayer(LurkerEnemy enemy, Vector3 playerPosition)
    {
        if (_debugSystemStatus)
            Debug.Log($"👁️ Enemy detected player: {enemy.name}");
    }

    #endregion

    #region Event Processing

    private void QueueEvent(GameplayEvent gameEvent)
    {
        _eventQueue.Enqueue(gameEvent);
    }

    private void ProcessGameplayEvent(GameplayEvent gameEvent)
    {
        switch (gameEvent.eventType)
        {
            case GameplayEvent.GameplayEventType.AbilityActivated:
                ProcessAbilityActivated(gameEvent);
                break;

            case GameplayEvent.GameplayEventType.NodeActivated:
                ProcessNodeActivated(gameEvent);
                break;

            case GameplayEvent.GameplayEventType.EnemyStunned:
                ProcessEnemyStunned(gameEvent);
                break;

            case GameplayEvent.GameplayEventType.GateOpened:
                ProcessGateOpened(gameEvent);
                break;

            case GameplayEvent.GameplayEventType.PuzzleSolved:
                ProcessPuzzleSolved(gameEvent);
                break;

            case GameplayEvent.GameplayEventType.EssenceGained:
                ProcessEssenceGained(gameEvent);
                break;
        }
    }

    private void ProcessAbilityActivated(GameplayEvent gameEvent)
    {
        // Handle ability activation effects
        if (_coordinateVisualEffects)
        {
            // Could trigger screen effects, camera shake, etc.
        }
    }

    private void ProcessNodeActivated(GameplayEvent gameEvent)
    {
        // Check for puzzle completion patterns
        if (_coordinatePuzzleEvents)
        {
            CheckForPuzzleCompletion();
        }
    }

    private void ProcessEnemyStunned(GameplayEvent gameEvent)
    {
        // Award tactical bonus essence
        if (_progressionSystem != null)
        {
            _progressionSystem.AddLightEssence(1);
        }
    }

    private void ProcessGateOpened(GameplayEvent gameEvent)
    {
        // Award puzzle solving bonus
        if (_progressionSystem != null)
        {
            _progressionSystem.AddLightEssence(10);
        }
    }

    private void ProcessPuzzleSolved(GameplayEvent gameEvent)
    {
        // Handle puzzle completion rewards
    }

    private void ProcessEssenceGained(GameplayEvent gameEvent)
    {
        // Update UI, play effects, etc.
    }

    private void CheckForPuzzleCompletion()
    {
        // Complex puzzle completion detection logic would go here
        // For now, just log when multiple nodes are active
        var activeNodes = FindObjectsOfType<LightPuzzleNode>();
        int activeCount = 0;

        foreach (var node in activeNodes)
        {
            if (node.IsActive)
                activeCount++;
        }

        if (activeCount >= 3)
        {
            QueueEvent(new GameplayEvent
            {
                eventType = GameplayEvent.GameplayEventType.PuzzleSolved,
                data = activeCount,
                timestamp = Time.time
            });
        }
    }

    #endregion

    #region Ability Management

    private void EnableAbilityFromProgression(string abilityId)
    {
        foreach (var binding in _abilityBindings)
        {
            if (binding.abilityId == abilityId && binding.abilityScript != null)
            {
                binding.abilityScript.enabled = true;

                if (_debugSystemStatus)
                    Debug.Log($"🔓 Enabled ability: {abilityId}");
                break;
            }
        }
    }

    #endregion

    #region Public API

    public bool IsSystemReady(string systemName)
    {
        return systemName switch
        {
            "LanternController" => _lanternController != null,
            "LightEffects" => _lightEffectsController != null,
            "Progression" => _progressionSystem != null,
            "SolarFlare" => _solarFlareAbility != null,
            _ => false
        };
    }

    public List<ILightInteractable> GetTargetsOfEffect(LightEffect effect)
    {
        return _effectTargets.ContainsKey(effect) ? new List<ILightInteractable>(_effectTargets[effect]) : new List<ILightInteractable>();
    }

    public void ForceSystemSync()
    {
        if (_systemUpdateCoroutine != null)
        {
            StopCoroutine(_systemUpdateCoroutine);
        }

        UpdateSystemMetrics();
        ProcessEventQueue();
        CheckSystemHealth();

        _systemUpdateCoroutine = StartCoroutine(SystemUpdateLoop());
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Log System Status")]
    public void LogSystemStatus()
    {
        Debug.Log("=== LANTERNBOUND SYSTEM STATUS ===");
        Debug.Log($"All Systems Ready: {AllSystemsReady}");
        Debug.Log($"Player Has Lantern: {PlayerHasLantern}");
        Debug.Log($"Active Abilities: {ActiveAbilitiesCount}");
        Debug.Log($"Event Queue Size: {_eventQueue.Count}");

        Debug.Log("\nSystem Components:");
        Debug.Log($"  Lantern Controller: {(_lanternController != null ? "✓" : "❌")}");
        Debug.Log($"  Light Effects Controller: {(_lightEffectsController != null ? "✓" : "❌")}");
        Debug.Log($"  Progression System: {(_progressionSystem != null ? "✓" : "❌")}");
        Debug.Log($"  Solar Flare Ability: {(_solarFlareAbility != null ? "✓" : "❌")}");

        Debug.Log("\nPerformance Metrics:");
        foreach (var metric in SystemPerformanceMetrics)
        {
            Debug.Log($"  {metric.Key}: {metric.Value:F2}");
        }

        Debug.Log("\nAbility Bindings:");
        foreach (var binding in _abilityBindings)
        {
            bool isEnabled = binding.abilityScript != null && binding.abilityScript.enabled;
            Debug.Log($"  {binding.abilityId} ({binding.activationKey}): {(isEnabled ? "✓" : "❌")}");
        }
    }

    [ContextMenu("Test Solar Flare Integration")]
    public void TestSolarFlareIntegration()
    {
        if (_solarFlareAbility != null && Application.isPlaying)
        {
            _solarFlareAbility.TestSolarFlare();
        }
        else
        {
            Debug.LogWarning("Cannot test Solar Flare - ability not found or not in play mode");
        }
    }

    [ContextMenu("Force Event Queue Processing")]
    public void ForceEventQueueProcessing()
    {
        while (_eventQueue.Count > 0)
        {
            var gameEvent = _eventQueue.Dequeue();
            ProcessGameplayEvent(gameEvent);
        }
        Debug.Log("Event queue fully processed");
    }

    #endregion
}