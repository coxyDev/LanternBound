using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;
using TMPro;

/// <summary>
/// Light Conduit - Primary target for Prism Beam that powers mechanisms
/// Demonstrates precision routing and sustained beam requirements
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightConduit : MonoBehaviour, IPrismBeamTarget, ILightInteractable
{
    [Header("Conduit Configuration")]
    [SerializeField] private string _conduitId = "";
    [SerializeField] private float _requiredIntensity = 0.8f;
    [SerializeField] private float _chargeTime = 2f;
    [SerializeField] private float _maxCharge = 100f;
    [SerializeField] private bool _requiresContinuousBeam = true;
    [SerializeField] private float _chargeDecayRate = 10f; // Per second when not powered

    [Header("Connection System")]
    [SerializeField] private List<LightConduit> _connectedConduits = new List<LightConduit>();
    [SerializeField] private List<GameObject> _poweredObjects = new List<GameObject>();
    [SerializeField] private float _powerTransferEfficiency = 0.9f;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer _conduitRenderer;
    [SerializeField] private Light2D _conduitLight;
    [SerializeField] private LineRenderer _connectionLines;
    [SerializeField] private ParticleSystem _chargingParticles;
    [SerializeField] private ParticleSystem _fullyChargedParticles;
    [SerializeField] private Slider _chargeIndicator;

    [Header("Audio")]
    [SerializeField] private AudioClip _chargingSound;
    [SerializeField] private AudioClip _fullyChargedSound;
    [SerializeField] private AudioClip _powerTransferSound;

    // State tracking
    public bool IsCurrentlyIlluminated { get; private set; }
    public LightEffect CurrentActiveEffect { get; private set; }
    public bool IsFullyCharged => _currentCharge >= _maxCharge;
    public bool IsReceivingBeam { get; private set; }
    public float ChargePercentage => _currentCharge / _maxCharge;

    private float _currentCharge = 0f;
    private bool _wasFullyCharged = false;
    private AudioSource _audioSource;

    // Events
    public static System.Action<LightConduit> OnConduitFullyCharged;
    public static System.Action<LightConduit> OnConduitLostPower;

    // Visual states
    private Color _inactiveColor = new Color(0.3f, 0.3f, 0.3f);
    private Color _chargingColor = new Color(0.8f, 0.6f, 0.2f);
    private Color _fullyChargedColor = new Color(0.2f, 0.8f, 1f);

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        if (string.IsNullOrEmpty(_conduitId))
        {
            _conduitId = $"Conduit_{GetInstanceID()}";
        }

        SetupComponents();
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void SetupComponents()
    {
        // Setup conduit renderer
        if (_conduitRenderer == null)
        {
            _conduitRenderer = GetComponent<SpriteRenderer>();
            if (_conduitRenderer == null)
            {
                _conduitRenderer = gameObject.AddComponent<SpriteRenderer>();
                _conduitRenderer.sprite = CreateConduitSprite();
            }
        }

        // Setup conduit light
        if (_conduitLight == null)
        {
            GameObject lightObj = new GameObject("ConduitLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _conduitLight = lightObj.AddComponent<Light2D>();
        }

        _conduitLight.lightType = Light2D.LightType.Point;
        _conduitLight.intensity = 0.5f;
        _conduitLight.pointLightInnerRadius = 0.2f;
        _conduitLight.pointLightOuterRadius = 3f;
        _conduitLight.color = _inactiveColor;

        // Setup charge indicator
        if (_chargeIndicator == null)
        {
            CreateChargeIndicator();
        }

        // Setup connection lines
        if (_connectionLines == null && _connectedConduits.Count > 0)
        {
            CreateConnectionLines();
        }
    }

    private Sprite CreateConduitSprite()
    {
        // Create a crystalline conduit sprite
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.4f;
        float innerRadius = size * 0.2f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance <= outerRadius && distance >= innerRadius)
                {
                    // Create hexagonal crystal pattern
                    float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);
                    float normalizedAngle = (angle + Mathf.PI) / (2 * Mathf.PI);
                    bool isOnEdge = (normalizedAngle * 6) % 1 < 0.2f;

                    if (isOnEdge)
                    {
                        float alpha = 1f - (distance - innerRadius) / (outerRadius - innerRadius) * 0.5f;
                        pixels[y * size + x] = new Color(0.7f, 0.8f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0.3f, 0.3f, 0.5f, 0.6f);
                    }
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void CreateChargeIndicator()
    {
        GameObject indicatorObj = new GameObject("ChargeIndicator");
        indicatorObj.transform.SetParent(transform);
        indicatorObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        indicatorObj.transform.localScale = Vector3.one * 0.5f;

        Canvas canvas = indicatorObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        _chargeIndicator = indicatorObj.AddComponent<Slider>();
        _chargeIndicator.minValue = 0f;
        _chargeIndicator.maxValue = 1f;
        _chargeIndicator.value = 0f;

        // Create background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(indicatorObj.transform);
        var bgRect = background.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(2f, 0.2f);
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Create fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(indicatorObj.transform);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(2f, 0.2f);
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = _chargingColor;

        _chargeIndicator.targetGraphic = fillImage;
    }

    private void CreateConnectionLines()
    {
        GameObject connectionObj = new GameObject("ConnectionLines");
        connectionObj.transform.SetParent(transform);
        _connectionLines = connectionObj.AddComponent<LineRenderer>();

        _connectionLines.material = new Material(Shader.Find("Sprites/Default"));
        _connectionLines.startColor = new Color(0.5f, 0.8f, 1f, 0.3f);
        _connectionLines.startWidth = 0.05f;
        _connectionLines.endWidth = 0.05f;
        _connectionLines.useWorldSpace = true;

        UpdateConnectionLines();
    }

    private void UpdateConnectionLines()
    {
        if (_connectionLines == null) return;

        _connectionLines.positionCount = _connectedConduits.Count * 2;

        for (int i = 0; i < _connectedConduits.Count; i++)
        {
            if (_connectedConduits[i] != null)
            {
                _connectionLines.SetPosition(i * 2, transform.position);
                _connectionLines.SetPosition(i * 2 + 1, _connectedConduits[i].transform.position);
            }
        }
    }

    private void Update()
    {
        UpdateCharging();
        UpdateVisuals();
        UpdatePowerTransfer();
    }

    private void UpdateCharging()
    {
        if (IsReceivingBeam)
        {
            // Charge up while receiving beam
            _currentCharge += (_maxCharge / _chargeTime) * Time.deltaTime;
            _currentCharge = Mathf.Min(_currentCharge, _maxCharge);

            // Check for newly fully charged
            if (!_wasFullyCharged && IsFullyCharged)
            {
                _wasFullyCharged = true;
                OnConduitFullyCharged?.Invoke(this);

                if (_fullyChargedSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_fullyChargedSound);
                }

                Debug.Log($"🔋 Conduit {_conduitId} fully charged!");
            }
        }
        else if (_requiresContinuousBeam && _currentCharge > 0f)
        {
            // Decay charge when not receiving beam
            _currentCharge -= _chargeDecayRate * Time.deltaTime;
            _currentCharge = Mathf.Max(_currentCharge, 0f);

            // Check for lost power
            if (_wasFullyCharged && !IsFullyCharged)
            {
                _wasFullyCharged = false;
                OnConduitLostPower?.Invoke(this);

                Debug.Log($"🔌 Conduit {_conduitId} lost power");
            }
        }
    }

    private void UpdateVisuals()
    {
        Color targetColor;

        if (IsFullyCharged)
        {
            targetColor = _fullyChargedColor;
        }
        else if (_currentCharge > 0f)
        {
            float chargeRatio = ChargePercentage;
            targetColor = Color.Lerp(_inactiveColor, _chargingColor, chargeRatio);
        }
        else
        {
            targetColor = _inactiveColor;
        }

        if (_conduitRenderer != null)
            _conduitRenderer.color = targetColor;

        if (_conduitLight != null)
        {
            _conduitLight.color = targetColor;
            _conduitLight.intensity = 0.5f + ChargePercentage * 1.5f;
        }

        if (_chargeIndicator != null)
        {
            _chargeIndicator.value = ChargePercentage;

            var fillImage = _chargeIndicator.targetGraphic as Image;
            if (fillImage != null)
            {
                fillImage.color = IsFullyCharged ? _fullyChargedColor : _chargingColor;
            }
        }

        // Update particles
        if (_chargingParticles != null)
        {
            if (IsReceivingBeam && !IsFullyCharged && !_chargingParticles.isPlaying)
            {
                _chargingParticles.Play();
            }
            else if ((!IsReceivingBeam || IsFullyCharged) && _chargingParticles.isPlaying)
            {
                _chargingParticles.Stop();
            }
        }

        if (_fullyChargedParticles != null)
        {
            if (IsFullyCharged && !_fullyChargedParticles.isPlaying)
            {
                _fullyChargedParticles.Play();
            }
            else if (!IsFullyCharged && _fullyChargedParticles.isPlaying)
            {
                _fullyChargedParticles.Stop();
            }
        }
    }

    private void UpdatePowerTransfer()
    {
        if (!IsFullyCharged) return;

        // Transfer power to connected conduits
        foreach (var connectedConduit in _connectedConduits)
        {
            if (connectedConduit != null && !connectedConduit.IsFullyCharged)
            {
                float transferAmount = (_maxCharge / _chargeTime) * _powerTransferEfficiency * Time.deltaTime;
                connectedConduit.ReceivePowerTransfer(transferAmount);
            }
        }

        // Power connected objects
        foreach (var poweredObject in _poweredObjects)
        {
            if (poweredObject != null)
            {
                var powerReceiver = poweredObject.GetComponent<IPowerReceiver>();
                if (powerReceiver != null)
                {
                    powerReceiver.ReceivePower(this);
                }
            }
        }
    }

    public void ReceivePowerTransfer(float amount)
    {
        _currentCharge += amount;
        _currentCharge = Mathf.Min(_currentCharge, _maxCharge);
    }

    #region IPrismBeamTarget Implementation

    public void OnBeamHit(PrismBeamAbility beam, float intensity, Vector3 hitPoint)
    {
        if (intensity >= _requiredIntensity)
        {
            IsReceivingBeam = true;

            if (_chargingSound != null && _audioSource != null && !_audioSource.isPlaying)
            {
                _audioSource.clip = _chargingSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }

            Debug.Log($"🔋 Conduit {_conduitId} receiving beam (intensity: {intensity:F2})");
        }
    }

    public void OnBeamStay(PrismBeamAbility beam, float intensity, float deltaTime)
    {
        // Beam is maintained - charging continues in Update()
    }

    public void OnBeamExit(PrismBeamAbility beam)
    {
        IsReceivingBeam = false;

        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        Debug.Log($"🔌 Conduit {_conduitId} beam disconnected");
    }

    public bool CanBeTargeted(PrismBeamAbility beam)
    {
        return !IsFullyCharged || !_requiresContinuousBeam;
    }

    public float GetRequiredIntensity()
    {
        return _requiredIntensity;
    }

    #endregion

    #region ILightInteractable Implementation

    public void OnLightEnter(LightEffect effect, float intensity, Vector2 direction)
    {
        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;

        // Conduits can also be powered by other light effects
        if (effect == LightEffect.Energize && intensity >= _requiredIntensity * 0.5f)
        {
            IsReceivingBeam = true;
        }
    }

    public void OnLightStay(LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        // Continue receiving light
    }

    public void OnLightExit(LightEffect effect)
    {
        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = LightEffect.Reveal;

        if (effect == LightEffect.Energize)
        {
            IsReceivingBeam = false;
        }
    }

    public bool RespondsToEffect(LightEffect effect)
    {
        return effect == LightEffect.Energize || effect == LightEffect.Refract;
    }

    public float GetMinimumIntensity(LightEffect effect)
    {
        return _requiredIntensity;
    }

    #endregion

    #region Public API

    public string GetConduitId() => _conduitId;
    public void AddConnectedConduit(LightConduit conduit)
    {
        if (!_connectedConduits.Contains(conduit))
        {
            _connectedConduits.Add(conduit);
            UpdateConnectionLines();
        }
    }

    public void AddPoweredObject(GameObject obj)
    {
        if (!_poweredObjects.Contains(obj))
        {
            _poweredObjects.Add(obj);
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Draw conduit range
        Gizmos.color = IsFullyCharged ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);

        // Draw connections
        Gizmos.color = Color.blue;
        foreach (var connected in _connectedConduits)
        {
            if (connected != null)
            {
                Gizmos.DrawLine(transform.position, connected.transform.position);
            }
        }

        // Draw powered objects
        Gizmos.color = Color.green;
        foreach (var powered in _poweredObjects)
        {
            if (powered != null)
            {
                Gizmos.DrawLine(transform.position, powered.transform.position);
            }
        }
    }
}

/// <summary>
/// Interface for objects that can receive power from conduits
/// </summary>
public interface IPowerReceiver
{
    void ReceivePower(LightConduit source);
    void LosePower(LightConduit source);
    bool RequiresPower { get; }
}

/// <summary>
/// Enhanced UI System for LanternBound - displays abilities, mana, progress, and tutorials
/// </summary>
public class LanternBoundUISystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas _mainCanvas;
    [SerializeField] private CanvasGroup _hudGroup;
    [SerializeField] private CanvasGroup _tutorialGroup;
    [SerializeField] private CanvasGroup _abilityMenuGroup;

    [Header("HUD Elements")]
    [SerializeField] private Slider _manaBar;
    [SerializeField] private TextMeshProUGUI _manaText;
    [SerializeField] private TextMeshProUGUI _essenceText;
    [SerializeField] private TextMeshProUGUI _currentLightTypeText;
    [SerializeField] private Image _lanternStatusIcon;

    [Header("Ability Display")]
    [SerializeField] private Transform _abilityContainer;
    [SerializeField] private GameObject _abilitySlotPrefab;
    [SerializeField] private List<AbilitySlotUI> _abilitySlots = new List<AbilitySlotUI>();

    [Header("Tutorial System")]
    [SerializeField] private TextMeshProUGUI _tutorialText;
    [SerializeField] private Button _tutorialContinueButton;
    [SerializeField] private Image _tutorialBackground;
    [SerializeField] private float _tutorialFadeTime = 0.5f;

    [Header("Progress Indicators")]
    [SerializeField] private Transform _progressContainer;
    [SerializeField] private Slider _overallProgressBar;
    [SerializeField] private TextMeshProUGUI _progressText;

    [Header("Visual Settings")]
    [SerializeField] private Color _manaColor = Color.blue;
    [SerializeField] private Color _essenceColor = Color.yellow;
    [SerializeField] private Color _activeAbilityColor = Color.cyan;
    [SerializeField] private Color _cooldownColor = Color.red;

    // System references
    private EnhancedLanternController _lanternController;
    private DualProgressionSystem _progressionSystem;
    private LanternBoundSystemIntegrator _systemIntegrator;

    // UI state
    private Queue<TutorialMessage> _tutorialQueue = new Queue<TutorialMessage>();
    private Coroutine _currentTutorialCoroutine;
    private bool _isShowingTutorial = false;

    [System.Serializable]
    public class TutorialMessage
    {
        public string message;
        public float displayTime;
        public bool requiresInput;
        public System.Action onComplete;
    }

    [System.Serializable]
    public class AbilitySlotUI
    {
        public string abilityId;
        public Image iconImage;
        public Image cooldownOverlay;
        public TextMeshProUGUI hotkeyText;
        public Slider cooldownSlider;
        public Button activateButton;
        public CanvasGroup slotGroup;
    }

    private void Awake()
    {
        SetupUIReferences();
        InitializeUI();
    }

    private void Start()
    {
        FindSystemReferences();
        SubscribeToEvents();
        UpdateAllUI();

        // Show initial tutorial
        QueueTutorial("Welcome to LanternBound! Use WASD to move and Space to jump.", 3f, false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SetupUIReferences()
    {
        if (_mainCanvas == null)
            _mainCanvas = GetComponent<Canvas>();

        if (_mainCanvas == null)
        {
            Debug.LogError("LanternBoundUISystem requires a Canvas component!");
            enabled = false;
            return;
        }

        // Auto-find UI elements if not assigned
        if (_manaBar == null)
            _manaBar = GetComponentInChildren<Slider>();
    }

    private void InitializeUI()
    {
        // Setup canvas for UI
        if (_mainCanvas != null)
        {
            _mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _mainCanvas.sortingOrder = 100;
        }

        // Initialize ability slots
        SetupAbilitySlots();

        // Setup tutorial system
        SetupTutorialSystem();

        // Initialize progress tracking
        SetupProgressSystem();
    }

    private void SetupAbilitySlots()
    {
        if (_abilityContainer == null) return;

        // Create ability slots for common abilities
        var abilityKeys = new KeyCode[] { KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.T };
        var abilityIds = new string[] { "solar_flare", "prism_beam", "glowing_rift", "light_dash" };

        for (int i = 0; i < abilityKeys.Length; i++)
        {
            CreateAbilitySlot(abilityIds[i], abilityKeys[i]);
        }
    }

    private void CreateAbilitySlot(string abilityId, KeyCode hotkey)
    {
        GameObject slotObj = new GameObject($"AbilitySlot_{abilityId}");
        slotObj.transform.SetParent(_abilityContainer);

        var slotUI = new AbilitySlotUI
        {
            abilityId = abilityId,
            slotGroup = slotObj.AddComponent<CanvasGroup>()
        };

        // Create slot visual elements
        var slotRect = slotObj.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(60f, 60f);

        var slotBackground = slotObj.AddComponent<Image>();
        slotBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Icon
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slotObj.transform);
        slotUI.iconImage = iconObj.AddComponent<Image>();
        slotUI.iconImage.rectTransform.sizeDelta = new Vector2(40f, 40f);

        // Cooldown overlay
        GameObject cooldownObj = new GameObject("CooldownOverlay");
        cooldownObj.transform.SetParent(slotObj.transform);
        slotUI.cooldownOverlay = cooldownObj.AddComponent<Image>();
        slotUI.cooldownOverlay.rectTransform.sizeDelta = new Vector2(60f, 60f);
        slotUI.cooldownOverlay.color = new Color(0f, 0f, 0f, 0.7f);
        slotUI.cooldownOverlay.gameObject.SetActive(false);

        // Hotkey text
        GameObject hotkeyObj = new GameObject("Hotkey");
        hotkeyObj.transform.SetParent(slotObj.transform);
        slotUI.hotkeyText = hotkeyObj.AddComponent<TextMeshProUGUI>();
        slotUI.hotkeyText.text = hotkey.ToString();
        slotUI.hotkeyText.fontSize = 12f;
        slotUI.hotkeyText.color = Color.white;
        slotUI.hotkeyText.rectTransform.sizeDelta = new Vector2(20f, 20f);
        slotUI.hotkeyText.rectTransform.anchoredPosition = new Vector2(20f, -20f);

        // Start disabled
        slotUI.slotGroup.alpha = 0.3f;
        slotUI.slotGroup.interactable = false;

        _abilitySlots.Add(slotUI);
    }

    private void SetupTutorialSystem()
    {
        if (_tutorialGroup != null)
        {
            _tutorialGroup.alpha = 0f;
            _tutorialGroup.blocksRaycasts = false;
        }

        if (_tutorialContinueButton != null)
        {
            _tutorialContinueButton.onClick.AddListener(ContinueTutorial);
        }
    }

    private void SetupProgressSystem()
    {
        if (_overallProgressBar != null)
        {
            _overallProgressBar.minValue = 0f;
            _overallProgressBar.maxValue = 1f;
            _overallProgressBar.value = 0f;
        }
    }

    private void FindSystemReferences()
    {
        _lanternController = FindObjectOfType<EnhancedLanternController>();
        _progressionSystem = FindObjectOfType<DualProgressionSystem>();
        _systemIntegrator = FindObjectOfType<LanternBoundSystemIntegrator>();
    }

    private void SubscribeToEvents()
    {
        if (_lanternController != null)
        {
            _lanternController.OnLanternAcquired += HandleLanternAcquired;
            _lanternController.OnLightTypeChanged += HandleLightTypeChanged;
            _lanternController.OnManaChanged += HandleManaChanged;
        }

        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityDiscovered += HandleAbilityDiscovered;
            _progressionSystem.OnEssenceChanged += HandleEssenceChanged;
        }

        // Subscribe to puzzle events
        LightPuzzleNode.OnNodeActivated += HandleNodeActivated;
        MultiNodePuzzleGate.OnGateOpened += HandleGateOpened;
    }

    private void UnsubscribeFromEvents()
    {
        if (_lanternController != null)
        {
            _lanternController.OnLanternAcquired -= HandleLanternAcquired;
            _lanternController.OnLightTypeChanged -= HandleLightTypeChanged;
            _lanternController.OnManaChanged -= HandleManaChanged;
        }

        if (_progressionSystem != null)
        {
            _progressionSystem.OnAbilityDiscovered -= HandleAbilityDiscovered;
            _progressionSystem.OnEssenceChanged -= HandleEssenceChanged;
        }

        LightPuzzleNode.OnNodeActivated -= HandleNodeActivated;
        MultiNodePuzzleGate.OnGateOpened -= HandleGateOpened;
    }

    private void Update()
    {
        UpdateManaDisplay();
        UpdateAbilitySlots();
        UpdateProgressDisplay();
    }

    #region UI Updates

    private void UpdateAllUI()
    {
        UpdateManaDisplay();
        UpdateEssenceDisplay();
        UpdateLanternStatusDisplay();
        UpdateAbilitySlots();
        UpdateProgressDisplay();
    }

    private void UpdateManaDisplay()
    {
        if (_lanternController == null) return;

        float manaPercentage = _lanternController.ManaPercentage;

        if (_manaBar != null)
        {
            _manaBar.value = manaPercentage;
            _manaBar.fillRect.GetComponent<Image>().color = Color.Lerp(Color.red, _manaColor, manaPercentage);
        }

        if (_manaText != null)
        {
            _manaText.text = $"Mana: {manaPercentage:P0}";
            _manaText.color = _manaColor;
        }
    }

    private void UpdateEssenceDisplay()
    {
        if (_progressionSystem == null) return;

        if (_essenceText != null)
        {
            _essenceText.text = $"Essence: {_progressionSystem.GetLightEssence()}";
            _essenceText.color = _essenceColor;
        }
    }

    private void UpdateLanternStatusDisplay()
    {
        if (_lanternController == null) return;

        if (_currentLightTypeText != null)
        {
            _currentLightTypeText.text = _lanternController.HasLantern ?
                $"Light: {_lanternController.CurrentLightType}" :
                "No Lantern";
        }

        if (_lanternStatusIcon != null)
        {
            _lanternStatusIcon.color = _lanternController.IsLanternActive ? Color.yellow : Color.gray;
        }
    }

    private void UpdateAbilitySlots()
    {
        if (_progressionSystem == null) return;

        var discoveredAbilities = _progressionSystem.GetDiscoveredAbilities();

        foreach (var slot in _abilitySlots)
        {
            var ability = discoveredAbilities.Find(a => a.AbilityId == slot.abilityId);

            if (ability != null)
            {
                // Ability is discovered
                slot.slotGroup.alpha = 1f;
                slot.slotGroup.interactable = true;

                // Update cooldown display
                UpdateAbilitySlotCooldown(slot, ability);
            }
            else
            {
                // Ability not yet discovered
                slot.slotGroup.alpha = 0.3f;
                slot.slotGroup.interactable = false;
            }
        }
    }

    private void UpdateAbilitySlotCooldown(AbilitySlotUI slot, LightAbility ability)
    {
        bool isOnCooldown = ability.IsOnCooldown;

        if (slot.cooldownOverlay != null)
        {
            slot.cooldownOverlay.gameObject.SetActive(isOnCooldown);

            if (isOnCooldown)
            {
                float cooldownProgress = 1f - ability.CooldownProgress;
                slot.cooldownOverlay.fillAmount = cooldownProgress;
            }
        }

        // Update icon color
        if (slot.iconImage != null)
        {
            slot.iconImage.color = isOnCooldown ? _cooldownColor : _activeAbilityColor;
        }
    }

    private void UpdateProgressDisplay()
    {
        if (_progressionSystem == null) return;

        // Calculate overall progress
        int totalAbilities = 4; // Adjust based on your total ability count
        int discoveredAbilities = _progressionSystem.GetDiscoveredAbilities().Count;
        float progress = (float)discoveredAbilities / totalAbilities;

        if (_overallProgressBar != null)
        {
            _overallProgressBar.value = progress;
        }

        if (_progressText != null)
        {
            _progressText.text = $"Progress: {progress:P0}";
        }
    }

    #endregion

    #region Event Handlers

    private void HandleLanternAcquired()
    {
        QueueTutorial("Ancient Lantern acquired! Press F to activate your light.", 4f, false, () =>
        {
            QueueTutorial("Aim with your mouse and illuminate the hidden platforms.", 3f, false);
        });

        UpdateAllUI();
    }

    private void HandleLightTypeChanged(EnhancedLanternController.LightType newType)
    {
        UpdateLanternStatusDisplay();
    }

    private void HandleManaChanged(float percentage)
    {
        UpdateManaDisplay();
    }

    private void HandleAbilityDiscovered(LightAbility ability)
    {
        string abilityName = ability.DisplayName;
        string message = $"New ability discovered: {abilityName}!";

        if (ability.AbilityId == "solar_flare")
        {
            message += " Hold Q to charge, release to blast!";
        }
        else if (ability.AbilityId == "prism_beam")
        {
            message += " Hold E for a focused beam that bounces off prisms!";
        }

        QueueTutorial(message, 4f, false);
        UpdateAbilitySlots();
    }

    private void HandleEssenceChanged(int newAmount)
    {
        UpdateEssenceDisplay();
    }

    private void HandleNodeActivated(LightPuzzleNode node)
    {
        // Could show feedback for node activation
    }

    private void HandleGateOpened(MultiNodePuzzleGate gate)
    {
        QueueTutorial("Puzzle solved! Gate opened.", 2f, false);
    }

    #endregion

    #region Tutorial System

    public void QueueTutorial(string message, float displayTime, bool requiresInput, System.Action onComplete = null)
    {
        _tutorialQueue.Enqueue(new TutorialMessage
        {
            message = message,
            displayTime = displayTime,
            requiresInput = requiresInput,
            onComplete = onComplete
        });

        if (!_isShowingTutorial)
        {
            ShowNextTutorial();
        }
    }

    private void ShowNextTutorial()
    {
        if (_tutorialQueue.Count == 0)
        {
            _isShowingTutorial = false;
            return;
        }

        var tutorial = _tutorialQueue.Dequeue();
        _isShowingTutorial = true;

        if (_currentTutorialCoroutine != null)
        {
            StopCoroutine(_currentTutorialCoroutine);
        }

        _currentTutorialCoroutine = StartCoroutine(ShowTutorialCoroutine(tutorial));
    }

    private IEnumerator ShowTutorialCoroutine(TutorialMessage tutorial)
    {
        // Fade in tutorial
        if (_tutorialGroup != null)
        {
            _tutorialGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < _tutorialFadeTime)
            {
                elapsed += Time.deltaTime;
                _tutorialGroup.alpha = elapsed / _tutorialFadeTime;
                yield return null;
            }
            _tutorialGroup.alpha = 1f;
        }

        // Set tutorial text
        if (_tutorialText != null)
        {
            _tutorialText.text = tutorial.message;
        }

        // Show/hide continue button
        if (_tutorialContinueButton != null)
        {
            _tutorialContinueButton.gameObject.SetActive(tutorial.requiresInput);
        }

        // Wait for display time or input
        if (tutorial.requiresInput)
        {
            // Wait for player input
            yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));
        }
        else
        {
            yield return new WaitForSeconds(tutorial.displayTime);
        }

        // Fade out tutorial
        if (_tutorialGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < _tutorialFadeTime)
            {
                elapsed += Time.deltaTime;
                _tutorialGroup.alpha = 1f - (elapsed / _tutorialFadeTime);
                yield return null;
            }
            _tutorialGroup.alpha = 0f;
            _tutorialGroup.blocksRaycasts = false;
        }

        // Call completion callback
        tutorial.onComplete?.Invoke();

        // Show next tutorial
        ShowNextTutorial();
    }

    private void ContinueTutorial()
    {
        // This will be handled by the coroutine waiting for input
    }

    #endregion

    #region Public API

    public void ShowAbilityMenu()
    {
        if (_abilityMenuGroup != null)
        {
            _abilityMenuGroup.alpha = 1f;
            _abilityMenuGroup.blocksRaycasts = true;
        }
    }

    public void HideAbilityMenu()
    {
        if (_abilityMenuGroup != null)
        {
            _abilityMenuGroup.alpha = 0f;
            _abilityMenuGroup.blocksRaycasts = false;
        }
    }

    public void ForceUpdateUI()
    {
        UpdateAllUI();
    }

    #endregion
}