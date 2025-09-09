using UnityEngine;
using System.Collections;

/// <summary>
/// Data structure for active light abilities that players discover in the world
/// </summary>
[System.Serializable]
public class LightAbility
{
    [Header("Basic Info")]
    public string AbilityId;
    public string DisplayName;
    [TextArea(2, 4)]
    public string Description;
    public Sprite AbilityIcon;

    [Header("Classification")]
    public AbilityCategory AbilityType;
    public UnlockType UnlockMethod;
    public string[] Prerequisites = new string[0];

    [Header("Resource Costs")]
    public float ManaCost = 20f;
    public float Cooldown = 5f;

    [Header("Effect Values")]
    public float BaseDamage = 0f;
    public float Range = 5f;
    public float Duration = 0f;
    public float AreaOfEffect = 0f;

    [Header("Visual & Audio")]
    public GameObject EffectPrefab;
    public AudioClip ActivationSound;
    public Color EffectColor = Color.white;
    public ParticleSystem ParticleEffect;

    [Header("Animation")]
    public string AnimationTrigger;
    public float AnimationDuration = 1f;

    [Header("Advanced Properties")]
    public bool RequiresTarget = false;
    public bool RequiresGrounded = false;
    public bool CanUseWhileMoving = true;
    public bool InterruptsMovement = false;

    // Runtime state
    [System.NonSerialized]
    private float _lastUsedTime = -999f;
    [System.NonSerialized]
    private bool _isActive = false;

    public enum AbilityCategory
    {
        Combat,     // Direct damage abilities
        Mobility,   // Movement and traversal
        Utility,    // Puzzle solving and environment interaction
        Defensive   // Protection and crowd control
    }

    public enum UnlockType
    {
        WorldDiscovery,    // Found as collectible in world
        ShrineReward,      // Granted by shrine activation
        BossDefeat,        // Unlocked after defeating boss
        QuestCompletion,   // Unlocked through story progression
        SecretArea         // Hidden in secret areas
    }

    /// <summary>
    /// Check if ability is currently on cooldown
    /// </summary>
    public bool IsOnCooldown => Time.time - _lastUsedTime < Cooldown;

    /// <summary>
    /// Get remaining cooldown time
    /// </summary>
    public float RemainingCooldown => Mathf.Max(0f, Cooldown - (Time.time - _lastUsedTime));

    /// <summary>
    /// Get cooldown progress (0 = ready, 1 = just used)
    /// </summary>
    public float CooldownProgress => Mathf.Clamp01((Time.time - _lastUsedTime) / Cooldown);

    /// <summary>
    /// Start the cooldown timer
    /// </summary>
    public void StartCooldown()
    {
        _lastUsedTime = Time.time;
    }

    /// <summary>
    /// Check if all prerequisites are met
    /// </summary>
    public bool ArePrerequisitesMet(DualProgressionSystem progression)
    {
        foreach (string prereqId in Prerequisites)
        {
            if (!progression.HasAbility(prereqId))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Get modified values based on passive upgrades
    /// </summary>
    public float GetModifiedManaCost(DualProgressionSystem progression)
    {
        float efficiency = progression.GetUpgradeModifier(PassiveUpgrade.UpgradeType.ManaEfficiency);
        return ManaCost * (1f - efficiency);
    }

    public float GetModifiedCooldown(DualProgressionSystem progression)
    {
        float reduction = progression.GetUpgradeModifier(PassiveUpgrade.UpgradeType.CooldownReduction);
        return Cooldown * (1f - reduction);
    }

    public float GetModifiedDamage(DualProgressionSystem progression)
    {
        float bonus = progression.GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDamage);
        return BaseDamage * (1f + bonus);
    }

    public float GetModifiedRange(DualProgressionSystem progression)
    {
        float bonus = progression.GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange);
        return Range * (1f + bonus);
    }

    public float GetModifiedDuration(DualProgressionSystem progression)
    {
        float bonus = progression.GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDuration);
        return Duration * (1f + bonus);
    }
}

/// <summary>
/// Data structure for passive upgrades purchased with light essence
/// </summary>
[System.Serializable]
public class PassiveUpgrade
{
    [Header("Basic Info")]
    public string UpgradeId;
    public string DisplayName;
    [TextArea(2, 3)]
    public string Description;
    public Sprite UpgradeIcon;

    [Header("Classification")]
    public UpgradeType Type;
    public UpgradeCategory Category;
    public int Tier = 1; // 1 = basic, 2 = advanced, 3 = master

    [Header("Cost and Prerequisites")]
    public int EssenceCost = 10;
    public string[] Prerequisites = new string[0];

    [Header("Effect")]
    public float ModifierValue = 0.1f; // Usually percentage as decimal
    public bool IsPercentage = true;

    [Header("UI Presentation")]
    public Vector2 SkillTreePosition = Vector2.zero;
    public Color NodeColor = Color.white;

    public enum UpgradeType
    {
        // Resource Management
        ManaEfficiency,      // Reduce ability mana costs
        ManaRegeneration,    // Increase mana regeneration rate
        MaxMana,             // Increase maximum mana pool

        // Ability Enhancement
        AbilityDamage,       // Increase all ability damage
        AbilityRange,        // Increase all ability range
        AbilityDuration,     // Increase all ability duration
        CooldownReduction,   // Reduce all ability cooldowns
        AreaOfEffect,        // Increase AOE ability size

        // Movement and Survival
        MovementSpeed,       // Increase player movement speed
        JumpHeight,          // Increase jump height
        DashDistance,        // Increase dash distance
        HealthRegeneration,  // Health recovery rate
        DamageReduction,     // Reduce incoming damage

        // Light System
        LightIntensity,      // Brighter lantern light
        LightRange,          // Longer lantern range
        LightPenetration,    // Light goes through more barriers

        // Special
        EssenceGain,         // Gain more essence from sources
        AbilityChainChance,  // Chance for abilities to not consume mana
        CriticalChance,      // Chance for abilities to deal extra damage
        StatusResistance     // Resistance to debuffs
    }

    public enum UpgradeCategory
    {
        Core,           // Universal upgrades everyone will want
        Combat,         // Focused on damage and combat effectiveness
        Exploration,    // Movement and world interaction
        Resource,       // Mana and essence management
        Specialized     // Niche upgrades for specific builds
    }

    /// <summary>
    /// Check if all prerequisites are met for this upgrade
    /// </summary>
    public bool ArePrerequisitesMet(DualProgressionSystem progression)
    {
        foreach (string prereqId in Prerequisites)
        {
            if (!progression.HasUpgrade(prereqId))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Get formatted description with actual values
    /// </summary>
    public string GetFormattedDescription()
    {
        string formattedDesc = Description;

        if (IsPercentage)
        {
            string percentageStr = (ModifierValue * 100f).ToString("F0") + "%";
            formattedDesc = formattedDesc.Replace("{value}", percentageStr);
        }
        else
        {
            formattedDesc = formattedDesc.Replace("{value}", ModifierValue.ToString("F1"));
        }

        return formattedDesc;
    }
}

/// <summary>
/// ScriptableObject for creating ability data assets
/// </summary>
[CreateAssetMenu(fileName = "New Light Ability", menuName = "LanternBound/Light Ability")]
public class LightAbilityData : ScriptableObject
{
    public LightAbility AbilityData;

    private void OnValidate()
    {
        // Auto-generate ID from name if empty
        if (string.IsNullOrEmpty(AbilityData.AbilityId))
        {
            AbilityData.AbilityId = name.ToLower().Replace(" ", "_");
        }

        // Auto-generate display name from asset name if empty
        if (string.IsNullOrEmpty(AbilityData.DisplayName))
        {
            AbilityData.DisplayName = name;
        }
    }
}

/// <summary>
/// ScriptableObject for creating passive upgrade data assets
/// </summary>
[CreateAssetMenu(fileName = "New Passive Upgrade", menuName = "LanternBound/Passive Upgrade")]
public class PassiveUpgradeData : ScriptableObject
{
    public PassiveUpgrade UpgradeData;

    private void OnValidate()
    {
        // Auto-generate ID from name if empty
        if (string.IsNullOrEmpty(UpgradeData.UpgradeId))
        {
            UpgradeData.UpgradeId = name.ToLower().Replace(" ", "_");
        }

        // Auto-generate display name from asset name if empty
        if (string.IsNullOrEmpty(UpgradeData.DisplayName))
        {
            UpgradeData.DisplayName = name;
        }
    }
}

/// <summary>
/// Collectible object that grants active abilities when discovered
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightAbilityCollectible : MonoBehaviour
{
    [Header("Ability to Grant")]
    [SerializeField] private string _abilityId;
    [SerializeField] private LightAbilityData _abilityData;

    [Header("Discovery Effects")]
    [SerializeField] private GameObject _discoveryEffect;
    [SerializeField] private AudioClip _discoverySound;
    [SerializeField] private ParticleSystem _ambientEffect;
    [SerializeField] private Light _glowLight;

    [Header("Visual Representation")]
    [SerializeField] private SpriteRenderer _abilityIcon;
    [SerializeField] private float _floatHeight = 0.3f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 30f;

    private Vector3 _startPosition;
    private bool _discovered = false;
    private AudioSource _audioSource;

    public static System.Action<string> OnAbilityDiscovered;

    private void Awake()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        // Setup collider as trigger
        GetComponent<Collider2D>().isTrigger = true;

        // Setup visual effects
        if (_abilityData != null)
        {
            _abilityId = _abilityData.AbilityData.AbilityId;

            if (_abilityIcon != null && _abilityData.AbilityData.AbilityIcon != null)
            {
                _abilityIcon.sprite = _abilityData.AbilityData.AbilityIcon;
            }

            if (_glowLight != null)
            {
                _glowLight.color = _abilityData.AbilityData.EffectColor;
            }
        }
    }

    private void Update()
    {
        if (!_discovered)
        {
            AnimateCollectible();
        }
    }

    private void AnimateCollectible()
    {
        // Floating animation
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);

        // Rotation animation
        transform.Rotate(Vector3.forward * _rotationSpeed * Time.deltaTime);

        // Pulsing glow
        if (_glowLight != null)
        {
            float pulseIntensity = 1f + Mathf.Sin(Time.time * _floatSpeed * 1.5f) * 0.3f;
            _glowLight.intensity = pulseIntensity;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_discovered || !other.CompareTag("Player")) return;

        DiscoverAbility(other.gameObject);
    }

    private void DiscoverAbility(GameObject player)
    {
        _discovered = true;

        // Find progression system and grant ability
        var progression = player.GetComponent<DualProgressionSystem>();
        if (progression != null)
        {
            progression.DiscoverAbility(_abilityId);
        }

        // Play effects
        PlayDiscoveryEffects();

        // Notify other systems
        OnAbilityDiscovered?.Invoke(_abilityId);

        // Destroy collectible
        Destroy(gameObject, 1f);
    }

    private void PlayDiscoveryEffects()
    {
        // Play discovery sound
        if (_audioSource != null && _discoverySound != null)
        {
            _audioSource.PlayOneShot(_discoverySound);
        }

        // Spawn discovery effect
        if (_discoveryEffect != null)
        {
            Instantiate(_discoveryEffect, transform.position, Quaternion.identity);
        }

        // Burst ambient particles
        if (_ambientEffect != null)
        {
            var emission = _ambientEffect.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 20)
            });
            _ambientEffect.Play();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);

        if (_abilityData != null)
        {
            Gizmos.color = _abilityData.AbilityData.EffectColor;
            Gizmos.DrawIcon(transform.position, "LightAbility", true);
        }
    }
}

/// <summary>
/// Input handler for triggering active abilities
/// </summary>
public class LightAbilityInputHandler : MonoBehaviour
{
    [Header("Input Bindings")]
    [SerializeField] private KeyCode _ability1Key = KeyCode.Q;
    [SerializeField] private KeyCode _ability2Key = KeyCode.E;
    [SerializeField] private KeyCode _ability3Key = KeyCode.R;
    [SerializeField] private KeyCode _ability4Key = KeyCode.T;

    [Header("Quick Slot Abilities")]
    [SerializeField] private string[] _quickSlotAbilities = new string[4];

    private DualProgressionSystem _progression;

    private void Awake()
    {
        _progression = GetComponent<DualProgressionSystem>();
    }

    private void Update()
    {
        HandleAbilityInput();
    }

    private void HandleAbilityInput()
    {
        if (_progression == null) return;

        // Check ability keys
        if (Input.GetKeyDown(_ability1Key) && !string.IsNullOrEmpty(_quickSlotAbilities[0]))
        {
            _progression.UseAbility(_quickSlotAbilities[0]);
        }

        if (Input.GetKeyDown(_ability2Key) && !string.IsNullOrEmpty(_quickSlotAbilities[1]))
        {
            _progression.UseAbility(_quickSlotAbilities[1]);
        }

        if (Input.GetKeyDown(_ability3Key) && !string.IsNullOrEmpty(_quickSlotAbilities[2]))
        {
            _progression.UseAbility(_quickSlotAbilities[2]);
        }

        if (Input.GetKeyDown(_ability4Key) && !string.IsNullOrEmpty(_quickSlotAbilities[3]))
        {
            _progression.UseAbility(_quickSlotAbilities[3]);
        }

        // Mouse buttons for primary abilities
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            // Try to use primary combat ability
            TryUsePrimaryAbility(LightAbility.AbilityCategory.Combat);
        }

        if (Input.GetMouseButtonDown(1)) // Right click
        {
            // Try to use primary utility ability
            TryUsePrimaryAbility(LightAbility.AbilityCategory.Utility);
        }
    }

    private void TryUsePrimaryAbility(LightAbility.AbilityCategory category)
    {
        var abilities = _progression.GetDiscoveredAbilities();

        // Find first ability of the specified category
        foreach (var ability in abilities)
        {
            if (ability.AbilityType == category)
            {
                _progression.UseAbility(ability.AbilityId);
                break;
            }
        }
    }

    /// <summary>
    /// Assign an ability to a quick slot
    /// </summary>
    public void SetQuickSlotAbility(int slotIndex, string abilityId)
    {
        if (slotIndex >= 0 && slotIndex < _quickSlotAbilities.Length)
        {
            _quickSlotAbilities[slotIndex] = abilityId;
        }
    }

    /// <summary>
    /// Get the ability assigned to a quick slot
    /// </summary>
    public string GetQuickSlotAbility(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _quickSlotAbilities.Length)
        {
            return _quickSlotAbilities[slotIndex];
        }
        return null;
    }
}