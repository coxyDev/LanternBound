using UnityEngine;

/// <summary>
/// Data structure for active abilities in the dual progression system
/// Used by DualProgressionSystem for ability management
/// </summary>
[System.Serializable]
public class LightAbility
{
    [Header("Basic Info")]
    public string AbilityId;
    public string DisplayName;
    [TextArea(2, 3)]
    public string Description;
    public Sprite AbilityIcon;

    [Header("Classification")]
    public AbilityCategory AbilityCategory;
    public UnlockType UnlockMethod;

    [Header("Resource Costs")]
    public float ManaCost = 20f;
    public float Cooldown = 5f;

    [Header("Effect Properties")]
    public float BaseDamage = 0f;
    public float Range = 8f;
    public float Duration = 2f;
    public float AreaOfEffect = 0f;

    [Header("Unlock Requirements")]
    public string[] Prerequisites = new string[0];
    public int RequiredLightEssence = 0;
    public string RequiredWorldArea = "";

    [Header("Visual & Audio")]
    public Color EffectColor = Color.white;
    public ParticleSystem ActivationEffect;
    public AudioClip ActivationSound;
    public AudioClip ChannelingSound;

    public enum AbilityCategory
    {
        Combat,     // Direct damage/interaction abilities
        Mobility,   // Movement and traversal abilities  
        Utility,    // Puzzle-solving and world interaction
        Defensive,  // Protection and evasion abilities
        Special     // Unique story/progression abilities
    }

    public enum UnlockType
    {
        WorldDiscovery,    // Found as collectibles in world
        EssencePurchase,   // Bought with light essence
        StoryProgression,  // Unlocked through main quest
        SecretUnlock,      // Hidden/special requirements
        UpgradeEvolution   // Evolved from other abilities
    }

    // Runtime state (not serialized)
    private float _lastUsedTime = -999f;

    /// <summary>
    /// Check if ability is ready to use (not on cooldown)
    /// </summary>
    public bool IsReady => Time.time >= (_lastUsedTime + Cooldown);

    /// <summary>
    /// Get remaining cooldown time
    /// </summary>
    public float CooldownRemaining => Mathf.Max(0f, (Cooldown - (Time.time - _lastUsedTime)));

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
    public int Tier = 1;

    [Header("Cost & Effect")]
    public int Cost = 10;
    public float EffectValue = 0.1f; // 10% improvement by default
    public int MaxLevel = 1;
    public int CurrentLevel = 0;

    [Header("Requirements")]
    public string[] Prerequisites = new string[0];
    public int RequiredPlayerLevel = 0;

    [Header("Visual")]
    public Color UpgradeColor = Color.cyan;
    public ParticleSystem PurchaseEffect;

    public enum UpgradeType
    {
        // Efficiency upgrades
        ManaEfficiency,     // Reduces mana costs
        CooldownReduction,  // Reduces ability cooldowns
        MovementSpeed,      // Increases player movement speed

        // Power upgrades  
        AbilityDamage,      // Increases ability damage
        AbilityRange,       // Increases ability range
        AbilityDuration,    // Increases ability duration

        // Utility upgrades
        ManaRegeneration,   // Faster mana recovery
        LightIntensity,     // Brighter, more effective light
        LightRange,         // Longer light beam range

        // Special upgrades
        EssenceGain,        // More essence from enemies/sources
        AbilityChain,       // Abilities can chain between targets
        ReflectionMastery   // Better light reflection mechanics
    }

    public enum UpgradeCategory
    {
        Efficiency,    // Resource management improvements
        Power,         // Direct effectiveness improvements
        Utility,       // Quality of life and convenience
        Mastery,       // Advanced technique improvements
        Special        // Unique mechanical changes
    }

    /// <summary>
    /// Check if this upgrade can be purchased
    /// </summary>
    public bool CanPurchase(DualProgressionSystem progression)
    {
        // Check cost
        if (progression.GetLightEssence() < Cost)
            return false;

        // Check level
        if (CurrentLevel >= MaxLevel)
            return false;

        // Check prerequisites
        foreach (string prereqId in Prerequisites)
        {
            var prereq = progression.GetUpgrade(prereqId);
            if (prereq == null || prereq.CurrentLevel < prereq.MaxLevel)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get the total effect value based on current level
    /// </summary>
    public float GetTotalEffectValue()
    {
        return EffectValue * CurrentLevel;
    }

    /// <summary>
    /// Get the cost for the next level
    /// </summary>
    public int GetNextLevelCost()
    {
        if (CurrentLevel >= MaxLevel)
            return int.MaxValue;

        // Cost increases with each level
        return Mathf.RoundToInt(Cost * Mathf.Pow(1.5f, CurrentLevel));
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
        // Auto-set AbilityId based on asset name
        if (AbilityData != null && string.IsNullOrEmpty(AbilityData.AbilityId))
        {
            AbilityData.AbilityId = name.ToLower().Replace(" ", "_");
        }
    }
}

/// <summary>
/// ScriptableObject for creating upgrade data assets
/// </summary>
[CreateAssetMenu(fileName = "New Passive Upgrade", menuName = "LanternBound/Passive Upgrade")]
public class PassiveUpgradeData : ScriptableObject
{
    public PassiveUpgrade UpgradeData;

    private void OnValidate()
    {
        // Auto-set UpgradeId based on asset name
        if (UpgradeData != null && string.IsNullOrEmpty(UpgradeData.UpgradeId))
        {
            UpgradeData.UpgradeId = name.ToLower().Replace(" ", "_");
        }
    }
}

/// <summary>
/// Helper class for ability/upgrade management
/// </summary>
public static class ProgressionDataHelper
{
    /// <summary>
    /// Create a standard combat ability
    /// </summary>
    public static LightAbility CreateCombatAbility(string id, string name, string description, float manaCost, float cooldown, float damage, float range)
    {
        return new LightAbility
        {
            AbilityId = id,
            DisplayName = name,
            Description = description,
            AbilityCategory = LightAbility.AbilityCategory.Combat,
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery,
            ManaCost = manaCost,
            Cooldown = cooldown,
            BaseDamage = damage,
            Range = range,
            Duration = 0.5f,
            Prerequisites = new string[0]
        };
    }

    /// <summary>
    /// Create a standard utility ability
    /// </summary>
    public static LightAbility CreateUtilityAbility(string id, string name, string description, float manaCost, float cooldown, float range, float duration)
    {
        return new LightAbility
        {
            AbilityId = id,
            DisplayName = name,
            Description = description,
            AbilityCategory = LightAbility.AbilityCategory.Utility,
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery,
            ManaCost = manaCost,
            Cooldown = cooldown,
            BaseDamage = 0f,
            Range = range,
            Duration = duration,
            Prerequisites = new string[0]
        };
    }

    /// <summary>
    /// Create a standard passive upgrade
    /// </summary>
    public static PassiveUpgrade CreatePassiveUpgrade(string id, string name, string description, PassiveUpgrade.UpgradeType type, PassiveUpgrade.UpgradeCategory category, int cost, float effectValue)
    {
        return new PassiveUpgrade
        {
            UpgradeId = id,
            DisplayName = name,
            Description = description,
            Type = type,
            Category = category,
            Cost = cost,
            EffectValue = effectValue,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        };
    }

    /// <summary>
    /// Get ability category color for UI
    /// </summary>
    public static Color GetAbilityCategoryColor(LightAbility.AbilityCategory category)
    {
        return category switch
        {
            LightAbility.AbilityCategory.Combat => new Color(1f, 0.3f, 0.3f),     // Red
            LightAbility.AbilityCategory.Mobility => new Color(0.3f, 1f, 0.3f),   // Green  
            LightAbility.AbilityCategory.Utility => new Color(0.3f, 0.3f, 1f),    // Blue
            LightAbility.AbilityCategory.Defensive => new Color(1f, 1f, 0.3f),    // Yellow
            LightAbility.AbilityCategory.Special => new Color(1f, 0.3f, 1f),      // Magenta
            _ => Color.white
        };
    }

    /// <summary>
    /// Get upgrade category color for UI
    /// </summary>
    public static Color GetUpgradeCategoryColor(PassiveUpgrade.UpgradeCategory category)
    {
        return category switch
        {
            PassiveUpgrade.UpgradeCategory.Efficiency => new Color(0.3f, 1f, 1f), // Cyan
            PassiveUpgrade.UpgradeCategory.Power => new Color(1f, 0.5f, 0.3f),    // Orange
            PassiveUpgrade.UpgradeCategory.Utility => new Color(0.7f, 1f, 0.3f),  // Light Green
            PassiveUpgrade.UpgradeCategory.Mastery => new Color(1f, 1f, 0.7f),    // Light Yellow
            PassiveUpgrade.UpgradeCategory.Special => new Color(1f, 0.7f, 1f),    // Light Magenta
            _ => Color.white
        };
    }
}