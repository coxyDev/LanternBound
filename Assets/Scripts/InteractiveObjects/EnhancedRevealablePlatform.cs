using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class EnhancedRevealablePlatform : MonoBehaviour, ILightInteractable
{
    [Header("Platform Behavior Type")]
    [SerializeField] private PlatformType _platformType = PlatformType.Tutorial;
    [SerializeField] private float _fadeDelayTime = 5f;
    [SerializeField] private float _flickerDuration = 2f;
    [SerializeField] private bool _requiresContinuousLight = false;

    [Header("Visual States")]
    [SerializeField] private Color _hiddenColor = new Color(1, 1, 1, 0.1f);
    [SerializeField] private Color _visibleColor = Color.white;
    [SerializeField] private Color _flickerColor = new Color(1, 1, 0.5f);
    [SerializeField] private bool _startHidden = true;

    [Header("2D Lighting")]
    [SerializeField] private Light2D _platformLight2D;

    [Header("Light Requirements")]
    [SerializeField] private EnhancedLanternController.LightType _requiredLightType = EnhancedLanternController.LightType.Ember;
    [SerializeField] private bool _acceptAnyLightType = true;

    [Header("Audio")]
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;
    [SerializeField] private AudioClip _flickerSound;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    public enum PlatformType
    {
        Tutorial,       // Stays lit forever once activated
        Timed,          // Stays lit for X seconds after light leaves
        Flickering,     // Flickers before deactivating
        Continuous,     // Requires constant light to stay active
        Progressive,    // Behavior determined by player progression level
        Puzzle          // Special behavior for puzzle contexts
    }

    // Components
    private SpriteRenderer _renderer;
    private BoxCollider2D _detectionTrigger;
    private BoxCollider2D _solidCollider;
    private AudioSource _audioSource;

    // State management
    public bool IsIlluminated { get; private set; }
    public bool IsPermanentlyActive { get; private set; }

    private bool _isFlickering = false;
    private bool _isFadingOut = false;
    private Coroutine _fadeCoroutine;
    private Coroutine _flickerCoroutine;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _audioSource = GetComponent<AudioSource>();

        SetupDualColliders();
        SetupPlatformLight();

        // Determine actual platform type based on progression
        _platformType = DeterminePlatformTypeFromProgression();

        // Start in appropriate state
        if (_startHidden)
        {
            SetHidden();
        }
        else
        {
            SetVisible();
        }

        if (_debugMode)
        {
            Debug.Log($"✓ EnhancedRevealablePlatform initialized: {gameObject.name}");
            Debug.Log($"  Platform Type: {_platformType}");
            Debug.Log($"  Fade Delay: {_fadeDelayTime}s");
            Debug.Log($"  Requires Continuous Light: {_requiresContinuousLight}");
            Debug.Log($"  Accept Any Light: {_acceptAnyLightType}");
        }
    }

    private PlatformType DeterminePlatformTypeFromProgression()
    {
        // If manually set to non-progressive, use that type
        if (_platformType != PlatformType.Progressive)
            return _platformType;

        // Find player progression system to determine appropriate type
        var progressionSystem = FindObjectOfType<DualProgressionSystem>();
        if (progressionSystem != null)
        {
            var discoveredAbilities = progressionSystem.GetDiscoveredAbilities();
            var lightEssence = progressionSystem.GetLightEssence();

            // Early game: Tutorial platforms (permanent)
            if (discoveredAbilities.Count <= 1 && lightEssence < 10)
            {
                return PlatformType.Tutorial;
            }
            // Mid game: Timed platforms
            else if (discoveredAbilities.Count <= 3 && lightEssence < 50)
            {
                return PlatformType.Timed;
            }
            // Late game: Advanced mechanics
            else
            {
                return UnityEngine.Random.value > 0.5f ? PlatformType.Flickering : PlatformType.Continuous;
            }
        }

        // Fallback to tutorial if no progression system found
        return PlatformType.Tutorial;
    }

    private void SetupDualColliders()
    {
        var existingColliders = GetComponents<BoxCollider2D>();

        if (existingColliders.Length >= 1)
        {
            _detectionTrigger = existingColliders[0];
            _detectionTrigger.isTrigger = true;

            if (existingColliders.Length >= 2)
            {
                _solidCollider = existingColliders[1];
                _solidCollider.isTrigger = false;
            }
            else
            {
                _solidCollider = gameObject.AddComponent<BoxCollider2D>();
                _solidCollider.isTrigger = false;
                _solidCollider.size = _detectionTrigger.size;
                _solidCollider.offset = _detectionTrigger.offset;
            }
        }
        else
        {
            _detectionTrigger = gameObject.AddComponent<BoxCollider2D>();
            _detectionTrigger.isTrigger = true;

            _solidCollider = gameObject.AddComponent<BoxCollider2D>();
            _solidCollider.isTrigger = false;
        }

        _detectionTrigger.enabled = true;
    }

    private void SetupPlatformLight()
    {
        if (_platformLight2D == null)
        {
            _platformLight2D = gameObject.AddComponent<Light2D>();
        }

        _platformLight2D.lightType = Light2D.LightType.Point;
        _platformLight2D.intensity = 0.8f;
        _platformLight2D.pointLightInnerRadius = 0.2f;
        _platformLight2D.pointLightOuterRadius = 3f;
        _platformLight2D.color = GetPlatformLightColor();
        _platformLight2D.enabled = false;
    }

    private Color GetPlatformLightColor()
    {
        return _platformType switch
        {
            PlatformType.Tutorial => Color.green,
            PlatformType.Timed => Color.cyan,
            PlatformType.Flickering => Color.yellow,
            PlatformType.Continuous => Color.red,
            PlatformType.Puzzle => Color.magenta,
            _ => Color.cyan
        };
    }

    public void OnIlluminated(EnhancedLanternController lantern)
    {
        if (_debugMode)
        {
            Debug.Log($"💡 PLATFORM ILLUMINATED: {gameObject.name} (Type: {_platformType})");
        }

        // Stop any fade/flicker processes
        StopCoroutines();

        if (!RespondsToLightType(lantern.CurrentLightType))
        {
            if (_debugMode)
                Debug.Log($"❌ Platform {gameObject.name} doesn't respond to {lantern.CurrentLightType}");
            return;
        }

        bool wasIlluminated = IsIlluminated;
        IsIlluminated = true;

        // Handle different platform types
        switch (_platformType)
        {
            case PlatformType.Tutorial:
                ActivatePermanently();
                break;

            case PlatformType.Timed:
            case PlatformType.Flickering:
            case PlatformType.Continuous:
                ActivateTemporarily();
                break;

            case PlatformType.Puzzle:
                HandlePuzzleActivation(lantern);
                break;
        }

        // Play activation sound only on first illumination
        if (!wasIlluminated && _activationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_activationSound);
        }

        if (_debugMode)
        {
            Debug.Log($"✨ PLATFORM REVEALED: {gameObject.name} - Permanent: {IsPermanentlyActive}");
        }
    }

    public void OnLeftLight(EnhancedLanternController lantern)
    {
        if (_debugMode)
        {
            Debug.Log($"🌑 PLATFORM LEFT LIGHT: {gameObject.name} (Type: {_platformType})");
        }

        if (!IsIlluminated || IsPermanentlyActive)
        {
            if (_debugMode)
                Debug.Log($"⚠️ Platform {gameObject.name} ignoring light exit (Illuminated: {IsIlluminated}, Permanent: {IsPermanentlyActive})");
            return;
        }

        // Handle different behaviors when light leaves
        switch (_platformType)
        {
            case PlatformType.Tutorial:
                // Do nothing - stays active forever
                break;

            case PlatformType.Timed:
                StartFadeDelay();
                break;

            case PlatformType.Flickering:
                StartFlickerThenFade();
                break;

            case PlatformType.Continuous:
                DeactivateImmediately();
                break;
        }
    }

    public bool RespondsToLightType(EnhancedLanternController.LightType lightType)
    {
        return _acceptAnyLightType || lightType == _requiredLightType;
    }

    #region Platform Behaviors

    private void ActivatePermanently()
    {
        IsPermanentlyActive = true;
        SetVisible();

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = true;
        }

        if (_debugMode)
        {
            Debug.Log($"🟢 Tutorial platform {gameObject.name} permanently activated");
        }
    }

    private void ActivateTemporarily()
    {
        SetVisible();

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = true;
        }
    }

    private void HandlePuzzleActivation(EnhancedLanternController lantern)
    {
        // Puzzle platforms might have special behavior
        ActivateTemporarily();
    }

    private void StartFadeDelay()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeDelayCoroutine());
    }

    private void StartFlickerThenFade()
    {
        if (_flickerCoroutine != null)
            StopCoroutine(_flickerCoroutine);

        _flickerCoroutine = StartCoroutine(FlickerThenFadeCoroutine());
    }

    private void DeactivateImmediately()
    {
        IsIlluminated = false;
        SetHidden();

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = false;
        }

        if (_deactivationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_deactivationSound);
        }

        if (_debugMode)
        {
            Debug.Log($"🔴 Continuous platform {gameObject.name} deactivated immediately");
        }
    }

    private IEnumerator FadeDelayCoroutine()
    {
        if (_debugMode)
        {
            Debug.Log($"⏱️ Platform {gameObject.name} starting fade delay ({_fadeDelayTime}s)");
        }

        yield return new WaitForSeconds(_fadeDelayTime);

        // Deactivate after delay
        IsIlluminated = false;
        SetHidden();

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = false;
        }

        if (_deactivationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_deactivationSound);
        }

        if (_debugMode)
        {
            Debug.Log($"⚫ Timed platform {gameObject.name} deactivated after delay");
        }
    }

    private IEnumerator FlickerThenFadeCoroutine()
    {
        if (_debugMode)
        {
            Debug.Log($"⚡ Platform {gameObject.name} starting flicker sequence");
        }

        _isFlickering = true;
        float elapsed = 0f;
        bool flickerState = true;
        float flickerRate = 0.2f;

        while (elapsed < _flickerDuration)
        {
            flickerState = !flickerState;
            _renderer.color = flickerState ? _flickerColor : _visibleColor;

            if (_flickerSound != null && _audioSource != null && flickerState)
            {
                _audioSource.PlayOneShot(_flickerSound, 0.3f);
            }

            yield return new WaitForSeconds(flickerRate);
            elapsed += flickerRate;

            // Speed up flicker as we approach the end
            flickerRate = Mathf.Max(0.05f, flickerRate * 0.9f);
        }

        // Final deactivation
        _isFlickering = false;
        IsIlluminated = false;
        SetHidden();

        if (_platformLight2D != null)
        {
            _platformLight2D.enabled = false;
        }

        if (_deactivationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_deactivationSound);
        }

        if (_debugMode)
        {
            Debug.Log($"💥 Flickering platform {gameObject.name} deactivated after flicker");
        }
    }

    #endregion

    #region State Management

    private void SetVisible()
    {
        if (!_isFlickering)
        {
            _renderer.color = _visibleColor;
        }

        if (_solidCollider != null)
        {
            _solidCollider.enabled = true;
        }
    }

    private void SetHidden()
    {
        _renderer.color = _hiddenColor;

        if (_solidCollider != null)
        {
            _solidCollider.enabled = false;
        }
    }

    private void StopCoroutines()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_flickerCoroutine != null)
        {
            StopCoroutine(_flickerCoroutine);
            _flickerCoroutine = null;
        }

        _isFlickering = false;
        _isFadingOut = false;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Progressive Behavior")]
    public void TestProgressiveBehavior()
    {
        _platformType = DeterminePlatformTypeFromProgression();
        Debug.Log($"🧪 Platform {gameObject.name} type determined as: {_platformType}");
    }

    [ContextMenu("Force Tutorial Mode")]
    public void ForceTutorialMode()
    {
        _platformType = PlatformType.Tutorial;
        SetupPlatformLight();
        Debug.Log($"🎓 Platform {gameObject.name} set to Tutorial mode");
    }

    [ContextMenu("Force Timed Mode")]
    public void ForceTimedMode()
    {
        _platformType = PlatformType.Timed;
        SetupPlatformLight();
        Debug.Log($"⏱️ Platform {gameObject.name} set to Timed mode");
    }

    [ContextMenu("Show Platform Info")]
    public void ShowPlatformInfo()
    {
        Debug.Log($"=== PLATFORM INFO: {gameObject.name} ===");
        Debug.Log($"Type: {_platformType}");
        Debug.Log($"Is Illuminated: {IsIlluminated}");
        Debug.Log($"Is Permanently Active: {IsPermanentlyActive}");
        Debug.Log($"Fade Delay Time: {_fadeDelayTime}s");
        Debug.Log($"Is Flickering: {_isFlickering}");
        Debug.Log($"Current Color: {_renderer.color}");
        Debug.Log($"Solid Collider Enabled: {(_solidCollider != null ? _solidCollider.enabled : false)}");
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Color-code gizmos by platform type
        Color gizmoColor = _platformType switch
        {
            PlatformType.Tutorial => Color.green,
            PlatformType.Timed => Color.cyan,
            PlatformType.Flickering => Color.yellow,
            PlatformType.Continuous => Color.red,
            PlatformType.Puzzle => Color.magenta,
            _ => Color.white
        };

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale);

        // Show timer visualization for timed platforms
        if (_platformType == PlatformType.Timed)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, _fadeDelayTime * 0.2f);
        }
    }
}