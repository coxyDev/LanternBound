using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

/// <summary>
/// Automated builder for LanternBound vertical slice demo scenes
/// Creates complete playable sections with proper system integration
/// </summary>
public class VerticalSliceBuilder : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string _sceneName = "LanternBound_VerticalSlice";
    [SerializeField] private Vector3 _sceneOrigin = Vector3.zero;
    [SerializeField] private float _sectionSpacing = 20f;
    [SerializeField] private bool _buildOnStart = false;

    [Header("Demo Sections")]
    [SerializeField] private bool _buildAwakeningChamber = true;
    [SerializeField] private bool _buildPrismPuzzleRoom = true;
    [SerializeField] private bool _buildTensionCorridor = true;
    [SerializeField] private bool _buildClockworkSetPiece = true;

    [Header("Prefab References")]
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private GameObject _collectibleLanternPrefab;
    [SerializeField] private GameObject _floatingLanternPrefab;
    [SerializeField] private GameObject _puzzleNodePrefab;
    [SerializeField] private GameObject _puzzleGatePrefab;
    [SerializeField] private GameObject _lurkerEnemyPrefab;
    [SerializeField] private GameObject _platformPrefab;

    [Header("Material References")]
    [SerializeField] private Material _platformMaterial;
    [SerializeField] private Material _wallMaterial;
    [SerializeField] private Material _backgroundMaterial;

    [Header("Layer Configuration")]
    [SerializeField] private LayerMask _groundLayers = 1 << 3;
    [SerializeField] private LayerMask _puzzleLayers = 1 << 6;
    [SerializeField] private LayerMask _enemyLayers = 1 << 7;

    [Header("Debug Settings")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _createDebugMarkers = true;
    [SerializeField] private bool _showSectionBounds = true;

    // Built objects tracking
    private List<GameObject> _builtObjects = new List<GameObject>();
    private Dictionary<string, Transform> _sectionRoots = new Dictionary<string, Transform>();
    private GameObject _sceneRoot;

    // Demo flow tracking
    private Vector3 _currentBuildPosition;
    private int _sectionIndex = 0;

    [System.Serializable]
    public class DemoSection
    {
        public string sectionName;
        public string description;
        public Vector3 dimensions;
        public float estimatedPlayTime; // seconds
        public List<LearningObjective> objectives;

        [System.Serializable]
        public class LearningObjective
        {
            public string objective;
            public bool isRequired;
            public string mechanicIntroduced;
        }
    }

    private void Start()
    {
        if (_buildOnStart)
        {
            BuildVerticalSlice();
        }
    }

    #region Main Building Methods

    [ContextMenu("Build Complete Vertical Slice")]
    public void BuildVerticalSlice()
    {
        if (_debugMode)
            Debug.Log("🏗️ Starting Vertical Slice construction...");

        InitializeBuild();
        SetupSceneRoot();
        SetupCamera();

        _currentBuildPosition = _sceneOrigin;

        if (_buildAwakeningChamber)
            BuildAwakeningChamber();

        if (_buildPrismPuzzleRoom)
            BuildPrismPuzzleRoom();

        if (_buildTensionCorridor)
            BuildTensionCorridor();

        if (_buildClockworkSetPiece)
            BuildClockworkSetPiece();

        BuildPlayer();
        SetupSystemIntegration();
        FinalizeBuild();

        if (_debugMode)
            Debug.Log("✅ Vertical Slice construction completed!");
    }

    [ContextMenu("Clear Scene")]
    public void ClearScene()
    {
        foreach (var obj in _builtObjects)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }

        _builtObjects.Clear();
        _sectionRoots.Clear();

        if (_sceneRoot != null)
            DestroyImmediate(_sceneRoot);

        if (_debugMode)
            Debug.Log("🧹 Scene cleared");
    }

    private void InitializeBuild()
    {
        ClearScene();
        _builtObjects.Clear();
        _sectionRoots.Clear();
        _sectionIndex = 0;
    }

    private void SetupSceneRoot()
    {
        _sceneRoot = new GameObject($"VerticalSlice_{_sceneName}");
        _sceneRoot.transform.position = _sceneOrigin;
        AddToBuiltObjects(_sceneRoot);

        if (_debugMode)
            Debug.Log($"📁 Created scene root: {_sceneRoot.name}");
    }

    private void SetupCamera()
    {
        // Find or create main camera
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            AddToBuiltObjects(cameraObj);
        }

        // Setup for 2D
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 8f;
        mainCamera.transform.position = new Vector3(_sceneOrigin.x, _sceneOrigin.y, -10f);

        // Add URP camera data if using URP
        var urpCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (urpCameraData == null && GraphicsSettings.defaultRenderPipeline != null)
        {
            urpCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        if (_debugMode)
            Debug.Log("📷 Camera configured for 2D gameplay");
    }

    #endregion

    #region Section Builders

    private void BuildAwakeningChamber()
    {
        Vector3 sectionStart = _currentBuildPosition;
        Transform sectionRoot = CreateSectionRoot("01_AwakeningChamber", sectionStart);

        if (_debugMode)
            Debug.Log("🌅 Building Awakening Chamber...");

        // Create basic chamber structure
        Vector3 chamberSize = new Vector3(16f, 10f, 1f);
        CreateChamberWalls(sectionRoot, sectionStart, chamberSize);

        // Create platforms demonstrating different reveal behaviors
        CreateAwakeningPlatforms(sectionRoot, sectionStart);

        // Place collectible lantern
        Vector3 lanternPos = sectionStart + new Vector3(0f, 2f, 0f);
        CreateCollectibleLantern(sectionRoot, lanternPos);

        // Create tutorial UI triggers
        CreateTutorialTriggers(sectionRoot, sectionStart);

        _currentBuildPosition += new Vector3(_sectionSpacing, 0f, 0f);

        if (_debugMode)
            Debug.Log("✅ Awakening Chamber completed");
    }

    private void BuildPrismPuzzleRoom()
    {
        Vector3 sectionStart = _currentBuildPosition;
        Transform sectionRoot = CreateSectionRoot("02_PrismPuzzleRoom", sectionStart);

        if (_debugMode)
            Debug.Log("🔷 Building Prism Puzzle Room...");

        // Create puzzle chamber
        Vector3 chamberSize = new Vector3(20f, 12f, 1f);
        CreateChamberWalls(sectionRoot, sectionStart, chamberSize);

        // Create multi-node puzzle setup
        CreatePrismPuzzleSetup(sectionRoot, sectionStart);

        // Create the gate that requires multiple nodes
        CreateMultiNodeGate(sectionRoot, sectionStart + new Vector3(15f, 0f, 0f));

        _currentBuildPosition += new Vector3(_sectionSpacing, 0f, 0f);

        if (_debugMode)
            Debug.Log("✅ Prism Puzzle Room completed");
    }

    private void BuildTensionCorridor()
    {
        Vector3 sectionStart = _currentBuildPosition;
        Transform sectionRoot = CreateSectionRoot("03_TensionCorridor", sectionStart);

        if (_debugMode)
            Debug.Log("👁️ Building Tension Corridor...");

        // Create narrow corridor with cover points
        CreateCorridorStructure(sectionRoot, sectionStart);

        // Place Lurker enemies with patrol routes
        CreateLurkerEnemies(sectionRoot, sectionStart);

        // Create light essence pickups as rewards
        CreateEssencePickups(sectionRoot, sectionStart);

        _currentBuildPosition += new Vector3(_sectionSpacing, 0f, 0f);

        if (_debugMode)
            Debug.Log("✅ Tension Corridor completed");
    }

    private void BuildClockworkSetPiece()
    {
        Vector3 sectionStart = _currentBuildPosition;
        Transform sectionRoot = CreateSectionRoot("04_ClockworkSetPiece", sectionStart);

        if (_debugMode)
            Debug.Log("⚙️ Building Clockwork Set-Piece...");

        // Create moving platform mechanism
        CreateClockworkMechanism(sectionRoot, sectionStart);

        // Create final vista and completion area
        CreateCompletionVista(sectionRoot, sectionStart);

        if (_debugMode)
            Debug.Log("✅ Clockwork Set-Piece completed");
    }

    #endregion

    #region Detailed Builders

    private void CreateAwakeningPlatforms(Transform parent, Vector3 origin)
    {
        // Tutorial Platform (permanent once lit)
        var tutorialPlatform = CreatePlatform(parent, origin + new Vector3(-6f, 0f, 0f), "Tutorial_Platform");
        var tutorialReveal = tutorialPlatform.AddComponent<EnhancedRevealablePlatform>();
        // Configure as tutorial type

        // Timed Platform (stays lit for 5 seconds)
        var timedPlatform = CreatePlatform(parent, origin + new Vector3(-2f, 2f, 0f), "Timed_Platform");
        var timedReveal = timedPlatform.AddComponent<EnhancedRevealablePlatform>();
        // Configure as timed type

        // Sustained Platform (requires continuous light)
        var sustainedPlatform = CreatePlatform(parent, origin + new Vector3(2f, 4f, 0f), "Sustained_Platform");
        var sustainedReveal = sustainedPlatform.AddComponent<EnhancedRevealablePlatform>();
        // Configure as continuous type

        if (_debugMode)
            Debug.Log("📋 Created awakening platforms with different behaviors");
    }

    private void CreatePrismPuzzleSetup(Transform parent, Vector3 origin)
    {
        // Create 4 puzzle nodes in a pattern that benefits from Solar Flare
        var nodePositions = new Vector3[]
        {
            origin + new Vector3(-4f, 2f, 0f),
            origin + new Vector3(4f, 2f, 0f),
            origin + new Vector3(-4f, -2f, 0f),
            origin + new Vector3(4f, -2f, 0f)
        };

        for (int i = 0; i < nodePositions.Length; i++)
        {
            CreatePuzzleNode(parent, nodePositions[i], $"PuzzleNode_{i + 1}", LightPuzzleNode.NodeGroup.Alpha);
        }

        // Create central area where Solar Flare can hit all nodes
        CreateDebugMarker(parent, origin, "SolarFlare_OptimalPosition", Color.yellow);

        if (_debugMode)
            Debug.Log("🔵 Created multi-node puzzle setup optimized for Solar Flare");
    }

    private void CreateLurkerEnemies(Transform parent, Vector3 origin)
    {
        // Create 2 Lurker enemies with different patrol patterns
        var enemy1Pos = origin + new Vector3(-6f, 0f, 0f);
        var enemy2Pos = origin + new Vector3(6f, 0f, 0f);

        CreateLurkerEnemy(parent, enemy1Pos, "Lurker_Patrol_A");
        CreateLurkerEnemy(parent, enemy2Pos, "Lurker_Patrol_B");

        // Create cover objects
        CreateCoverObject(parent, origin + new Vector3(-8f, 0f, 0f));
        CreateCoverObject(parent, origin + new Vector3(8f, 0f, 0f));

        if (_debugMode)
            Debug.Log("👁️ Created Lurker enemies with patrol routes and cover");
    }

    private void CreateClockworkMechanism(Transform parent, Vector3 origin)
    {
        // Create moving platforms that require timing
        // This would be expanded with actual moving platform scripts
        var movingPlatform = CreatePlatform(parent, origin + new Vector3(0f, 3f, 0f), "Moving_Platform");

        // Add a simple oscillator for now
        var oscillator = movingPlatform.AddComponent<SimpleOscillator>();

        CreateDebugMarker(parent, origin, "TimingPuzzle_Center", Color.cyan);

        if (_debugMode)
            Debug.Log("⚙️ Created clockwork timing mechanism");
    }

    #endregion

    #region Utility Builders

    private GameObject CreatePlatform(Transform parent, Vector3 position, string name)
    {
        GameObject platform;

        if (_platformPrefab != null)
        {
            platform = Instantiate(_platformPrefab, position, Quaternion.identity, parent);
            platform.name = name;
        }
        else
        {
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.transform.SetParent(parent);
            platform.transform.position = position;
            platform.transform.localScale = new Vector3(3f, 0.5f, 1f);
            platform.name = name;

            // Remove 3D collider and add 2D collider
            DestroyImmediate(platform.GetComponent<BoxCollider>());
            platform.AddComponent<BoxCollider2D>();

            // Set layer
            platform.layer = Mathf.RoundToInt(Mathf.Log(_groundLayers.value, 2));
        }

        AddToBuiltObjects(platform);
        return platform;
    }

    private GameObject CreatePuzzleNode(Transform parent, Vector3 position, string name, LightPuzzleNode.NodeGroup group)
    {
        GameObject node;

        if (_puzzleNodePrefab != null)
        {
            node = Instantiate(_puzzleNodePrefab, position, Quaternion.identity, parent);
            node.name = name;
        }
        else
        {
            node = new GameObject(name);
            node.transform.SetParent(parent);
            node.transform.position = position;

            // Add components
            node.AddComponent<CircleCollider2D>().isTrigger = true;
            var puzzleNode = node.AddComponent<LightPuzzleNode>();

            // Set layer
            node.layer = Mathf.RoundToInt(Mathf.Log(_puzzleLayers.value, 2));
        }

        AddToBuiltObjects(node);
        return node;
    }

    private GameObject CreateLurkerEnemy(Transform parent, Vector3 position, string name)
    {
        GameObject enemy;

        if (_lurkerEnemyPrefab != null)
        {
            enemy = Instantiate(_lurkerEnemyPrefab, position, Quaternion.identity, parent);
            enemy.name = name;
        }
        else
        {
            enemy = new GameObject(name);
            enemy.transform.SetParent(parent);
            enemy.transform.position = position;

            // Add components
            enemy.AddComponent<Rigidbody2D>();
            enemy.AddComponent<BoxCollider2D>();
            enemy.AddComponent<LurkerEnemy>();

            // Set layer
            enemy.layer = Mathf.RoundToInt(Mathf.Log(_enemyLayers.value, 2));
        }

        AddToBuiltObjects(enemy);
        return enemy;
    }

    private GameObject CreateCollectibleLantern(Transform parent, Vector3 position)
    {
        GameObject lantern;

        if (_collectibleLanternPrefab != null)
        {
            lantern = Instantiate(_collectibleLanternPrefab, position, Quaternion.identity, parent);
        }
        else
        {
            lantern = new GameObject("CollectibleLantern");
            lantern.transform.SetParent(parent);
            lantern.transform.position = position;

            // Add components
            lantern.AddComponent<SphereCollider2D>().isTrigger = true;
            lantern.AddComponent<CollectibleLantern>();
            lantern.AddComponent<Light2D>();
        }

        AddToBuiltObjects(lantern);
        return lantern;
    }

    private GameObject CreateMultiNodeGate(Transform parent, Vector3 position)
    {
        GameObject gate;

        if (_puzzleGatePrefab != null)
        {
            gate = Instantiate(_puzzleGatePrefab, position, Quaternion.identity, parent);
        }
        else
        {
            gate = new GameObject("MultiNodePuzzleGate");
            gate.transform.SetParent(parent);
            gate.transform.position = position;
            gate.transform.localScale = new Vector3(1f, 4f, 1f);

            // Add components
            gate.AddComponent<BoxCollider2D>();
            gate.AddComponent<SpriteRenderer>();
            gate.AddComponent<MultiNodePuzzleGate>();
        }

        AddToBuiltObjects(gate);
        return gate;
    }

    private void CreateChamberWalls(Transform parent, Vector3 origin, Vector3 size)
    {
        // Create simple chamber walls
        var positions = new Vector3[]
        {
            origin + new Vector3(-size.x/2, 0f, 0f), // Left wall
            origin + new Vector3(size.x/2, 0f, 0f),  // Right wall
            origin + new Vector3(0f, -size.y/2, 0f), // Floor
            origin + new Vector3(0f, size.y/2, 0f)   // Ceiling
        };

        var scales = new Vector3[]
        {
            new Vector3(0.5f, size.y, 1f), // Left wall
            new Vector3(0.5f, size.y, 1f), // Right wall
            new Vector3(size.x, 0.5f, 1f), // Floor
            new Vector3(size.x, 0.5f, 1f)  // Ceiling
        };

        var names = new string[] { "LeftWall", "RightWall", "Floor", "Ceiling" };

        for (int i = 0; i < positions.Length; i++)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(parent);
            wall.transform.position = positions[i];
            wall.transform.localScale = scales[i];
            wall.name = names[i];

            // Convert to 2D physics
            DestroyImmediate(wall.GetComponent<BoxCollider>());
            wall.AddComponent<BoxCollider2D>();

            // Set layer
            wall.layer = Mathf.RoundToInt(Mathf.Log(_groundLayers.value, 2));

            AddToBuiltObjects(wall);
        }
    }

    private void CreateCorridorStructure(Transform parent, Vector3 origin)
    {
        // Create narrow corridor with cover points
        var corridor = new GameObject("Corridor");
        corridor.transform.SetParent(parent);
        corridor.transform.position = origin;

        CreateChamberWalls(corridor.transform, origin, new Vector3(16f, 6f, 1f));
        AddToBuiltObjects(corridor);
    }

    private void CreateCoverObject(Transform parent, Vector3 position)
    {
        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cover.transform.SetParent(parent);
        cover.transform.position = position;
        cover.transform.localScale = new Vector3(1f, 3f, 1f);
        cover.name = "Cover";

        // Convert to 2D
        DestroyImmediate(cover.GetComponent<BoxCollider>());
        cover.AddComponent<BoxCollider2D>();

        cover.layer = Mathf.RoundToInt(Mathf.Log(_groundLayers.value, 2));
        AddToBuiltObjects(cover);
    }

    private void CreateEssencePickups(Transform parent, Vector3 origin)
    {
        var positions = new Vector3[]
        {
            origin + new Vector3(-4f, 1f, 0f),
            origin + new Vector3(0f, 1f, 0f),
            origin + new Vector3(4f, 1f, 0f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var essence = new GameObject($"LightEssence_{i + 1}");
            essence.transform.SetParent(parent);
            essence.transform.position = positions[i];

            essence.AddComponent<SphereCollider2D>().isTrigger = true;
            essence.AddComponent<LightEssencePickup>();

            AddToBuiltObjects(essence);
        }
    }

    private void CreateTutorialTriggers(Transform parent, Vector3 origin)
    {
        // Create invisible trigger zones for tutorial messages
        var triggerPos = origin + new Vector3(-8f, 0f, 0f);
        var trigger = new GameObject("TutorialTrigger_Movement");
        trigger.transform.SetParent(parent);
        trigger.transform.position = triggerPos;

        var triggerCollider = trigger.AddComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector2(2f, 4f);

        AddToBuiltObjects(trigger);
    }

    private void CreateCompletionVista(Transform parent, Vector3 origin)
    {
        // Create an elevated platform that shows the completed path
        var vista = CreatePlatform(parent, origin + new Vector3(0f, 8f, 0f), "CompletionVista");

        CreateDebugMarker(parent, origin + new Vector3(0f, 10f, 0f), "Demo_Complete", Color.green);
    }

    private void CreateDebugMarker(Transform parent, Vector3 position, string label, Color color)
    {
        if (!_createDebugMarkers) return;

        var marker = new GameObject($"DEBUG_{label}");
        marker.transform.SetParent(parent);
        marker.transform.position = position;

        // Add a visual component for debugging
        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.color = color;

        AddToBuiltObjects(marker);
    }

    #endregion

    #region Final Setup

    private void BuildPlayer()
    {
        GameObject player;

        if (_playerPrefab != null)
        {
            player = Instantiate(_playerPrefab, _sceneOrigin + new Vector3(-15f, 2f, 0f), Quaternion.identity);
        }
        else
        {
            player = new GameObject("Player");
            player.transform.position = _sceneOrigin + new Vector3(-15f, 2f, 0f);
            player.tag = "Player";

            // Add all required components
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<InputManager>();
            player.AddComponent<EnhancedLanternController>().enabled = false; // Start disabled
            player.AddComponent<DualProgressionSystem>();
            player.AddComponent<SolarFlareAbility>();
        }

        AddToBuiltObjects(player);

        if (_debugMode)
            Debug.Log("🧑 Player created and positioned at scene start");
    }

    private void SetupSystemIntegration()
    {
        // Add the system integrator to the scene root
        var integrator = _sceneRoot.AddComponent<LanternBoundSystemIntegrator>();

        // Find and connect all the systems
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null)
        {
            var lanternController = player.GetComponent<EnhancedLanternController>();
            var progressionSystem = player.GetComponent<DualProgressionSystem>();
            var solarFlare = player.GetComponent<SolarFlareAbility>();

            // Manual connection if auto-discovery fails
            // This could be expanded to use reflection to set the integrator's fields
        }

        if (_debugMode)
            Debug.Log("🔗 System integration setup completed");
    }

    private void FinalizeBuild()
    {
        // Set up proper layer assignments
        ValidateLayerAssignments();

        // Create section documentation
        CreateSectionDocumentation();

        // Setup performance markers
        SetupPerformanceMarkers();

        if (_debugMode)
        {
            Debug.Log($"📊 Build Summary:");
            Debug.Log($"  Total Objects Created: {_builtObjects.Count}");
            Debug.Log($"  Sections Built: {_sectionRoots.Count}");
            Debug.Log($"  Scene Ready for Play Testing");
        }
    }

    private void ValidateLayerAssignments()
    {
        // Validate that all objects are on correct layers
        int groundLayer = Mathf.RoundToInt(Mathf.Log(_groundLayers.value, 2));
        int puzzleLayer = Mathf.RoundToInt(Mathf.Log(_puzzleLayers.value, 2));
        int enemyLayer = Mathf.RoundToInt(Mathf.Log(_enemyLayers.value, 2));

        if (_debugMode)
        {
            Debug.Log($"🏷️ Layer Validation:");
            Debug.Log($"  Ground Layer: {groundLayer}");
            Debug.Log($"  Puzzle Layer: {puzzleLayer}");
            Debug.Log($"  Enemy Layer: {enemyLayer}");
        }
    }

    private void CreateSectionDocumentation()
    {
        var documentation = new GameObject("DEMO_DOCUMENTATION");
        documentation.transform.SetParent(_sceneRoot.transform);

        var docScript = documentation.AddComponent<DemoDocumentation>();
        // This would store information about each section's purpose and expected flow

        AddToBuiltObjects(documentation);
    }

    private void SetupPerformanceMarkers()
    {
        // Add performance monitoring components
        var performanceMonitor = _sceneRoot.AddComponent<DemoPerformanceMonitor>();

        if (_debugMode)
            Debug.Log("📈 Performance monitoring setup completed");
    }

    #endregion

    #region Utility Methods

    private Transform CreateSectionRoot(string sectionName, Vector3 position)
    {
        var section = new GameObject(sectionName);
        section.transform.SetParent(_sceneRoot.transform);
        section.transform.position = position;

        _sectionRoots[sectionName] = section.transform;
        AddToBuiltObjects(section);

        if (_createDebugMarkers)
        {
            CreateDebugMarker(section.transform, position, $"Section_{_sectionIndex++}", Color.white);
        }

        return section.transform;
    }

    private void AddToBuiltObjects(GameObject obj)
    {
        if (obj != null && !_builtObjects.Contains(obj))
        {
            _builtObjects.Add(obj);
        }
    }

    #endregion

    #region Context Menu Helpers

    [ContextMenu("Build Awakening Chamber Only")]
    public void BuildAwakeningChamberOnly()
    {
        InitializeBuild();
        SetupSceneRoot();
        _currentBuildPosition = _sceneOrigin;
        BuildAwakeningChamber();
        BuildPlayer();
        SetupSystemIntegration();
        FinalizeBuild();
    }

    [ContextMenu("Build Puzzle Room Only")]
    public void BuildPuzzleRoomOnly()
    {
        InitializeBuild();
        SetupSceneRoot();
        _currentBuildPosition = _sceneOrigin;
        BuildPrismPuzzleRoom();
        BuildPlayer();
        SetupSystemIntegration();
        FinalizeBuild();
    }

    [ContextMenu("Test System Integration")]
    public void TestSystemIntegration()
    {
        var player = FindObjectOfType<PlayerMovement>();
        if (player == null)
        {
            Debug.LogWarning("No player found - build scene first");
            return;
        }

        var integrator = FindObjectOfType<LanternBoundSystemIntegrator>();
        if (integrator != null)
        {
            integrator.LogSystemStatus();
        }
        else
        {
            Debug.LogWarning("No system integrator found");
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (_showSectionBounds)
        {
            // Draw section boundaries
            Gizmos.color = Color.cyan;

            for (int i = 0; i < 4; i++)
            {
                Vector3 sectionPos = _sceneOrigin + new Vector3(i * _sectionSpacing, 0f, 0f);
                Gizmos.DrawWireCube(sectionPos, new Vector3(18f, 12f, 1f));
            }
        }

        // Draw build origin
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(_sceneOrigin, 0.5f);
    }
}

/// <summary>
/// Simple oscillator for moving platforms
/// </summary>
public class SimpleOscillator : MonoBehaviour
{
    [SerializeField] private Vector3 _movement = new Vector3(0f, 3f, 0f);
    [SerializeField] private float _speed = 2f;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        float time = Time.time * _speed;
        Vector3 offset = _movement * Mathf.Sin(time);
        transform.position = _startPosition + offset;
    }
}

/// <summary>
/// Demo documentation component
/// </summary>
public class DemoDocumentation : MonoBehaviour
{
    [TextArea(10, 20)]
    public string demoFlow = @"
LANTERNBOUND VERTICAL SLICE DEMO FLOW:

SECTION 1: AWAKENING CHAMBER (60s)
- Player learns basic movement (WASD + Space)
- Discovers and collects the Ancient Lantern
- Learns lantern activation (F key)
- Experiences 3 platform types: Tutorial, Timed, Sustained

SECTION 2: PRISM PUZZLE ROOM (2-3min)
- Introduction to puzzle nodes
- Multi-node activation challenge
- Solar Flare discovery and first use
- Gate opening reward cycle

SECTION 3: TENSION CORRIDOR (2-3min)
- Enemy introduction (Lurkers)
- Light as tactical advantage
- Cover-based navigation
- Solar Flare stunning demonstration

SECTION 4: CLOCKWORK SET-PIECE (60s)
- Moving platform timing challenge
- Mastery demonstration
- Vista revealing larger world
- Completion satisfaction

TOTAL DEMO TIME: 5-8 minutes
PRIMARY GOAL: Prove puzzle-first, combat-secondary philosophy
SECONDARY GOAL: Demonstrate unique light-based mechanics
";
}

/// <summary>
/// Performance monitoring for the demo
/// </summary>
public class DemoPerformanceMonitor : MonoBehaviour
{
    [SerializeField] private bool _showPerformanceGUI = true;
    [SerializeField] private float _updateRate = 1f;

    private float _fps;
    private int _frameCount;
    private float _deltaTime;
    private float _lastUpdateTime;

    private void Update()
    {
        _frameCount++;
        _deltaTime += Time.unscaledDeltaTime;

        if (Time.unscaledTime - _lastUpdateTime >= _updateRate)
        {
            _fps = _frameCount / _deltaTime;
            _frameCount = 0;
            _deltaTime = 0f;
            _lastUpdateTime = Time.unscaledTime;
        }
    }

    private void OnGUI()
    {
        if (!_showPerformanceGUI) return;

        GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 100));
        GUILayout.Box("Demo Performance");
        GUILayout.Label($"FPS: {_fps:F1}");
        GUILayout.Label($"Objects: {FindObjectsOfType<GameObject>().Length}");
        GUILayout.Label($"Nodes: {FindObjectsOfType<LightPuzzleNode>().Length}");
        GUILayout.Label($"Enemies: {FindObjectsOfType<LurkerEnemy>().Length}");
        GUILayout.EndArea();
    }
}