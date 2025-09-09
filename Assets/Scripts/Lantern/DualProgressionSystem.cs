using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Fixed Dual progression system supporting both active abilities and passive upgrades
/// Active abilities are found/unlocked through world exploration
/// Passive upgrades are purchased with light essence currency
/// </summary>
public class DualProgressionSystem : MonoBehaviour
{
    [Header("Active Abilities")]
    [SerializeField] private List<LightAbility> _discoveredAbilities = new List<LightAbility>();
    [SerializeField] private List<LightAbility> _allActiveAbilities = new List<LightAbility>();

    [Header("Passive Upgrades")]
    [SerializeField] private List<PassiveUpgrade> _unlockedUpgrades = new List<PassiveUpgrade>();
    [SerializeField] private List<PassiveUpgrade> _allPassiveUpgrades = new List<PassiveUpgrade>();

    [Header("Currency")]
    [SerializeField] private int _lightEssence = 0;

    private EnhancedLanternController _lanternController;

    // Events
    public System.Action<LightAbility> OnAbilityDiscovered;
    public System.Action<PassiveUpgrade> OnUpgradePurchased;
    public System.Action<int> OnEssenceChanged;

    private void Awake()
    {
        CreateDefaultAbilities();
        CreateDefaultUpgrades();
    }

    public void Initialize(EnhancedLanternController controller)
    {
        _lanternController = controller;
        Debug.Log("✓ DualProgressionSystem initialized with lantern controller");
    }

    #region Active Abilities System

    /// <summary>
    /// Discover a new active ability (found in world)
    /// </summary>
    public void DiscoverAbility(string abilityId)
    {
        var ability = _allActiveAbilities.Find(a => a.AbilityId == abilityId);
        if (ability != null && !_discoveredAbilities.Contains(ability))
        {
            _discoveredAbilities.Add(ability);
            OnAbilityDiscovered?.Invoke(ability);
            Debug.Log($"New ability discovered: {ability.DisplayName}!");
        }
    }

    /// <summary>
    /// Use an active ability
    /// </summary>
    public bool UseAbility(string abilityId)
    {
        var ability = _discoveredAbilities.Find(a => a.AbilityId == abilityId);
        if (ability == null) return false;

        if (CanUseAbility(ability))
        {
            ExecuteAbility(ability);
            return true;
        }
        return false;
    }

    private bool CanUseAbility(LightAbility ability)
    {
        // Check if we have a lantern controller
        if (_lanternController == null) return false;

        // Check mana cost - FIXED: Use public property instead of private field
        if (_lanternController.ManaPercentage * _lanternController.MaxMana < ability.ManaCost)
            return false;

        // Check cooldown
        if (ability.IsOnCooldown)
            return false;

        // Check prerequisites
        foreach (string prereqId in ability.Prerequisites)
        {
            if (!_discoveredAbilities.Exists(a => a.AbilityId == prereqId))
                return false;
        }

        return true;
    }

    private void ExecuteAbility(LightAbility ability)
    {
        // Consume mana
        float manaCost = GetModifiedManaCost(ability);
        _lanternController.ConsumeMana(manaCost);

        // Start cooldown
        ability.StartCooldown();

        // Execute ability effect
        switch (ability.AbilityType)
        {
            case LightAbility.AbilityCategory.Combat:
                ExecuteCombatAbility(ability);
                break;
            case LightAbility.AbilityCategory.Mobility:
                ExecuteMobilityAbility(ability);
                break;
            case LightAbility.AbilityCategory.Utility:
                ExecuteUtilityAbility(ability);
                break;
            case LightAbility.AbilityCategory.Defensive:
                ExecuteDefensiveAbility(ability);
                break;
        }
    }

    #endregion

    #region Passive Upgrades System

    /// <summary>
    /// Purchase a passive upgrade with light essence
    /// </summary>
    public bool PurchaseUpgrade(string upgradeId)
    {
        var upgrade = _allPassiveUpgrades.Find(u => u.UpgradeId == upgradeId);
        if (upgrade == null) return false;

        if (CanPurchaseUpgrade(upgrade))
        {
            _lightEssence -= upgrade.EssenceCost;
            _unlockedUpgrades.Add(upgrade);
            OnUpgradePurchased?.Invoke(upgrade);
            OnEssenceChanged?.Invoke(_lightEssence);
            Debug.Log($"Upgrade purchased: {upgrade.DisplayName}!");
            return true;
        }
        return false;
    }

    private bool CanPurchaseUpgrade(PassiveUpgrade upgrade)
    {
        // Check cost
        if (_lightEssence < upgrade.EssenceCost)
            return false;

        // Check if already purchased
        if (_unlockedUpgrades.Contains(upgrade))
            return false;

        // Check prerequisites
        foreach (string prereqId in upgrade.Prerequisites)
        {
            if (!_unlockedUpgrades.Exists(u => u.UpgradeId == prereqId))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get total modifier value for a specific upgrade type
    /// </summary>
    public float GetUpgradeModifier(PassiveUpgrade.UpgradeType upgradeType)
    {
        float totalModifier = 0f;
        foreach (var upgrade in _unlockedUpgrades)
        {
            if (upgrade.Type == upgradeType)
            {
                totalModifier += upgrade.ModifierValue;
            }
        }
        return totalModifier;
    }

    #endregion

    #region Currency Management

    public void AddLightEssence(int amount)
    {
        _lightEssence += amount;
        OnEssenceChanged?.Invoke(_lightEssence);
        Debug.Log($"Gained {amount} Light Essence! Total: {_lightEssence}");
    }

    public int GetLightEssence() => _lightEssence;

    #endregion

    #region Ability Execution

    private void ExecuteCombatAbility(LightAbility ability)
    {
        switch (ability.AbilityId)
        {
            case "light_slash":
                PerformLightSlash(ability);
                break;
            case "solar_flare":
                PerformSolarFlare(ability);
                break;
            case "prism_beam":
                PerformPrismBeam(ability);
                break;
            // REMOVED MISSING METHODS FOR MVP - Add these back later when you implement them
            // case "shadow_split":
            //     PerformShadowSplit(ability);
            //     break;
            // case "refracted_shot":
            //     PerformRefractedShot(ability);
            //     break;
            default:
                Debug.LogWarning($"Combat ability '{ability.AbilityId}' not implemented yet");
                break;
        }
    }

    private void ExecuteMobilityAbility(LightAbility ability)
    {
        switch (ability.AbilityId)
        {
            case "light_dash":
                PerformLightDash(ability);
                break;
            case "luminous_chains":
                PerformLuminousChains(ability);
                break;
            default:
                Debug.LogWarning($"Mobility ability '{ability.AbilityId}' not implemented yet");
                break;
        }
    }

    private void ExecuteUtilityAbility(LightAbility ability)
    {
        switch (ability.AbilityId)
        {
            case "glowing_rift":
                PerformGlowingRift(ability);
                break;
            case "phantom_glow":
                PerformPhantomGlow(ability);
                break;
            case "eclipsing_veil":
                PerformEclipsingVeil(ability);
                break;
            default:
                Debug.LogWarning($"Utility ability '{ability.AbilityId}' not implemented yet");
                break;
        }
    }

    private void ExecuteDefensiveAbility(LightAbility ability)
    {
        switch (ability.AbilityId)
        {
            case "illuminated_ward":
                PerformIlluminatedWard(ability);
                break;
            default:
                Debug.LogWarning($"Defensive ability '{ability.AbilityId}' not implemented yet");
                break;
        }
    }

    #endregion

    #region Implemented Ability Methods

    private void PerformLightSlash(LightAbility ability)
    {
        // Create a melee arc attack in front of player
        Vector2 playerPos = _lanternController.transform.position;
        Vector2 direction = _lanternController.transform.right; // Or facing direction

        float damage = ability.BaseDamage * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDamage));
        float range = ability.Range * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange));

        // Simple implementation - find enemies in range
        Collider2D[] enemies = Physics2D.OverlapCircleAll(playerPos + direction * range * 0.5f, range * 0.5f);
        foreach (var enemy in enemies)
        {
            var lightSensitive = enemy.GetComponent<ILightInteractable>();
            if (lightSensitive != null)
            {
                // Apply damage effect
                Debug.Log($"Light Slash hit: {enemy.name} for {damage} damage");

                // Award essence for successful hit
                AddLightEssence(1);
            }
        }

        Debug.Log($"Light Slash executed: {damage} damage, {range} range");
    }

    private void PerformSolarFlare(LightAbility ability)
    {
        // Create blinding AOE effect
        Vector2 playerPos = _lanternController.transform.position;
        float radius = ability.Range * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange));

        // Find all enemies in radius and apply stun
        Collider2D[] enemies = Physics2D.OverlapCircleAll(playerPos, radius);
        int enemiesAffected = 0;

        foreach (var enemy in enemies)
        {
            var lightSensitive = enemy.GetComponent<ILightInteractable>();
            if (lightSensitive != null)
            {
                // Force illumination for stun effect
                lightSensitive.OnIlluminated(_lanternController);
                enemiesAffected++;
                Debug.Log($"Solar Flare stunned: {enemy.name}");
            }
        }

        if (enemiesAffected > 0)
        {
            AddLightEssence(enemiesAffected * 2); // Bonus for multiple enemies
        }

        Debug.Log($"Solar Flare executed: {enemiesAffected} enemies affected");
    }

    private void PerformLightDash(LightAbility ability)
    {
        // Quick dash with light trail
        Vector2 dashDirection = GetInputDirection();
        float dashDistance = ability.Range * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange));

        StartCoroutine(DashCoroutine(dashDirection, dashDistance, ability));
    }

    private void PerformPrismBeam(LightAbility ability)
    {
        // Concentrated beam attack
        Vector2 playerPos = _lanternController.transform.position;
        Vector2 direction = GetAimDirection();

        float damage = ability.BaseDamage * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDamage));
        float range = ability.Range * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange));

        RaycastHit2D hit = Physics2D.Raycast(playerPos, direction, range);
        if (hit.collider != null)
        {
            var lightSensitive = hit.collider.GetComponent<ILightInteractable>();
            if (lightSensitive != null)
            {
                // Apply concentrated damage
                lightSensitive.OnIlluminated(_lanternController);
                AddLightEssence(3); // Higher reward for precise ability
                Debug.Log($"Prism Beam hit: {hit.collider.name} for {damage} damage");
            }
        }

        Debug.Log($"Prism Beam executed: {damage} damage, {range} range");
    }

    private void PerformPhantomGlow(LightAbility ability)
    {
        // Create light clone
        Vector2 playerPos = _lanternController.transform.position;
        float duration = ability.Duration * (1f + GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDuration));

        StartCoroutine(PhantomCloneCoroutine(playerPos, duration));
    }

    // PLACEHOLDER METHODS - Implement these as needed for MVP
    private void PerformLuminousChains(LightAbility ability)
    {
        Debug.Log($"Luminous Chains executed (placeholder)");
    }

    private void PerformGlowingRift(LightAbility ability)
    {
        Debug.Log($"Glowing Rift executed (placeholder)");
    }

    private void PerformEclipsingVeil(LightAbility ability)
    {
        Debug.Log($"Eclipsing Veil executed (placeholder)");
    }

    private void PerformIlluminatedWard(LightAbility ability)
    {
        Debug.Log($"Illuminated Ward executed (placeholder)");
    }

    #endregion

    #region Coroutine Implementations

    private IEnumerator DashCoroutine(Vector2 direction, float distance, LightAbility ability)
    {
        Transform player = _lanternController.transform;
        Vector2 startPos = player.position;
        Vector2 endPos = startPos + direction * distance;

        float dashTime = 0.2f; // Quick dash
        float elapsed = 0f;

        while (elapsed < dashTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashTime;
            player.position = Vector2.Lerp(startPos, endPos, t);

            // Create light trail effect (simple debug for now)
            Debug.DrawLine(startPos, player.position, Color.yellow, 1f);

            yield return null;
        }

        player.position = endPos;
        Debug.Log($"Light Dash completed: {distance} units");
    }

    private IEnumerator PhantomCloneCoroutine(Vector2 position, float duration)
    {
        // Create visual clone GameObject
        GameObject clone = new GameObject("PhantomClone");
        clone.transform.position = position;

        // Add basic visual (for testing)
        var renderer = clone.AddComponent<SpriteRenderer>();
        renderer.color = new Color(1f, 1f, 1f, 0.5f); // Semi-transparent

        // Add a simple white square sprite for testing
        renderer.sprite = CreateSimpleSprite();

        Debug.Log($"Phantom Clone created for {duration} seconds");

        yield return new WaitForSeconds(duration);

        if (clone != null)
            Destroy(clone);
    }

    private Sprite CreateSimpleSprite()
    {
        // Create a simple 1x1 white texture for testing
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }

    #endregion

    #region Helper Methods

    private float GetModifiedManaCost(LightAbility ability)
    {
        float baseCost = ability.ManaCost;
        float efficiency = GetUpgradeModifier(PassiveUpgrade.UpgradeType.ManaEfficiency);
        return baseCost * (1f - efficiency);
    }

    private Vector2 GetInputDirection()
    {
        Vector2 input = InputManager.Movement;
        if (input.magnitude < 0.1f)
            return _lanternController.transform.right; // Default to facing direction
        return input.normalized;
    }

    private Vector2 GetAimDirection()
    {
        // Use mouse position for aiming
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(InputManager.MousePosition);
        mousePos.z = 0f;
        Vector2 direction = ((Vector2)mousePos - (Vector2)_lanternController.transform.position).normalized;
        return direction;
    }

    #endregion

    #region Default Data Creation

    private void CreateDefaultAbilities()
    {
        _allActiveAbilities.Clear();

        // Early Game Abilities - START WITH JUST 2 FOR TESTING
        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "light_slash",
            DisplayName = "Light Slash",
            Description = "A basic melee attack using a quick arc of light",
            AbilityType = LightAbility.AbilityCategory.Combat,
            ManaCost = 10f,
            Cooldown = 1f,
            BaseDamage = 25f,
            Range = 2f,
            Prerequisites = new string[0],
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "solar_flare",
            DisplayName = "Solar Flare",
            Description = "A blinding flash that stuns enemies in a radius",
            AbilityType = LightAbility.AbilityCategory.Defensive,
            ManaCost = 20f,
            Cooldown = 8f,
            Range = 5f,
            Duration = 3f,
            Prerequisites = new string[] { "light_slash" },
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        // ADD MORE ABILITIES LATER AS NEEDED
    }

    private void CreateDefaultUpgrades()
    {
        _allPassiveUpgrades.Clear();

        // Mana Upgrades
        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "mana_efficiency_1",
            DisplayName = "Mana Focus I",
            Description = "Reduce all ability mana costs by 10%",
            Type = PassiveUpgrade.UpgradeType.ManaEfficiency,
            ModifierValue = 0.1f,
            EssenceCost = 5,
            Prerequisites = new string[0]
        });

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "mana_efficiency_2",
            DisplayName = "Mana Focus II",
            Description = "Reduce all ability mana costs by 15%",
            Type = PassiveUpgrade.UpgradeType.ManaEfficiency,
            ModifierValue = 0.15f,
            EssenceCost = 10,
            Prerequisites = new string[] { "mana_efficiency_1" }
        });

        // Ability Damage Upgrades
        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "ability_damage_1",
            DisplayName = "Light Intensity I",
            Description = "Increase all ability damage by 20%",
            Type = PassiveUpgrade.UpgradeType.AbilityDamage,
            ModifierValue = 0.2f,
            EssenceCost = 8,
            Prerequisites = new string[0]
        });

        // Ability Range Upgrades
        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "ability_range_1",
            DisplayName = "Extended Reach I",
            Description = "Increase all ability range by 25%",
            Type = PassiveUpgrade.UpgradeType.AbilityRange,
            ModifierValue = 0.25f,
            EssenceCost = 7,
            Prerequisites = new string[0]
        });

        // Cooldown Reduction
        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "cooldown_reduction_1",
            DisplayName = "Swift Casting I",
            Description = "Reduce all ability cooldowns by 15%",
            Type = PassiveUpgrade.UpgradeType.CooldownReduction,
            ModifierValue = 0.15f,
            EssenceCost = 12,
            Prerequisites = new string[] { "mana_efficiency_1" }
        });
    }

    #endregion

    #region Public API

    public List<LightAbility> GetDiscoveredAbilities() => new List<LightAbility>(_discoveredAbilities);
    public List<PassiveUpgrade> GetUnlockedUpgrades() => new List<PassiveUpgrade>(_unlockedUpgrades);
    public List<PassiveUpgrade> GetAvailableUpgrades()
    {
        return _allPassiveUpgrades.FindAll(u => CanPurchaseUpgrade(u));
    }

    public bool HasAbility(string abilityId)
    {
        return _discoveredAbilities.Exists(a => a.AbilityId == abilityId);
    }

    public bool HasUpgrade(string upgradeId)
    {
        return _unlockedUpgrades.Exists(u => u.UpgradeId == upgradeId);
    }

    // Debug methods for testing
    [ContextMenu("Debug: Show Current State")]
    public void DebugShowCurrentState()
    {
        Debug.Log($"=== DUAL PROGRESSION SYSTEM STATE ===");
        Debug.Log($"Light Essence: {_lightEssence}");
        Debug.Log($"Discovered Abilities: {_discoveredAbilities.Count}");
        Debug.Log($"Unlocked Upgrades: {_unlockedUpgrades.Count}");

        if (_lanternController != null)
        {
            Debug.Log($"Lantern Active: {_lanternController.IsLanternActive}");
            Debug.Log($"Mana: {_lanternController.ManaPercentage:P}");
        }
        else
        {
            Debug.Log("Lantern Controller: Not Connected");
        }
    }

    [ContextMenu("Debug: Grant Test Ability")]
    public void DebugGrantTestAbility()
    {
        DiscoverAbility("light_slash");
    }

    [ContextMenu("Debug: Add Test Essence")]
    public void DebugAddTestEssence()
    {
        AddLightEssence(10);
    }

    #endregion
}