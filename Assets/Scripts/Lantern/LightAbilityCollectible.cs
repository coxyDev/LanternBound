using UnityEngine;
using System.Collections;

/// <summary>
/// NEW SYSTEM: Light Ability Collectible
/// Separate pickup objects that grant specific active abilities
/// Should be placed at shrines, hidden areas, or after boss defeats
/// </summary>
public class LightAbilityCollectible : MonoBehaviour
{
    [Header("Ability Configuration")]
    [SerializeField] private string _abilityId = "solar_flare";
    [Tooltip("Select which ability this collectible grants")]
    [SerializeField] private AbilityType _abilityType = AbilityType.SolarFlare;

    [Header("Discovery Settings")]
    [SerializeField] private float _discoveryRadius = 2.5f;
    [SerializeField] private LayerMask _playerLayer = 1 << 6;
    [SerializeField] private bool _requireLantern = true; // Must have basic lantern first

    [Header("Visual Effects")]
    [SerializeField] private SpriteRenderer _iconRenderer;
    [SerializeField] private ParticleSystem _auraParticles;
    [SerializeField] private Light _glowLight;
    [SerializeField] private Color _abilityColor = Color.yellow;

    [Header("Animation")]
    [SerializeField] private float _floatHeight = 0.2f;
    [SerializeField] private float _floatSpeed = 1.5f;
    [SerializeField] private float _rotationSpeed = 20f;

    [Header("Audio")]
    [SerializeField] private AudioClip _discoverySound;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    // State
    private bool _collected = false;
    private Vector3 _startPosition;
    private AudioSource _audioSource;

    // Ability type enum
    public enum AbilityType
    {
        SolarFlare,
        PrismBeam,
        LightSlash,
        LightDash,
        GlowingRift,
        PhantomGlow,
        LuminousChains
    }

    private void Awake()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null && _discoverySound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Map ability type to ID
        _abilityId = GetAbilityIdFromType(_abilityType);

        // Set visual color
        SetupVisuals();
    }

    private void Update()
    {
        if (_collected) return;

        // Floating animation
        AnimateFloating();

        // Check for player
        CheckForPlayer();
    }

    private void AnimateFloating()
    {
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);

        if (_iconRenderer != null)
        {
            _iconRenderer.transform.Rotate(Vector3.forward, _rotationSpeed * Time.deltaTime);
        }
    }

    private void CheckForPlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _discoveryRadius, _playerLayer);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                GameObject player = FindPlayerRoot(hit.gameObject);
                if (player != null)
                {
                    GrantAbility(player);
                    break;
                }
            }
        }
    }

    private GameObject FindPlayerRoot(GameObject startObject)
    {
        // Check for DualProgressionSystem component
        var progression = startObject.GetComponent<DualProgressionSystem>();
        if (progression != null) return startObject;

        // Check parent
        Transform current = startObject.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<DualProgressionSystem>() != null)
                return current.gameObject;
            current = current.parent;
        }

        // Check children
        progression = startObject.GetComponentInChildren<DualProgressionSystem>();
        if (progression != null) return progression.gameObject;

        return null;
    }

    private void GrantAbility(GameObject player)
    {
        if (_collected) return;

        var progressionSystem = player.GetComponent<DualProgressionSystem>();
        if (progressionSystem == null)
        {
            if (_debugMode)
                Debug.LogError("❌ DualProgressionSystem not found on player!");
            return;
        }

        // Check if player has lantern first
        if (_requireLantern)
        {
            var lanternController = player.GetComponent<EnhancedLanternController>();
            if (lanternController == null || !lanternController.HasLantern)
            {
                if (_debugMode)
                    Debug.Log("⚠️ Player needs the Ancient Lantern first!");
                return;
            }
        }

        // Check if already has this ability
        if (progressionSystem.HasAbility(_abilityId))
        {
            if (_debugMode)
                Debug.Log($"⚠️ Player already has ability: {_abilityId}");
            return;
        }

        _collected = true;

        if (_debugMode)
        {
            Debug.Log("═══════════════════════════════════");
            Debug.Log($"⭐ NEW ABILITY DISCOVERED: {GetAbilityDisplayName(_abilityType)}");
            Debug.Log($"   ID: {_abilityId}");
            Debug.Log("═══════════════════════════════════");
        }

        // Grant the ability
        progressionSystem.DiscoverAbility(_abilityId);

        // Play effects
        PlayCollectionEffects();

        // Destroy collectible
        Destroy(gameObject, 2f);
    }

    private void PlayCollectionEffects()
    {
        // Play sound
        if (_audioSource != null && _discoverySound != null)
        {
            _audioSource.PlayOneShot(_discoverySound);
        }

        // Burst particles
        if (_auraParticles != null)
        {
            var emission = _auraParticles.emission;
            emission.enabled = true;
            _auraParticles.Play();
        }

        // Flash light
        if (_glowLight != null)
        {
            StartCoroutine(FlashLight());
        }

        // Hide icon
        if (_iconRenderer != null)
        {
            StartCoroutine(FadeOutIcon());
        }
    }

    private IEnumerator FlashLight()
    {
        float startIntensity = _glowLight.intensity;
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Pulse then fade
            float intensity = t < 0.3f ?
                Mathf.Lerp(startIntensity, startIntensity * 3f, t / 0.3f) :
                Mathf.Lerp(startIntensity * 3f, 0f, (t - 0.3f) / 0.7f);

            _glowLight.intensity = intensity;
            yield return null;
        }

        _glowLight.enabled = false;
    }

    private IEnumerator FadeOutIcon()
    {
        Color startColor = _iconRenderer.color;
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Color newColor = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0), t);
            _iconRenderer.color = newColor;
            yield return null;
        }
    }

    private void SetupVisuals()
    {
        // Set color based on ability type
        if (_iconRenderer != null)
        {
            _iconRenderer.color = _abilityColor;
        }

        if (_glowLight != null)
        {
            _glowLight.color = _abilityColor;
        }

        if (_auraParticles != null)
        {
            var main = _auraParticles.main;
            main.startColor = _abilityColor;
        }
    }

    private string GetAbilityIdFromType(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.SolarFlare: return "solar_flare";
            case AbilityType.PrismBeam: return "prism_beam";
            case AbilityType.LightSlash: return "light_slash";
            case AbilityType.LightDash: return "light_dash";
            case AbilityType.GlowingRift: return "glowing_rift";
            case AbilityType.PhantomGlow: return "phantom_glow";
            case AbilityType.LuminousChains: return "luminous_chains";
            default: return "unknown";
        }
    }

    private string GetAbilityDisplayName(AbilityType type)
    {
        switch (type)
        {
            case AbilityType.SolarFlare: return "Solar Flare";
            case AbilityType.PrismBeam: return "Prism Beam";
            case AbilityType.LightSlash: return "Light Slash";
            case AbilityType.LightDash: return "Light Dash";
            case AbilityType.GlowingRift: return "Glowing Rift";
            case AbilityType.PhantomGlow: return "Phantom Glow";
            case AbilityType.LuminousChains: return "Luminous Chains";
            default: return "Unknown Ability";
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _abilityColor;
        Gizmos.DrawWireSphere(transform.position, _discoveryRadius);

        // Draw ability type label
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
            GetAbilityDisplayName(_abilityType));
#endif
    }

    #region EDITOR HELPERS
#if UNITY_EDITOR
    [ContextMenu("Setup Solar Flare")]
    private void SetupSolarFlare()
    {
        _abilityType = AbilityType.SolarFlare;
        _abilityColor = Color.yellow;
        _abilityId = "solar_flare";
        SetupVisuals();
    }

    [ContextMenu("Setup Prism Beam")]
    private void SetupPrismBeam()
    {
        _abilityType = AbilityType.PrismBeam;
        _abilityColor = Color.cyan;
        _abilityId = "prism_beam";
        SetupVisuals();
    }

    [ContextMenu("Setup Light Slash")]
    private void SetupLightSlash()
    {
        _abilityType = AbilityType.LightSlash;
        _abilityColor = Color.white;
        _abilityId = "light_slash";
        SetupVisuals();
    }
#endif
    #endregion
}