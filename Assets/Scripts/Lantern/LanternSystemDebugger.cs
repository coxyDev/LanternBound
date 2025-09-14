using UnityEngine;

/// <summary>
/// DEBUG TOOL: Complete lantern system debugger to identify issues
/// Attach this to your Player GameObject to diagnose problems
/// </summary>
public class LanternSystemDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool _enableDebugGUI = true;
    [SerializeField] private bool _enableVerboseLogging = true;

    private EnhancedLanternController _lanternController;
    private DualProgressionSystem _progressionSystem;
    private InputManager _inputManager;

    private void Awake()
    {
        _lanternController = GetComponent<EnhancedLanternController>();
        _progressionSystem = GetComponent<DualProgressionSystem>();
        _inputManager = FindObjectOfType<InputManager>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PerformSystemDiagnostic();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            ForceAcquireLantern();
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            ToggleLanternDebug();
        }

        if (_enableVerboseLogging)
        {
            LogInputStates();
        }
    }

    [ContextMenu("Perform Full System Diagnostic")]
    public void PerformSystemDiagnostic()
    {
        Debug.Log("=== LANTERN SYSTEM DIAGNOSTIC ===");

        // Check core components
        CheckCoreComponents();
        CheckInputSystem();
        CheckLanternController();
        CheckProgressionSystem();
        CheckSceneSetup();

        Debug.Log("=== DIAGNOSTIC COMPLETE ===");
    }

    private void CheckCoreComponents()
    {
        Debug.Log("--- CORE COMPONENTS ---");

        Debug.Log($"EnhancedLanternController: {(_lanternController != null ? "✓ Found" : "❌ Missing")}");
        Debug.Log($"DualProgressionSystem: {(_progressionSystem != null ? "✓ Found" : "❌ Missing")}");
        Debug.Log($"InputManager: {(_inputManager != null ? "✓ Found" : "❌ Missing")}");

        if (_lanternController != null)
        {
            Debug.Log($"  Lantern Enabled: {_lanternController.enabled}");
            Debug.Log($"  Has Lantern: {_lanternController.HasLantern}");
            Debug.Log($"  Is Active: {_lanternController.IsLanternActive}");
        }
    }

    private void CheckInputSystem()
    {
        Debug.Log("--- INPUT SYSTEM ---");

        if (_inputManager == null)
        {
            Debug.LogError("❌ InputManager not found!");
            return;
        }

        // Check if PlayerInput exists
        var playerInput = FindObjectOfType<UnityEngine.InputSystem.PlayerInput>();
        Debug.Log($"PlayerInput Component: {(playerInput != null ? "✓ Found" : "❌ Missing")}");

        if (playerInput != null)
        {
            Debug.Log($"  Actions Asset: {(playerInput.actions != null ? "✓ Assigned" : "❌ Missing")}");

            if (playerInput.actions != null)
            {
                try
                {
                    var lanternAction = playerInput.actions["LanternToggle"];
                    Debug.Log($"  LanternToggle Action: ✓ Found");
                }
                catch
                {
                    Debug.LogWarning("⚠️ LanternToggle action not found - using F key fallback");
                }
            }
        }

        // Test current input states
        Debug.Log($"Current F Key State: {Input.GetKey(KeyCode.F)}");
        Debug.Log($"LanternTogglePressed: {InputManager.LanternTogglePressed}");
    }

    private void CheckLanternController()
    {
        Debug.Log("--- LANTERN CONTROLLER ---");

        if (_lanternController == null) return;

        Debug.Log($"Has Lantern: {_lanternController.HasLantern}");
        Debug.Log($"Is Active: {_lanternController.IsLanternActive}");
        Debug.Log($"Current Light Type: {_lanternController.CurrentLightType}");
        Debug.Log($"Mana: {_lanternController.ManaPercentage:P}");

        // Check 2D lights
        var innerLight = _lanternController.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
        Debug.Log($"Has 2D Light Components: {(innerLight != null ? "✓ Found" : "❌ Missing")}");

        if (innerLight != null)
        {
            Debug.Log($"  Inner Light Enabled: {innerLight.enabled}");
            Debug.Log($"  Inner Light Intensity: {innerLight.intensity}");
        }
    }

    private void CheckProgressionSystem()
    {
        Debug.Log("--- PROGRESSION SYSTEM ---");

        if (_progressionSystem == null) return;

        Debug.Log($"Light Essence: {_progressionSystem.GetLightEssence()}");
        Debug.Log($"Discovered Abilities: {_progressionSystem.GetDiscoveredAbilities().Count}");
        Debug.Log($"Unlocked Upgrades: {_progressionSystem.GetUnlockedUpgrades().Count}");
    }

    private void CheckSceneSetup()
    {
        Debug.Log("--- SCENE SETUP ---");

        // Check for collectible lanterns
        var collectibles = FindObjectsOfType<CollectibleLantern>();
        Debug.Log($"Collectible Lanterns in scene: {collectibles.Length}");

        // Check for interactive platforms
        var platforms = FindObjectsOfType<EnhancedRevealablePlatform>();
        Debug.Log($"Revealable Platforms in scene: {platforms.Length}");

        // Check for light-sensitive enemies
        var enemies = FindObjectsOfType<EnhancedLightSensitiveEnemy>();
        Debug.Log($"Light-Sensitive Enemies in scene: {enemies.Length}");

        // Check camera setup
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Debug.Log($"Main Camera: ✓ Found ({mainCamera.name})");
            Debug.Log($"  Is Orthographic: {mainCamera.orthographic}");

            var urpCamera = mainCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Debug.Log($"  URP Camera Data: {(urpCamera != null ? "✓ Found" : "❌ Missing")}");
        }
        else
        {
            Debug.LogError("❌ Main Camera not found!");
        }
    }

    private void LogInputStates()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log($"🔦 F KEY PRESSED - Lantern Toggle: {InputManager.LanternTogglePressed}");
        }

        if (InputManager.LanternTogglePressed)
        {
            Debug.Log($"🔦 LANTERN TOGGLE INPUT DETECTED");
        }
    }

    [ContextMenu("Force Acquire Lantern")]
    public void ForceAcquireLantern()
    {
        if (_lanternController == null)
        {
            Debug.LogError("❌ Cannot force acquire - no lantern controller!");
            return;
        }

        _lanternController.AcquireLantern();

        if (_progressionSystem != null)
        {
            _progressionSystem.Initialize(_lanternController);
        }

        Debug.Log("🔦 FORCED LANTERN ACQUISITION");
    }

    [ContextMenu("Toggle Lantern (Force)")]
    public void ToggleLanternDebug()
    {
        if (_lanternController == null || !_lanternController.HasLantern)
        {
            Debug.LogWarning("⚠️ Cannot toggle - no lantern or not acquired!");
            return;
        }

        if (_lanternController.IsLanternActive)
        {
            _lanternController.DeactivateLantern();
        }
        else
        {
            _lanternController.ActivateLantern();
        }

        Debug.Log($"🔦 FORCED LANTERN TOGGLE - Now Active: {_lanternController.IsLanternActive}");
    }

    private void OnGUI()
    {
        if (!_enableDebugGUI) return;

        GUILayout.BeginArea(new Rect(10, 100, 300, 400));
        GUILayout.Box("Lantern Debug Panel", GUILayout.Width(290));

        // System status
        GUILayout.Label($"Has Lantern: {(_lanternController?.HasLantern ?? false)}");
        GUILayout.Label($"Lantern Active: {(_lanternController?.IsLanternActive ?? false)}");
        GUILayout.Label($"Light Type: {(_lanternController?.CurrentLightType ?? EnhancedLanternController.LightType.None)}");
        GUILayout.Label($"Mana: {(_lanternController?.ManaPercentage ?? 0):P}");

        GUILayout.Space(10);

        // Input status
        GUILayout.Label($"F Key: {Input.GetKey(KeyCode.F)}");
        GUILayout.Label($"Lantern Toggle: {InputManager.LanternTogglePressed}");

        GUILayout.Space(10);

        // Debug buttons
        if (GUILayout.Button("F1: Full Diagnostic"))
        {
            PerformSystemDiagnostic();
        }

        if (GUILayout.Button("F2: Force Acquire Lantern"))
        {
            ForceAcquireLantern();
        }

        if (GUILayout.Button("F3: Force Toggle Lantern"))
        {
            ToggleLanternDebug();
        }

        GUILayout.Space(10);

        // Component status
        GUILayout.Label("Components:");
        GUILayout.Label($"  Lantern Controller: {(_lanternController != null ? "✓" : "❌")}");
        GUILayout.Label($"  Progression System: {(_progressionSystem != null ? "✓" : "❌")}");
        GUILayout.Label($"  Input Manager: {(_inputManager != null ? "✓" : "❌")}");

        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (_lanternController == null || !_lanternController.HasLantern) return;

        // Draw lantern range when active
        if (_lanternController.IsLanternActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 8f); // Default lantern range
        }

        // Draw inner light range
        Gizmos.color = new Color(1f, 0.8f, 0.6f, 0.3f);
        Gizmos.DrawSphere(transform.position, 2f); // Inner light range
    }
}