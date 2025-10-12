using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Rendering.Universal;

/// <summary>
/// MIGRATION VERSION: Enhanced DualProgressionSystem that preserves your existing interface
/// while adding the new advanced ability system
/// 
/// COMPATIBILITY: Maintains all existing progression and upgrade functionality
/// NEW FEATURES: Solar Flare, Prism Beam, enhanced ability execution system
/// </summary>
public class DualProgressionSystem : MonoBehaviour
{
    [Header("Compatibility Settings")]
    [SerializeField] private bool _enableAdvancedAbilities = true;
    [SerializeField] private bool _debugMode = true;

    [Header("Active Abilities")]
    [SerializeField] private List<LightAbility> _discoveredAbilities = new List<LightAbility>();
    [SerializeField] private List<LightAbility> _allActiveAbilities = new List<LightAbility>();

    [Header("Passive Upgrades")]
    [SerializeField] private List<PassiveUpgrade> _unlockedUpgrades = new List<PassiveUpgrade>();
    [SerializeField] private List<PassiveUpgrade> _allPassiveUpgrades = new List<PassiveUpgrade>();

    [Header("Currency & Resources")]
    [SerializeField] private int _lightEssence = 20; // Start with some essence for testing
    [SerializeField] private float _maxMana = 100f;
    [SerializeField] private float _currentMana = 100f;

    [Header("Ability Input Bindings")]
    [SerializeField] private KeyCode _ability1Key = KeyCode.Q; // Solar Flare
    [SerializeField] private KeyCode _ability2Key = KeyCode.E; // Prism Beam
    [SerializeField] private KeyCode _ability3Key = KeyCode.R; // Third ability
    [SerializeField] private KeyCode _ability4Key = KeyCode.T; // Fourth ability

    [Header("Bond Tracking (LanternBound System)")]
    [SerializeField] private int _totalAbilityUsages = 0;
    [SerializeField] private float _lastAbilityUsageTime = 0f;
    [SerializeField] private bool _trackBondMetrics = true;

    // Component references
    private EnhancedLanternController _lanternController;

    // Ability tracking
    private Dictionary<string, float> _abilityCooldowns = new Dictionary<string, float>();
    private Dictionary<string, bool> _abilityInputHeld = new Dictionary<string, bool>();

    // Events for system integration
    public System.Action<LightAbility> OnAbilityDiscovered;
    public System.Action<LightAbility> OnAbilityUsed;
    public System.Action<PassiveUpgrade> OnUpgradePurchased;
    public System.Action<int> OnEssenceChanged;

    private void Awake()
    {
        CreateDefaultAbilities();
        CreateDefaultUpgrades();
        InitializeCooldowns();

        if (_debugMode)
            Debug.Log("✓ DualProgressionSystem initialized (Migration Version)");
    }

    public void Initialize(EnhancedLanternController controller)
    {
        _lanternController = controller;

        // Auto-discover Solar Flare for testing
        if (_enableAdvancedAbilities)
        {
            DiscoverAbility("solar_flare");
            DiscoverAbility("prism_beam");
        }

        if (_debugMode)
            Debug.Log("✓ DualProgressionSystem connected to Enhanced LanternController");
    }

    private void Update()
    {
        if (_lanternController == null || !_lanternController.HasLantern) return;

        UpdateCooldowns();
        HandleAbilityInput();
    }

    #region PRESERVED LEGACY INTERFACE

    /// <summary>
    /// LEGACY METHOD: Discover a new active ability
    /// </summary>
    public void DiscoverAbility(string abilityId)
    {
        var ability = _allActiveAbilities.Find(a => a.AbilityId == abilityId);
        if (ability != null && !_discoveredAbilities.Contains(ability))
        {
            _discoveredAbilities.Add(ability);
            OnAbilityDiscovered?.Invoke(ability);

            if (_debugMode)
                Debug.Log($"⭐ New ability discovered: {ability.DisplayName}!");
        }
    }

    /// <summary>
    /// LEGACY METHOD: Check if player has specific ability
    /// </summary>
    public bool HasAbility(string abilityId)
    {
        return _discoveredAbilities.Find(a => a.AbilityId == abilityId) != null;
    }

    /// <summary>
    /// LEGACY METHOD: Purchase passive upgrade with light essence
    /// </summary>
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

    /// <summary>
    /// LEGACY METHOD: Add light essence
    /// </summary>
    public void AddLightEssence(int amount)
    {
        _lightEssence += amount;
        OnEssenceChanged?.Invoke(_lightEssence);

        if (_debugMode && amount > 0)
            Debug.Log($"💎 Gained {amount} Light Essence! Total: {_lightEssence}");
    }

    /// <summary>
    /// LEGACY METHOD: Get current light essence
    /// </summary>
    public int GetLightEssence() => _lightEssence;

    /// <summary>
    /// LEGACY METHOD: Get discovered abilities
    /// </summary>
    public List<LightAbility> GetDiscoveredAbilities() => new List<LightAbility>(_discoveredAbilities);

    /// <summary>
    /// LEGACY METHOD: Get upgrade modifier value
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

    /// <summary>
    /// MISSING METHOD: Get specific upgrade by ID
    /// </summary>
    public PassiveUpgrade GetUpgrade(string upgradeId)
    {
        // Check unlocked upgrades first
        var unlockedUpgrade = _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId);
        if (unlockedUpgrade != null)
            return unlockedUpgrade;

        // If not unlocked, check all available upgrades
        var availableUpgrade = _allPassiveUpgrades.Find(u => u.UpgradeId == upgradeId);
        return availableUpgrade;
    }

    /// <summary>
    /// HELPER METHOD: Check if specific upgrade is unlocked
    /// </summary>
    public bool HasUpgrade(string upgradeId)
    {
        return _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId) != null;
    }

    public int GetUpgradeLevel(string upgradeId)
    {
        var upgrade = _unlockedUpgrades.Find(u => u.UpgradeId == upgradeId);
        return upgrade?.CurrentLevel ?? 0;
    }

    #endregion

    #region NEW ADVANCED ABILITIES

    /// <summary>
    /// NEW: Enhanced ability execution with proper targeting and effects
    /// </summary>
    public bool UseAbility(string abilityId)
    {
        var ability = _discoveredAbilities.Find(a => a.AbilityId == abilityId);
        if (ability == null) return false;

        // Check cooldown
        if (IsOnCooldown(abilityId))
        {
            if (_debugMode)
                Debug.Log($"⏰ {ability.DisplayName} is on cooldown");
            return false;
        }

        // Check mana cost
        float cost = GetModifiedManaCost(ability);
        if (!_lanternController.ConsumeMana(cost))
        {
            if (_debugMode)
                Debug.Log($"❌ Not enough mana for {ability.DisplayName}");
            return false;
        }

        // Execute ability
        ExecuteAbility(ability);

        // Start cooldown
        float cooldown = GetModifiedCooldown(ability);
        _abilityCooldowns[abilityId] = cooldown;

        ExecuteAbilityWithBondTracking(ability);

        if (_debugMode)
            Debug.Log($"✨ Used ability: {ability.DisplayName}");

        return true;
    }

    /// <summary>
    /// NEW: Execute specific ability with enhanced mechanics
    /// </summary>
    private void ExecuteAbility(LightAbility ability)
    {
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

    private void ExecuteCombatAbility(LightAbility ability)
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
                    Debug.LogWarning($"Combat ability '{ability.AbilityId}' not implemented");
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
                if (_debugMode)
                    Debug.LogWarning($"Mobility ability '{ability.AbilityId}' not implemented");
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
            default:
                if (_debugMode)
                    Debug.LogWarning($"Utility ability '{ability.AbilityId}' not implemented");
                break;
        }
    }

    private void ExecuteDefensiveAbility(LightAbility ability)
    {
        switch (ability.AbilityId)
        {
            case "eclipsing_veil":
                PerformEclipsingVeil(ability);
                break;
            default:
                if (_debugMode)
                    Debug.LogWarning($"Defensive ability '{ability.AbilityId}' not implemented");
                break;
        }
    }

    #endregion

    #region CORE ABILITY IMPLEMENTATIONS

    /// <summary>
    /// SOLAR FLARE: Multi-node activation for puzzle solving + enemy stunning
    /// </summary>
    private void PerformSolarFlare(LightAbility ability)
    {
        Vector3 center = transform.position;
        float range = GetModifiedRange(ability);

        if (_debugMode)
            Debug.Log($"☀️ SOLAR FLARE activated at {center} with range {range}");

        // Use the enhanced light controller's burst effect system
        if (_lanternController.CanUseAdvancedFeatures)
        {
            _lanternController.TriggerBurstEffect(
                EnhancedLanternController.LightEffect.Stun,
                center,
                range
            );
        }
        else
        {
            // Fallback for basic solar flare
            PerformBasicSolarFlare(center, range);
        }

        // Visual effect
        CreateSolarFlareVisualEffect(center, range);
    }

    /// <summary>
    /// Fallback Solar Flare implementation for basic systems
    /// </summary>
    private void PerformBasicSolarFlare(Vector3 center, float range)
    {
        // Find puzzle nodes in range
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, range);
        int nodesActivated = 0;
        int enemiesStunned = 0;

        foreach (var collider in colliders)
        {
            // Check for puzzle nodes (new system)
            //var puzzleNode = collider.GetComponent<LightPuzzleNode>();
            //if (puzzleNode != null)
            //{
            //    puzzleNode.ForceActivate();
            //    nodesActivated++;
            //    continue;
            //}

            // Check for legacy light interactables
            var lightInteractable = collider.GetComponent<ILightInteractable>();
            if (lightInteractable != null && lightInteractable.RespondsToEffect(EnhancedLanternController.LightEffect.Stun))
            {
                float distance = Vector3.Distance(center, collider.transform.position);
                float intensity = 1f - (distance / range);
                Vector2 direction = (collider.transform.position - center).normalized;

                lightInteractable.OnLightEnter(EnhancedLanternController.LightEffect.Stun, intensity, direction);

                // Check if it's an enemy
                if (collider.CompareTag("Enemy") || collider.GetComponent<LurkerEnemy>() != null)
                {
                    enemiesStunned++;
                }
            }

            // Legacy support for old enemy scripts
            var legacyEnemy = collider.GetComponent<MonoBehaviour>();
            if (legacyEnemy != null && legacyEnemy.GetType().Name.Contains("Enemy"))
            {
                // Try to call OnLightEnter if it exists
                var method = legacyEnemy.GetType().GetMethod("OnLightEnter");
                if (method != null)
                {
                    try
                    {
                        method.Invoke(legacyEnemy, null);
                        enemiesStunned++;
                    }
                    catch (System.Exception e)
                    {
                        if (_debugMode)
                            Debug.LogWarning($"Failed to invoke OnLightEnter on {legacyEnemy.name}: {e.Message}");
                    }
                }
            }
        }

        if (_debugMode)
        {
            Debug.Log($"☀️ Solar Flare Results:");
            Debug.Log($"  Nodes Activated: {nodesActivated}");
            Debug.Log($"  Enemies Stunned: {enemiesStunned}");
        }
    }

    /// <summary>
    /// PRISM BEAM: Focused light beam with reflection mechanics
    /// </summary>
    private void PerformPrismBeam(LightAbility ability)
    {
        if (_debugMode)
            Debug.Log($"🔷 PRISM BEAM activated");

        // Switch lantern to refraction mode
        if (_lanternController.CanUseAdvancedFeatures)
        {
            _lanternController.SetLightEffect(EnhancedLanternController.LightEffect.Refract);

            // Start channeling coroutine
            StartCoroutine(ChannelPrismBeam(ability));
        }
        else
        {
            // Fallback implementation
            PerformBasicPrismBeam(ability);
        }
    }

    private IEnumerator ChannelPrismBeam(LightAbility ability)
    {
        float duration = GetModifiedDuration(ability);
        float elapsed = 0f;

        while (elapsed < duration && Input.GetKey(_ability2Key))
        {
            // Beam automatically reflects via the enhanced light controller
            // Just consume mana over time
            float manaCostPerSecond = GetModifiedManaCost(ability) / duration;

            if (!_lanternController.ConsumeMana(manaCostPerSecond * Time.deltaTime))
            {
                break; // Out of mana
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Return to previous light effect
        _lanternController.SetLightEffect(EnhancedLanternController.LightEffect.Reveal);

        if (_debugMode)
            Debug.Log($"🔷 Prism Beam ended after {elapsed:F1}s");
    }

    private void PerformBasicPrismBeam(LightAbility ability)
    {
        // Simple raycast implementation for basic prism beam
        Vector3 origin = transform.position;
        Vector2 direction = GetAimDirection();
        float range = GetModifiedRange(ability);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, range);
        if (hit.collider != null)
        {
            var interactable = hit.collider.GetComponent<ILightInteractable>();
            if (interactable != null)
            {
                interactable.OnLightEnter(
                    EnhancedLanternController.LightEffect.Refract,
                    1f,
                    direction
                );
            }
        }

        if (_debugMode)
            Debug.Log($"🔷 Basic Prism Beam fired at {direction} for {range} units");
    }

    /// <summary>
    /// LIGHT SLASH: Basic melee light attack
    /// </summary>
    private void PerformLightSlash(LightAbility ability)
    {
        Vector3 center = transform.position;
        float range = GetModifiedRange(ability);
        float damage = GetModifiedDamage(ability);

        // Create arc of light in front of player
        Vector2 aimDirection = GetAimDirection();

        // Find targets in arc
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, range);

        foreach (var collider in colliders)
        {
            Vector2 toTarget = (collider.transform.position - center).normalized;
            float angle = Vector2.Angle(aimDirection, toTarget);

            if (angle <= 45f) // 90-degree arc
            {
                // Deal damage or trigger effect
                var interactable = collider.GetComponent<ILightInteractable>();
                if (interactable != null)
                {
                    interactable.OnLightEnter(
                        EnhancedLanternController.LightEffect.Purify,
                        damage / 25f, // Convert damage to intensity
                        toTarget
                    );
                }
            }
        }

        CreateLightSlashEffect(center, aimDirection, range);

        if (_debugMode)
            Debug.Log($"⚔️ Light Slash: {damage} damage in {range} unit arc");
    }

    /// <summary>
    /// LIGHT DASH: Enhanced mobility ability
    /// </summary>
    private void PerformLightDash(LightAbility ability)
    {
        Vector2 dashDirection = GetInputDirection();
        if (dashDirection.magnitude < 0.1f)
            dashDirection = Vector2.right; // Default direction

        float dashDistance = GetModifiedRange(ability);

        // Perform dash movement
        StartCoroutine(ExecuteDash(dashDirection, dashDistance));

        if (_debugMode)
            Debug.Log($"💨 Light Dash: {dashDistance} units in direction {dashDirection}");
    }

    private IEnumerator ExecuteDash(Vector2 direction, float distance)
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) yield break;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (Vector3)direction * distance;

        float dashTime = 0.2f;
        float elapsed = 0f;

        while (elapsed < dashTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashTime;

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
            rb.MovePosition(currentPos);

            yield return null;
        }
    }

    /// <summary>
    /// GLOWING RIFT: Time dilation utility
    /// </summary>
    private void PerformGlowingRift(LightAbility ability)
    {
        Vector3 center = transform.position;
        float range = GetModifiedRange(ability);
        float duration = GetModifiedDuration(ability);

        // Create time dilation field
        StartCoroutine(CreateTimeRift(center, range, duration));

        if (_debugMode)
            Debug.Log($"⏰ Glowing Rift: {range} unit radius for {duration}s");
    }

    private IEnumerator CreateTimeRift(Vector3 center, float range, float duration)
    {
        // Find all rigidbodies in range and slow them
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, range);
        List<Rigidbody2D> affectedBodies = new List<Rigidbody2D>();

        foreach (var collider in colliders)
        {
            var rb = collider.GetComponent<Rigidbody2D>();
            if (rb != null && rb != GetComponent<Rigidbody2D>()) // Don't affect player
            {
                affectedBodies.Add(rb);
            }
        }

        // Apply slow effect
        foreach (var rb in affectedBodies)
        {
            rb.linearVelocity *= 0.3f; // Slow to 30% speed
            rb.gravityScale *= 0.3f;
        }

        yield return new WaitForSeconds(duration);

        // Restore normal speed
        foreach (var rb in affectedBodies)
        {
            if (rb != null)
            {
                rb.linearVelocity /= 0.3f; // Restore speed
                rb.gravityScale /= 0.3f;
            }
        }
    }

    /// <summary>
    /// ECLIPSING VEIL: Stealth/concealment ability
    /// </summary>
    private void PerformEclipsingVeil(LightAbility ability)
    {
        float duration = GetModifiedDuration(ability);

        StartCoroutine(ActivateVeil(duration));

        if (_debugMode)
            Debug.Log($"👻 Eclipsing Veil activated for {duration}s");
    }

    private IEnumerator ActivateVeil(float duration)
    {
        // Make player partially transparent and hard to detect
        var spriteRenderer = GetComponent<SpriteRenderer>();
        Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (spriteRenderer != null)
        {
            Color veilColor = originalColor;
            veilColor.a = 0.3f;
            spriteRenderer.color = veilColor;
        }

        // TODO: Make enemies less likely to detect player

        yield return new WaitForSeconds(duration);

        // Restore visibility
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    /// <summary>
    /// PHANTOM GLOW: Decoy projection
    /// </summary>
    private void PerformPhantomGlow(LightAbility ability)
    {
        Vector3 targetPosition = GetAimPosition();
        float duration = GetModifiedDuration(ability);

        CreatePhantomDecoy(targetPosition, duration);

        if (_debugMode)
            Debug.Log($"👥 Phantom Glow: Decoy at {targetPosition} for {duration}s");
    }

    private void CreatePhantomDecoy(Vector3 position, float duration)
    {
        // Create a phantom copy of the player
        GameObject phantom = new GameObject("PhantomDecoy");
        phantom.transform.position = position;

        // Copy visual appearance
        var playerRenderer = GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            var phantomRenderer = phantom.AddComponent<SpriteRenderer>();
            phantomRenderer.sprite = playerRenderer.sprite;
            phantomRenderer.color = new Color(1f, 1f, 1f, 0.5f); // Semi-transparent
        }

        // Add light component to make it glow
        var phantomLight = phantom.AddComponent<Light2D>();
        phantomLight.lightType = Light2D.LightType.Point;
        phantomLight.intensity = 1f;
        phantomLight.pointLightOuterRadius = 3f;
        phantomLight.color = new Color(0.8f, 0.8f, 1f);

        // Destroy after duration
        Destroy(phantom, duration);
    }

    /// <summary>
    /// LUMINOUS CHAINS: Grappling/mobility
    /// </summary>
    private void PerformLuminousChains(LightAbility ability)
    {
        Vector2 aimDirection = GetAimDirection();
        float range = GetModifiedRange(ability);

        // Raycast to find grapple point
        RaycastHit2D hit = Physics2D.Raycast(transform.position, aimDirection, range);

        if (hit.collider != null)
        {
            StartCoroutine(ExecuteGrapple(hit.point));
            if (_debugMode)
                Debug.Log($"⛓️ Luminous Chains: Grappling to {hit.point}");
        }
        else
        {
            if (_debugMode)
                Debug.Log($"⛓️ Luminous Chains: No grapple point found");
        }
    }

    private IEnumerator ExecuteGrapple(Vector3 targetPoint)
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb == null) yield break;

        Vector3 startPos = transform.position;
        float grappleSpeed = 15f;

        while (Vector3.Distance(transform.position, targetPoint) > 0.5f)
        {
            Vector3 direction = (targetPoint - transform.position).normalized;
            rb.linearVelocity = direction * grappleSpeed;
            yield return null;
        }

        // Stop at target
        rb.linearVelocity = Vector2.zero;
    }

    #endregion

    #region Visual Effects

    private void CreateSolarFlareVisualEffect(Vector3 center, float range)
    {
        // Create expanding light ring
        GameObject effect = new GameObject("SolarFlareEffect");
        effect.transform.position = center;

        var lineRenderer = effect.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 0.8f, 0.2f, 0.8f);
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;
        lineRenderer.useWorldSpace = true;

        // Create circle
        int segments = 32;
        lineRenderer.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * range,
                Mathf.Sin(angle) * range,
                0f
            );
            lineRenderer.SetPosition(i, pos);
        }

        // Animate and destroy
        StartCoroutine(AnimateSolarFlareEffect(effect, lineRenderer));
    }

    private IEnumerator AnimateSolarFlareEffect(GameObject effect, LineRenderer lineRenderer)
    {
        float duration = 1f;
        float elapsed = 0f;
        Color startColor = lineRenderer.startColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);

            Color color = startColor;
            color.a = alpha;
            lineRenderer.startColor = color;

            yield return null;
        }

        Destroy(effect);
    }

    private void CreateLightSlashEffect(Vector3 center, Vector2 direction, float range)
    {
        // Create light arc effect
        GameObject effect = new GameObject("LightSlashEffect");
        effect.transform.position = center;

        var lineRenderer = effect.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 1f, 0.5f, 0.8f);
        lineRenderer.startWidth = 0.3f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.useWorldSpace = true;

        // Create arc
        int segments = 16;
        lineRenderer.positionCount = segments + 1;

        float startAngle = Mathf.Atan2(direction.y, direction.x) - Mathf.PI * 0.25f; // 45 degrees left
        float endAngle = startAngle + Mathf.PI * 0.5f; // 90 degree arc

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * range,
                Mathf.Sin(angle) * range,
                0f
            );
            lineRenderer.SetPosition(i, pos);
        }

        // Destroy after short time
        Destroy(effect, 0.3f);
    }

    #endregion

    #region Input Handling

    private void HandleAbilityInput()
    {
        // Solar Flare (Q)
        if (Input.GetKeyDown(_ability1Key))
        {
            UseAbility("solar_flare");
        }

        // Prism Beam (E) - Can be held for channeling
        if (Input.GetKeyDown(_ability2Key))
        {
            UseAbility("prism_beam");
        }

        // Third ability (R)
        if (Input.GetKeyDown(_ability3Key))
        {
            UseAbility("light_dash");
        }

        // Fourth ability (T)
        if (Input.GetKeyDown(_ability4Key))
        {
            UseAbility("glowing_rift");
        }
    }

    #endregion

    #region Utility Methods

    private void UpdateCooldowns()
    {
        var keys = new List<string>(_abilityCooldowns.Keys);
        foreach (var key in keys)
        {
            _abilityCooldowns[key] -= Time.deltaTime;
            if (_abilityCooldowns[key] <= 0f)
            {
                _abilityCooldowns.Remove(key);
            }
        }
    }

    private void InitializeCooldowns()
    {
        _abilityCooldowns.Clear();
    }

    private bool IsOnCooldown(string abilityId)
    {
        return _abilityCooldowns.ContainsKey(abilityId);
    }

    private float GetModifiedManaCost(LightAbility ability)
    {
        float baseCost = ability.ManaCost;
        float efficiency = GetUpgradeModifier(PassiveUpgrade.UpgradeType.ManaEfficiency);
        return baseCost * (1f - efficiency);
    }

    private float GetModifiedCooldown(LightAbility ability)
    {
        float baseCooldown = ability.Cooldown;
        float reduction = GetUpgradeModifier(PassiveUpgrade.UpgradeType.CooldownReduction);
        return baseCooldown * (1f - reduction);
    }

    private float GetModifiedDamage(LightAbility ability)
    {
        float baseDamage = ability.BaseDamage;
        float bonus = GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDamage);
        return baseDamage * (1f + bonus);
    }

    private float GetModifiedRange(LightAbility ability)
    {
        float baseRange = ability.Range;
        float bonus = GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityRange);
        return baseRange * (1f + bonus);
    }

    private float GetModifiedDuration(LightAbility ability)
    {
        float baseDuration = ability.Duration;
        float bonus = GetUpgradeModifier(PassiveUpgrade.UpgradeType.AbilityDuration);
        return baseDuration * (1f + bonus);
    }

    private Vector2 GetInputDirection()
    {
        Vector2 input = InputManager.Movement;
        if (input.magnitude < 0.1f)
            return transform.right; // Default to facing direction
        return input.normalized;
    }

    private Vector2 GetAimDirection()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(InputManager.MousePosition);
        mousePos.z = 0f;
        Vector2 direction = ((Vector2)mousePos - (Vector2)transform.position).normalized;
        return direction;
    }

    private Vector3 GetAimPosition()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(InputManager.MousePosition);
        mousePos.z = 0f;
        return mousePos;
    }

    #endregion

    #region BOND TRACKING ADDITIONS

    // Additional events for bond system
    public System.Action<int> OnAbilityUsageCountChanged; // For bond strength calculation
    public System.Action<float> OnUsageFrequencyChanged;   // For bond decay systems

    /// <summary>
    /// Get total number of ability usages (for bond strength calculation)
    /// </summary>
    public int TotalAbilityUsages => _totalAbilityUsages;

    /// <summary>
    /// Get time since last ability usage (for bond decay)
    /// </summary>
    public float TimeSinceLastUsage => Time.time - _lastAbilityUsageTime;

    /// <summary>
    /// Get discovered abilities count (for bond progression)
    /// </summary>
    public int DiscoveredAbilitiesCount => _discoveredAbilities.Count;

    /// <summary>
    /// Get unlocked upgrades count (for bond progression)
    /// </summary>
    public int UnlockedUpgradesCount => _unlockedUpgrades.Count;

    /// <summary>
    /// Get current light essence (for bond calculations)
    /// </summary>
    public int CurrentLightEssence => _lightEssence;

    /// <summary>
    /// Calculate usage frequency (uses per minute)
    /// </summary>
    public float GetUsageFrequency()
    {
        if (_totalAbilityUsages == 0) return 0f;

        float playTime = Time.time; // Could be replaced with actual play time tracking
        return _totalAbilityUsages / (playTime / 60f);
    }

    /// <summary>
    /// ENHANCED: Execute ability with bond tracking
    /// </summary>
    private void ExecuteAbilityWithBondTracking(LightAbility ability)
    {
        // Original ability execution logic here...

        // Add bond tracking
        if (_trackBondMetrics)
        {
            _totalAbilityUsages++;
            _lastAbilityUsageTime = Time.time;

            OnAbilityUsageCountChanged?.Invoke(_totalAbilityUsages);
            OnUsageFrequencyChanged?.Invoke(GetUsageFrequency());

            if (_debugMode)
                Debug.Log($"🔗 Bond Metric: Total usages = {_totalAbilityUsages}, Frequency = {GetUsageFrequency():F1}/min");
        }

        // Fire the existing event
        ExecuteAbilityWithBondTracking(ability);
    }

    /// <summary>
    /// Get bond-relevant progression data
    /// </summary>
    public BondProgressionData GetBondProgressionData()
    {
        return new BondProgressionData
        {
            TotalUsages = _totalAbilityUsages,
            DiscoveredAbilities = _discoveredAbilities.Count,
            UnlockedUpgrades = _unlockedUpgrades.Count,
            CurrentEssence = _lightEssence,
            TimeSinceLastUsage = TimeSinceLastUsage,
            UsageFrequency = GetUsageFrequency()
        };
    }

    /// <summary>
    /// Data structure for bond progression tracking
    /// </summary>
    [System.Serializable]
    public struct BondProgressionData
    {
        public int TotalUsages;
        public int DiscoveredAbilities;
        public int UnlockedUpgrades;
        public int CurrentEssence;
        public float TimeSinceLastUsage;
        public float UsageFrequency;
    }

    /// <summary>
    /// Debug method to simulate ability usage (for testing bond systems)
    /// </summary>
    [ContextMenu("Debug: Simulate Ability Usage")]
    public void DebugSimulateAbilityUsage()
    {
        if (Application.isPlaying && _discoveredAbilities.Count > 0)
        {
            var ability = _discoveredAbilities[0];
            ExecuteAbilityWithBondTracking(ability);

            Debug.Log($"🔗 Simulated ability usage: {ability.DisplayName} (Total: {_totalAbilityUsages})");
        }
    }

    /// <summary>
    /// Reset bond tracking metrics (for testing)
    /// </summary>
    [ContextMenu("Debug: Reset Bond Metrics")]
    public void DebugResetBondMetrics()
    {
        if (Application.isPlaying)
        {
            _totalAbilityUsages = 0;
            _lastAbilityUsageTime = 0f;

            OnAbilityUsageCountChanged?.Invoke(_totalAbilityUsages);
            OnUsageFrequencyChanged?.Invoke(0f);

            Debug.Log("🔄 Bond metrics reset");
        }
    }

    #endregion

    #region Default Data Creation

    private void CreateDefaultAbilities()
    {
        _allActiveAbilities.Clear();

        // Core combat abilities
        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "solar_flare",
            DisplayName = "Solar Flare",
            Description = "Burst of light that activates multiple puzzle nodes simultaneously and stuns nearby enemies",
            AbilityType = LightAbility.AbilityCategory.Combat,
            ManaCost = 25f,
            Cooldown = 8f,
            BaseDamage = 0f, // Not a damage ability
            Range = 8f,
            Duration = 3f,
            Prerequisites = new string[0],
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "prism_beam",
            DisplayName = "Prism Beam",
            Description = "Focused beam of light that bounces off reflective surfaces to reach distant targets",
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
            Description = "Quick melee attack using an arc of light",
            AbilityType = LightAbility.AbilityCategory.Combat,
            ManaCost = 10f,
            Cooldown = 1f,
            BaseDamage = 25f,
            Range = 2f,
            Duration = 0.2f,
            Prerequisites = new string[0],
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        // Mobility abilities
        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "light_dash",
            DisplayName = "Light Dash",
            Description = "Quick burst of movement leaving a trail of light",
            AbilityType = LightAbility.AbilityCategory.Mobility,
            ManaCost = 12f,
            Cooldown = 4f,
            BaseDamage = 0f,
            Range = 5f,
            Duration = 0.2f,
            Prerequisites = new string[] { "light_slash" },
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "luminous_chains",
            DisplayName = "Luminous Chains",
            Description = "Create a chain of light to grapple to distant points",
            AbilityType = LightAbility.AbilityCategory.Mobility,
            ManaCost = 20f,
            Cooldown = 6f,
            BaseDamage = 0f,
            Range = 15f,
            Duration = 1f,
            Prerequisites = new string[] { "light_dash" },
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        // Utility abilities
        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "glowing_rift",
            DisplayName = "Glowing Rift",
            Description = "Creates a field that slows time for moving objects, useful for timing puzzles",
            AbilityType = LightAbility.AbilityCategory.Utility,
            ManaCost = 30f,
            Cooldown = 15f,
            BaseDamage = 0f,
            Range = 6f,
            Duration = 8f,
            Prerequisites = new string[] { "solar_flare" },
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "phantom_glow",
            DisplayName = "Phantom Glow",
            Description = "Create a glowing decoy to distract enemies or trigger pressure plates",
            AbilityType = LightAbility.AbilityCategory.Utility,
            ManaCost = 18f,
            Cooldown = 10f,
            BaseDamage = 0f,
            Range = 8f,
            Duration = 15f,
            Prerequisites = new string[] { "prism_beam" },
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        // Defensive abilities
        _allActiveAbilities.Add(new LightAbility
        {
            AbilityId = "eclipsing_veil",
            DisplayName = "Eclipsing Veil",
            Description = "Become partially invisible and harder for enemies to detect",
            AbilityType = LightAbility.AbilityCategory.Defensive,
            ManaCost = 25f,
            Cooldown = 20f,
            BaseDamage = 0f,
            Range = 0f,
            Duration = 10f,
            Prerequisites = new string[] { "phantom_glow" },
            UnlockMethod = LightAbility.UnlockType.WorldDiscovery
        });

        if (_debugMode)
            Debug.Log($"✓ Created {_allActiveAbilities.Count} default abilities");
    }

    private void CreateDefaultUpgrades()
    {
        _allPassiveUpgrades.Clear();

        // Efficiency upgrades
        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "mana_efficiency_1",
            DisplayName = "Efficient Channeling I",
            Description = "Reduces mana cost of all abilities by 10%",
            Type = PassiveUpgrade.UpgradeType.ManaEfficiency,
            Category = PassiveUpgrade.UpgradeCategory.Efficiency,
            Cost = 5,
            EffectValue = 0.1f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "cooldown_reduction_1",
            DisplayName = "Quick Recovery I",
            Description = "Reduces cooldown of all abilities by 15%",
            Type = PassiveUpgrade.UpgradeType.CooldownReduction,
            Category = PassiveUpgrade.UpgradeCategory.Efficiency,
            Cost = 8,
            EffectValue = 0.15f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        // Power upgrades
        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "ability_range_1",
            DisplayName = "Extended Reach I",
            Description = "Increases range of all abilities by 25%",
            Type = PassiveUpgrade.UpgradeType.AbilityRange,
            Category = PassiveUpgrade.UpgradeCategory.Power,
            Cost = 10,
            EffectValue = 0.25f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        _allPassiveUpgrades.Add(new PassiveUpgrade
        {
            UpgradeId = "ability_duration_1",
            DisplayName = "Lasting Light I",
            Description = "Increases duration of all abilities by 30%",
            Type = PassiveUpgrade.UpgradeType.AbilityDuration,
            Category = PassiveUpgrade.UpgradeCategory.Power,
            Cost = 12,
            EffectValue = 0.3f,
            MaxLevel = 1,
            CurrentLevel = 0,
            Prerequisites = new string[0]
        });

        if (_debugMode)
            Debug.Log($"✓ Created {_allPassiveUpgrades.Count} default upgrades");
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Show Progression Status")]
    public void DebugShowProgressionStatus()
    {
        Debug.Log($"=== DUAL PROGRESSION SYSTEM STATUS ===");
        Debug.Log($"Light Essence: {_lightEssence}");
        Debug.Log($"Discovered Abilities: {_discoveredAbilities.Count}/{_allActiveAbilities.Count}");
        Debug.Log($"Unlocked Upgrades: {_unlockedUpgrades.Count}/{_allPassiveUpgrades.Count}");
        Debug.Log($"Advanced Features: {(_enableAdvancedAbilities ? "Enabled" : "Disabled")}");

        Debug.Log("Discovered Abilities:");
        foreach (var ability in _discoveredAbilities)
        {
            bool onCooldown = IsOnCooldown(ability.AbilityId);
            Debug.Log($"  {ability.DisplayName} ({ability.AbilityId}) {(onCooldown ? "[COOLDOWN]" : "[READY]")}");
        }
    }

    [ContextMenu("Debug: Test Solar Flare")]
    public void DebugTestSolarFlare()
    {
        if (Application.isPlaying)
        {
            UseAbility("solar_flare");
        }
    }

    [ContextMenu("Debug: Discover All Abilities")]
    public void DebugDiscoverAllAbilities()
    {
        if (Application.isPlaying)
        {
            foreach (var ability in _allActiveAbilities)
            {
                DiscoverAbility(ability.AbilityId);
            }
        }
    }

    [ContextMenu("Debug: Add Light Essence")]
    public void DebugAddEssence()
    {
        if (Application.isPlaying)
        {
            AddLightEssence(50);
        }
    }

    #endregion
}