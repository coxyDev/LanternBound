using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DualProgressionSystem — manages active abilities and passive upgrades.
/// 
/// MANA: This system does NOT own mana. All mana checks and deductions
/// go through EnhancedLanternController, which is the single source of truth.
/// 
/// ESSENCE: Light Essence currency for passive upgrades lives here.
/// ABILITIES: Discovered in the world, never auto-granted.
/// </summary>
public class DualProgressionSystem : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool _debugMode = true;

    [Header("Active Abilities (Discovered in World)")]
    [SerializeField] private List<LightAbility> _discoveredAbilities = new List<LightAbility>();
    [SerializeField] private List<LightAbility> _allActiveAbilities = new List<LightAbility>();

    [Header("Passive Upgrades (Purchased with Essence)")]
    [SerializeField] private List<PassiveUpgrade> _unlockedUpgrades = new List<PassiveUpgrade>();
    [SerializeField] private List<PassiveUpgrade> _allPassiveUpgrades = new List<PassiveUpgrade>();

    [Header("Resources")]
    [SerializeField] private int _lightEssence = 0;

    [Header("Ability Input")]
    [SerializeField] private KeyCode _ability1Key = KeyCode.Q;
    [SerializeField] private KeyCode _ability2Key = KeyCode.E;
    [SerializeField] private KeyCode _ability3Key = KeyCode.R;

    // Single reference to the mana authority
    private EnhancedLanternController _lanternController;

    private Dictionary<string, float> _abilityCooldowns = new Dictionary<string, float>();

    // Events
    public System.Action<LightAbility> OnAbilityDiscovered;
    public System.Action<LightAbility> OnAbilityUsed;
    public System.Action<PassiveUpgrade> OnUpgradePurchased;
    public System.Action<int> OnEssenceChanged;

    private void Awake()
    {
        CreateAbilityDatabase();
        CreateUpgradeDatabase();

        if (_debugMode)
            Debug.Log("✓ DualProgressionSystem initialized (NO auto-discovery, NO internal mana)");
    }

    /// <summary>
    /// Called by CollectableLantern after AcquireLantern(). Must be called before abilities work.
    /// </summary>
    public void Initialize(EnhancedLanternController controller)
    {
        _lanternController = controller;

        if (_debugMode)
            Debug.Log("✓ DualProgressionSystem connected to LanternController — mana delegated");
    }

    private void Update()
    {
        if (_lanternController == null || !_lanternController.HasLantern) return;

        UpdateCooldowns();
        HandleAbilityInput();
        // No mana regen here — EnhancedLanternController handles it
    }

    #region Ability Discovery

    public void DiscoverAbility(string abilityId)
    {
        var ability = _allActiveAbilities.Find(a => a.AbilityId == abilityId);
        if (ability == null)
        {
            Debug.LogError($"❌ Ability '{abilityId}' not found in database!");
            return;
        }

        if (_discoveredAbilities.Contains(ability))
        {
            if (_debugMode)
                Debug.Log($"⚠️ Already have ability: {ability.DisplayName}");
            return;
        }

        _discoveredAbilities.Add(ability);
        OnAbilityDiscovered?.Invoke(ability);

        if (_debugMode)
            Debug.Log($"⭐ NEW ABILITY DISCOVERED: {ability.DisplayName}");
    }

    public bool HasAbility(string abilityId)
    {
        return _discoveredAbilities.Find(a => a.AbilityId == abilityId) != null;
    }

    public List<LightAbility> GetDiscoveredAbilities()
    {
        return new List<LightAbility>(_discoveredAbilities);
    }

    #endregion

    #region Ability Usage

    private void HandleAbilityInput()
    {
        // Uses InputManager — never legacy Input.GetKeyDown
        // Ability keys are not yet in InputManager so KeyCode is acceptable here
        // TODO: Add Ability1/2/3 actions to the Input Action asset and read via InputManager
        if (Input.GetKeyDown(_ability1Key) && _discoveredAbilities.Count > 0)
            UseAbility(_discoveredAbilities[0].AbilityId);

        if (Input.GetKeyDown(_ability2Key) && _discoveredAbilities.Count > 1)
            UseAbility(_discoveredAbilities[1].AbilityId);

        if (Input.GetKeyDown(_ability3Key) && _discoveredAbilities.Count > 2)
            UseAbility(_discoveredAbilities[2].AbilityId);
    }

    public bool UseAbility(string abilityId)
    {
        var ability = _discoveredAbilities.Find(a => a.AbilityId == abilityId);
        if (ability == null)
        {
            if (_debugMode)
                Debug.Log($"❌ Ability '{abilityId}' not discovered yet");
            return false;
        }

        if (IsOnCooldown(abilityId))
        {
            if (_debugMode)
                Debug.Log($"⏰ {ability.DisplayName} is on cooldown");
            return false;
        }

        // Mana check and deduction go through the controller — the single mana authority
        float cost = GetModifiedManaCost(ability);
        if (!_lanternController.CanAffordAbility(cost))
        {
            if (_debugMode)
                Debug.Log($"❌ Not enough mana for {ability.DisplayName} " +
                          $"(need {cost:F0}, have {_lanternController.CurrentMana:F0})");
            return false;
        }

        // Deduct mana from the single pool
        _lanternController.ConsumeMana(cost);

        ExecuteAbility(ability);

        float cooldown = GetModifiedCooldown(ability);
        _abilityCooldowns[abilityId] = cooldown;

        OnAbilityUsed?.Invoke(ability);

        if (_debugMode)
            Debug.Log($"✨ Used: {ability.DisplayName} | " +
                      $"Mana: {_lanternController.CurrentMana:F0}/{_lanternController.MaxMana:F0}");

        return true;
    }

    private void ExecuteAbility(LightAbility ability)
    {
        switch (ability.AbilityId)
        {
            case "solar_flare":
                PerformSolarFlare(ability);
                break;
            case "prism_beam":
                PerformPrismBeam(ability);
                break;
            case "light_slash":
                PerformLightSlash(ability);
                break;
            default:
                if (_debugMode)
                    Debug.LogWarning($"⚠️ Ability '{ability.AbilityId}' has no implementation yet");
                break;
        }
    }

    private void PerformSolarFlare(LightAbility ability)
    {
        Vector3 center = transform.position;
        float radius = GetModifiedAbilityRange(ability);

        if (_debugMode)
            Debug.Log($"☀️ SOLAR FLARE — Radius: {radius}");

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        int activatedCount = 0;

        foreach (var hit in hits)
        {
            var interactable = hit.GetComponent<ILightInteractable>();
            if (interactable != null)
            {
                interactable.OnLightEnter(EnhancedLanternController.LightEffect.Stun, 1f, Vector2.zero);
                activatedCount++;
            }
        }

        if (_debugMode)
            Debug.Log($"☀️ Solar Flare activated {activatedCount} objects");

        DrawDebugCircle(center, radius, Color.yellow, 1f);
    }

    private void PerformPrismBeam(LightAbility ability)
    {
        if (_debugMode)
            Debug.Log("✨ PRISM BEAM activated");

        Vector2 aimDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        float range = GetModifiedAbilityRange(ability);

        RaycastHit2D hit = Physics2D.Raycast(transform.position, aimDir, range);
        if (hit.collider != null)
        {
            var interactable = hit.collider.GetComponent<ILightInteractable>();
            if (interactable != null)
                interactable.OnLightEnter(EnhancedLanternController.LightEffect.Energize, 1f, aimDir);

            if (_debugMode)
                Debug.Log($"✨ Prism Beam hit: {hit.collider.name}");
        }

        Debug.DrawRay(transform.position, aimDir * range, Color.cyan, 0.5f);
    }

    private void PerformLightSlash(LightAbility ability)
    {
        if (_debugMode)
            Debug.Log("⚔️ LIGHT SLASH activated");

        Vector2 slashDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        float range = GetModifiedAbilityRange(ability);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            (Vector2)transform.position + slashDir * range, 1f);

        foreach (var hit in hits)
        {
            var interactable = hit.GetComponent<ILightInteractable>();
            if (interactable != null)
                interactable.OnLightEnter(EnhancedLanternController.LightEffect.Reveal, 1f, slashDir);
        }
    }

    #endregion

    #region Cooldown System

    private void UpdateCooldowns()
    {
        List<string> keys = new List<string>(_abilityCooldowns.Keys);
        foreach (string key in keys)
        {
            _abilityCooldowns[key] -= Time.deltaTime;
            if (_abilityCooldowns[key] <= 0)
                _abilityCooldowns.Remove(key);
        }
    }

    private bool IsOnCooldown(string abilityId)
    {
        return _abilityCooldowns.ContainsKey(abilityId) && _abilityCooldowns[abilityId] > 0;
    }

    public float GetCooldownRemaining(string abilityId)
    {
        return _abilityCooldowns.ContainsKey(abilityId) ? _abilityCooldowns[abilityId] : 0f;
    }

    #endregion

    #region Passive Upgrades

    public bool PurchaseUpgrade(string upgradeId)
    {
        var upgrade = _allPassiveUpgrades.Find(u => u.UpgradeId == upgradeId);
        if (upgrade == null) return false;

        if (_lightEssence < upgrade.Cost || _unlockedUpgrades.Contains(upgrade))
            return false;

        _lightEssence -= upgrade.Cost;
        _unlockedUpgrades.Add(upgrade);

        // Apply regen upgrades directly to the controller's bonus property
        if (upgrade.Type == PassiveUpgrade.UpgradeType.ManaRegeneration && _lanternController != null)
        {
            _lanternController.ManaRegenBonus += upgrade.EffectValue;

            if (_debugMode)
                Debug.Log($"📈 Mana regen bonus applied to LanternController: " +
                          $"+{upgrade.EffectValue:P0} (total: {_lanternController.ManaRegenBonus:P0})");
        }

        OnUpgradePurchased?.Invoke(upgrade);
        OnEssenceChanged?.Invoke(_lightEssence);

        if (_debugMode)
            Debug.Log($"📈 Upgrade purchased: {upgrade.DisplayName}");

        return true;
    }

    public void AddLightEssence(int amount)
    {
        _lightEssence += amount;
        OnEssenceChanged?.Invoke(_lightEssence);

        if (_debugMode && amount > 0)
            Debug.Log($"💎 +{amount} Light Essence (Total: {_lightEssence})");
    }

    public int GetLightEssence() => _lightEssence;

    public PassiveUpgrade GetUpgrade(string upgradeId)
    {
        var unlocked = _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId);
        return unlocked ?? _allPassiveUpgrades.Find(u => u.UpgradeId == upgradeId);
    }

    public bool HasUpgrade(string upgradeId)
    {
        return _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId) != null;
    }

    /// <summary>
    /// Returns cumulative modifier for a given upgrade type across all unlocked upgrades.
    /// Used by UseAbility to calculate modified costs, cooldowns, and ranges.
    /// </summary>
    public float GetUpgradeModifier(PassiveUpgrade.UpgradeType type)
    {
        float modifier = 0f;
        foreach (var upgrade in _unlockedUpgrades)
        {
            if (upgrade.Type == type)
                modifier += upgrade.EffectValue;
        }
        return modifier;
    }

    #endregion

    #region Upgrade Modifiers (Internal)

    private float GetModifiedManaCost(LightAbility ability)
    {
        float cost = ability.ManaCost;
        float efficiency = GetUpgradeModifier(PassiveUpgrade.UpgradeType.ManaEfficiency);
        return cost * (1f - efficiency);
    }

    private float GetModifiedCooldown(LightAbility ability)
    {
        float cooldown = ability.Cooldown;
        float reduction = GetUpgradeModifier(PassiveUpgrade.UpgradeType.CooldownReduction);
        return cooldown * (1f - reduction);
    }

    private float GetModifiedAbilityRange(LightAbility ability)
    {
        float range = ability.Range;
        float bonus = GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange);
        return range * (1f + bonus);
    }

    #endregion

    #region Ability Database

    private void CreateAbilityDatabase()
    {
        _allActiveAbilities.Clear();

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "solar_flare",
            DisplayName = "Solar Flare",
            Description = "Burst of light that activates all nearby light-responsive objects simultaneously",
            AbilityType = LightAbility.AbilityCategory.Combat,
            ManaCost = 30f,
            Cooldown = 8f,
            BaseDamage = 0f,
            Range = 8f,
            Duration = 1f,
            Prerequisites = new string[0],
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "prism_beam",
            DisplayName = "Prism Beam",
            Description = "Focused beam that bounces off reflective surfaces",
            AbilityType = LightAbility.AbilityCategory.Combat,
            ManaCost = 15f,
            Cooldown = 3f,
            BaseDamage = 30f,
            Range = 12f,
            Duration = 2f,
            Prerequisites = new string[0],
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "light_slash",
            DisplayName = "Light Slash",
            Description = "Quick melee attack with an arc of light",
            AbilityType = LightAbility.AbilityCategory.Combat,
            ManaCost = 10f,
            Cooldown = 1f,
            BaseDamage = 25f,
            Range = 2f,
            Duration = 0.2f,
            Prerequisites = new string[0],
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        if (_debugMode)
            Debug.Log($"✓ Ability database: {_allActiveAbilities.Count} abilities");
    }

    private void CreateUpgradeDatabase()
    {
        _allPassiveUpgrades.Clear();

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "mana_efficiency_1",
            DisplayName = "Efficient Channeling I",
            Description = "Reduces mana cost of all abilities by 10%",
            Type = PassiveUpgrade.UpgradeType.ManaEfficiency,
            Category = PassiveUpgrade.UpgradeCategory.Efficiency,
            Cost = 50,
            EffectValue = 0.1f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "mana_regen_1",
            DisplayName = "Steady Flame I",
            Description = "Increases lantern mana regeneration by 20%",
            Type = PassiveUpgrade.UpgradeType.ManaRegeneration,
            Category = PassiveUpgrade.UpgradeCategory.Efficiency,
            Cost = 60,
            EffectValue = 0.2f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "cooldown_reduction_1",
            DisplayName = "Swift Recovery I",
            Description = "Reduces cooldown of all abilities by 15%",
            Type = PassiveUpgrade.UpgradeType.CooldownReduction,
            Category = PassiveUpgrade.UpgradeCategory.Efficiency,
            Cost = 75,
            EffectValue = 0.15f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "ability_range_1",
            DisplayName = "Extended Reach I",
            Description = "Increases range of all abilities by 25%",
            Type = PassiveUpgrade.UpgradeType.AbilityRange,
            Category = PassiveUpgrade.UpgradeCategory.Power,
            Cost = 100,
            EffectValue = 0.25f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        if (_debugMode)
            Debug.Log($"✓ Upgrade database: {_allPassiveUpgrades.Count} upgrades");
    }

    #endregion

    #region Debug Helpers

    private void DrawDebugCircle(Vector3 center, float radius, Color color, float duration)
    {
        int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * Mathf.PI * 2;
            float angle2 = (float)(i + 1) / segments * Mathf.PI * 2;

            Vector3 point1 = center + new Vector3(Mathf.Cos(angle1), Mathf.Sin(angle1), 0) * radius;
            Vector3 point2 = center + new Vector3(Mathf.Cos(angle2), Mathf.Sin(angle2), 0) * radius;

            Debug.DrawLine(point1, point2, color, duration);
        }
    }

    #endregion
}