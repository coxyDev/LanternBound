using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FINAL CORRECTED VERSION: DualProgressionSystem
/// - Fixed interface method calls (OnLightEnter not OnIlluminated)
/// - Added GetUpgradeModifier method
/// - Fixed UpgradeType.Damage to UpgradeType.AbilityRange
/// - NO auto-discovery (abilities from world only)
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
    [SerializeField] private float _maxMana = 100f;
    [SerializeField] private float _currentMana = 100f;

    [Header("Ability Input")]
    [SerializeField] private KeyCode _ability1Key = KeyCode.Q;
    [SerializeField] private KeyCode _ability2Key = KeyCode.E;
    [SerializeField] private KeyCode _ability3Key = KeyCode.R;

    // Component references
    private EnhancedLanternController _lanternController;

    // Ability tracking
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
            Debug.Log("✓ DualProgressionSystem initialized (NO auto-discovery)");
    }

    public void Initialize(EnhancedLanternController controller)
    {
        _lanternController = controller;

        if (_debugMode)
            Debug.Log("✓ DualProgressionSystem connected to LanternController");
    }

    private void Update()
    {
        if (_lanternController == null || !_lanternController.HasLantern) return;

        UpdateCooldowns();
        HandleAbilityInput();
        RegenerateMana();
    }

    #region ABILITY DISCOVERY

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
            Debug.Log($"⭐ NEW ABILITY DISCOVERED: {ability.DisplayName}!");
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

    #region ABILITY USAGE

    private void HandleAbilityInput()
    {
        if (Input.GetKeyDown(_ability1Key) && _discoveredAbilities.Count > 0)
        {
            UseAbility(_discoveredAbilities[0].AbilityId);
        }

        if (Input.GetKeyDown(_ability2Key) && _discoveredAbilities.Count > 1)
        {
            UseAbility(_discoveredAbilities[1].AbilityId);
        }

        if (Input.GetKeyDown(_ability3Key) && _discoveredAbilities.Count > 2)
        {
            UseAbility(_discoveredAbilities[2].AbilityId);
        }
    }

    public bool UseAbility(string abilityId)
    {
        var ability = _discoveredAbilities.Find(a => a.AbilityId == abilityId);
        if (ability == null)
        {
            if (_debugMode)
                Debug.Log($"❌ Ability '{abilityId}' not discovered yet!");
            return false;
        }

        // Check cooldown
        if (IsOnCooldown(abilityId))
        {
            if (_debugMode)
                Debug.Log($"⏰ {ability.DisplayName} is on cooldown");
            return false;
        }

        // Check mana cost
        float cost = GetModifiedManaCost(ability);
        if (_currentMana < cost)
        {
            if (_debugMode)
                Debug.Log($"❌ Not enough mana for {ability.DisplayName} (need {cost}, have {_currentMana})");
            return false;
        }

        // Consume mana
        _currentMana -= cost;
        _currentMana = Mathf.Max(0, _currentMana);

        // Execute ability
        ExecuteAbility(ability);

        // Start cooldown
        float cooldown = GetModifiedCooldown(ability);
        _abilityCooldowns[abilityId] = cooldown;

        OnAbilityUsed?.Invoke(ability);

        if (_debugMode)
            Debug.Log($"✨ Used: {ability.DisplayName} | Mana: {_currentMana:F0}/{_maxMana:F0}");

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
            Debug.Log($"☀️ SOLAR FLARE - Radius: {radius}");

        // Find all light-interactive objects in range
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        int activatedCount = 0;

        foreach (var hit in hits)
        {
            var interactable = hit.GetComponent<ILightInteractable>();
            if (interactable != null)
            {
                interactable.OnLightEnter(
                    EnhancedLanternController.LightEffect.Stun,
                    1f,
                    Vector2.zero
                );
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
            Debug.Log($"✨ PRISM BEAM activated");

        Vector2 aimDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        float range = GetModifiedAbilityRange(ability);

        RaycastHit2D hit = Physics2D.Raycast(transform.position, aimDir, range);

        if (hit.collider != null)
        {
            var interactable = hit.collider.GetComponent<ILightInteractable>();
            if (interactable != null)
            {
                interactable.OnLightEnter(
                    EnhancedLanternController.LightEffect.Energize,
                    1f,
                    aimDir
                );
            }

            if (_debugMode)
                Debug.Log($"✨ Prism Beam hit: {hit.collider.name}");
        }

        Debug.DrawRay(transform.position, aimDir * range, Color.cyan, 0.5f);
    }

    private void PerformLightSlash(LightAbility ability)
    {
        if (_debugMode)
            Debug.Log($"⚔️ LIGHT SLASH activated");

        Vector2 slashDir = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        float range = GetModifiedAbilityRange(ability);

        Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)transform.position + slashDir * range, 1f);

        foreach (var hit in hits)
        {
            var interactable = hit.GetComponent<ILightInteractable>();
            if (interactable != null)
            {
                interactable.OnLightEnter(
                    EnhancedLanternController.LightEffect.Reveal,
                    1f,
                    slashDir
                );
            }
        }
    }

    #endregion

    #region COOLDOWN SYSTEM

    private void UpdateCooldowns()
    {
        List<string> keys = new List<string>(_abilityCooldowns.Keys);
        foreach (string key in keys)
        {
            _abilityCooldowns[key] -= Time.deltaTime;
            if (_abilityCooldowns[key] <= 0)
            {
                _abilityCooldowns.Remove(key);
            }
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

    #region PASSIVE UPGRADES

    public bool PurchaseUpgrade(string upgradeId)
    {
        var upgrade = _allPassiveUpgrades.Find(u => u.UpgradeId == upgradeId);
        if (upgrade == null) return false;

        if (_lightEssence >= upgrade.Cost && !_unlockedUpgrades.Contains(upgrade))
        {
            _lightEssence -= upgrade.Cost;
            _unlockedUpgrades.Add(upgrade);

            OnUpgradePurchased?.Invoke(upgrade);
            OnEssenceChanged?.Invoke(_lightEssence);

            if (_debugMode)
                Debug.Log($"📈 Upgrade purchased: {upgrade.DisplayName}");
            return true;
        }
        return false;
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
        var unlockedUpgrade = _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId);
        if (unlockedUpgrade != null)
            return unlockedUpgrade;

        return _allPassiveUpgrades.Find(u => u.UpgradeId == upgradeId);
    }

    public bool HasUpgrade(string upgradeId)
    {
        return _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId) != null;
    }

    /// <summary>
    /// CRITICAL METHOD: Get cumulative upgrade modifier for a specific type
    /// Used by LightAbilityDataStructure to calculate modified ability stats
    /// </summary>
    public float GetUpgradeModifier(PassiveUpgrade.UpgradeType type)
    {
        float modifier = 0f;
        foreach (var upgrade in _unlockedUpgrades)
        {
            if (upgrade.Type == type)
            {
                modifier += upgrade.EffectValue;
            }
        }
        return modifier;
    }

    #endregion

    #region UPGRADE MODIFIERS (INTERNAL)

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

    // FIXED: Use AbilityRange instead of non-existent Damage type
    private float GetModifiedAbilityRange(LightAbility ability)
    {
        float range = ability.Range;
        float bonus = GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange);
        return range * (1f + bonus);
    }

    #endregion

    #region MANA SYSTEM

    private void RegenerateMana()
    {
        if (_currentMana < _maxMana)
        {
            float regenRate = 10f;
            float regenBonus = GetUpgradeModifier(PassiveUpgrade.UpgradeType.ManaRegeneration);
            regenRate *= (1f + regenBonus);

            _currentMana += regenRate * Time.deltaTime;
            _currentMana = Mathf.Min(_currentMana, _maxMana);
        }
    }

    public float GetManaPercentage() => _maxMana > 0 ? _currentMana / _maxMana : 0f;

    #endregion

    #region ABILITY DATABASE

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
            Debug.Log($"✓ Ability database created: {_allActiveAbilities.Count} abilities");
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
            Debug.Log($"✓ Upgrade database created: {_allPassiveUpgrades.Count} upgrades");
    }

    #endregion

    #region DEBUG HELPERS

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