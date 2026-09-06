using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Automated builder for LanternBound Opening Area
/// Creates all 6 sections with proper spacing, platforms, and geometry
/// Run from: Tools > LanternBound > Build Opening Area
/// </summary>
public class OpeningAreaBuilder : MonoBehaviour
{
    [Header("Building Configuration")]
    [SerializeField] private bool _buildOnStart = false;
    [SerializeField] private Material _platformMaterial;
    [SerializeField] private Color _platformColor = Color.gray;

    [Header("Section Toggles")]
    [SerializeField] private bool _buildAwakeningChamber = true;
    [SerializeField] private bool _buildCallingPath = true;
    [SerializeField] private bool _buildDescent = true;
    [SerializeField] private bool _buildChase = true;
    [SerializeField] private bool _buildLanternChamber = true;
    [SerializeField] private bool _buildFirstPuzzle = true;

    [Header("Layers")]
    [SerializeField] private string _groundLayerName = "Ground";

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    // Unity units per world unit
    private const float UNIT = 1f;

    // Current build position (x-axis)
    private float _currentX = 0f;

    private void Start()
    {
        if (_buildOnStart)
        {
            BuildCompleteOpeningArea();
        }
    }

    /// <summary>
    /// Build the entire opening area sequence
    /// </summary>
    public void BuildCompleteOpeningArea()
    {
        Debug.Log("═══════════════════════════════════");
        Debug.Log("🏗️ BUILDING OPENING AREA");
        Debug.Log("═══════════════════════════════════");

        _currentX = 0f;

        // Clear existing children
        ClearExistingStructure();

        if (_buildAwakeningChamber)
            BuildSection1_AwakeningChamber();

        if (_buildCallingPath)
            BuildSection2_CallingPath();

        if (_buildDescent)
            BuildSection3_Descent();

        if (_buildChase)
            BuildSection4_Chase();

        if (_buildLanternChamber)
            BuildSection5_LanternChamber();

        if (_buildFirstPuzzle)
            BuildSection6_FirstPuzzle();

        Debug.Log("═══════════════════════════════════");
        Debug.Log($"✅ Opening area built! Total length: {_currentX} units");
        Debug.Log("═══════════════════════════════════");
    }

    #region Section 1: Awakening Chamber

    private void BuildSection1_AwakeningChamber()
    {
        LogSection("SECTION 1: AWAKENING CHAMBER");

        GameObject section = CreateSectionParent("01_AwakeningChamber");

        // Main floor (20 units wide × 1 unit tall)
        CreatePlatform("MainFloor", section.transform,
            new Vector3(_currentX + 10f * UNIT, 0f, 0f),
            new Vector2(20f * UNIT, 1f * UNIT));

        // Small step down (teaches jumping)
        CreatePlatform("SmallStep", section.transform,
            new Vector3(_currentX + 15f * UNIT, -1.5f * UNIT, 0f),
            new Vector2(3f * UNIT, 0.5f * UNIT));

        // Gentle slope platforms (teach movement)
        CreatePlatform("SlopeStep1", section.transform,
            new Vector3(_currentX + 18f * UNIT, -1f * UNIT, 0f),
            new Vector2(2f * UNIT, 0.5f * UNIT));

        CreatePlatform("SlopeStep2", section.transform,
            new Vector3(_currentX + 20f * UNIT, -0.5f * UNIT, 0f),
            new Vector2(2f * UNIT, 0.5f * UNIT));

        // Walls (bounds)
        CreateWall("LeftWall", section.transform,
            new Vector3(_currentX, 4f * UNIT, 0f),
            new Vector2(0.5f * UNIT, 8f * UNIT));

        CreateWall("RightWall", section.transform,
            new Vector3(_currentX + 22f * UNIT, 4f * UNIT, 0f),
            new Vector2(0.5f * UNIT, 8f * UNIT));

        // Add spawn point marker
        GameObject spawnPoint = new GameObject("PlayerSpawnPoint");
        spawnPoint.transform.SetParent(section.transform);
        spawnPoint.transform.position = new Vector3(_currentX + 10f * UNIT, 1.5f * UNIT, 0f);
        spawnPoint.tag = "Respawn";

        // Add visual marker for spawn
        var spawnMarker = spawnPoint.AddComponent<SpriteRenderer>();
        spawnMarker.color = Color.green;
        // Create a simple quad for the marker
        spawnMarker.sprite = CreateSimpleSprite();
        spawnPoint.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        _currentX += 22f * UNIT;
        LogSectionComplete("Awakening Chamber", 22f);
    }

    #endregion

    #region Section 2: Calling Path

    private void BuildSection2_CallingPath()
    {
        LogSection("SECTION 2: CALLING PATH");

        GameObject section = CreateSectionParent("02_CallingPath");

        // Platform 1 (small landing)
        CreatePlatform("Platform1", section.transform,
            new Vector3(_currentX + 3f * UNIT, 0f, 0f),
            new Vector2(3f * UNIT, 1f * UNIT));

        // Gap 1 (3 units - simple jump)

        // Platform 2 (smaller, requires accuracy)
        CreatePlatform("Platform2", section.transform,
            new Vector3(_currentX + 9f * UNIT, 0.5f * UNIT, 0f),
            new Vector2(2f * UNIT, 1f * UNIT));

        // Wall for wall jump
        CreateWall("WallJumpWall", section.transform,
            new Vector3(_currentX + 12f * UNIT, 3.5f * UNIT, 0f),
            new Vector2(1f * UNIT, 6f * UNIT));

        // Platform 3 (after wall jump)
        CreatePlatform("Platform3", section.transform,
            new Vector3(_currentX + 16f * UNIT, 1f * UNIT, 0f),
            new Vector2(4f * UNIT, 1f * UNIT));

        // Longer gap (requires running jump or dash)

        // Platform 4 (calling source)
        CreatePlatform("Platform4_CallingSource", section.transform,
            new Vector3(_currentX + 25f * UNIT, 1.5f * UNIT, 0f),
            new Vector2(5f * UNIT, 1f * UNIT));

        // Add calling source marker
        GameObject callingMarker = new GameObject("CallingSourceMarker");
        callingMarker.transform.SetParent(section.transform);
        callingMarker.transform.position = new Vector3(_currentX + 25f * UNIT, 3f * UNIT, 0f);

        var callingSprite = callingMarker.AddComponent<SpriteRenderer>();
        callingSprite.color = new Color(0.5f, 0.5f, 1f, 0.5f); // Pale blue
        callingSprite.sprite = CreateSimpleSprite();
        callingMarker.transform.localScale = new Vector3(1f, 1f, 1f);

        _currentX += 30f * UNIT;
        LogSectionComplete("Calling Path", 30f);
    }

    #endregion

    #region Section 3: Descent

    private void BuildSection3_Descent()
    {
        LogSection("SECTION 3: THE DESCENT");

        GameObject section = CreateSectionParent("03_Descent");

        // Overlook platform (vista view)
        CreatePlatform("OverlookPlatform", section.transform,
            new Vector3(_currentX + 3f * UNIT, 2f * UNIT, 0f),
            new Vector2(6f * UNIT, 1f * UNIT));

        // Descending platforms (staggered down 20 units)
        float descentStartX = _currentX + 8f * UNIT;
        float descentY = 0f;
        float descentXStep = 3f * UNIT;
        float descentYStep = -3.5f * UNIT;

        for (int i = 0; i < 6; i++)
        {
            CreatePlatform($"DescentPlatform{i + 1}", section.transform,
                new Vector3(descentStartX + (i * descentXStep), descentY + (i * descentYStep), 0f),
                new Vector2(3f * UNIT, 1f * UNIT));
        }

        // Bottom landing platform
        CreatePlatform("BottomLanding", section.transform,
            new Vector3(_currentX + 28f * UNIT, -20f * UNIT, 0f),
            new Vector2(5f * UNIT, 1f * UNIT));

        // Add vista marker
        GameObject vistaMarker = new GameObject("VistaViewPoint");
        vistaMarker.transform.SetParent(section.transform);
        vistaMarker.transform.position = new Vector3(_currentX + 3f * UNIT, 4f * UNIT, 0f);
        vistaMarker.tag = "EditorOnly";

        _currentX += 32f * UNIT;
        LogSectionComplete("Descent", 32f);
    }

    #endregion

    #region Section 4: Chase Sequence

    private void BuildSection4_Chase()
    {
        LogSection("SECTION 4: CHASE SEQUENCE");

        GameObject section = CreateSectionParent("04_ChaseSequence");

        // Starting platform (chase trigger here)
        CreatePlatform("ChaseStart", section.transform,
            new Vector3(_currentX + 3f * UNIT, -20f * UNIT, 0f),
            new Vector2(4f * UNIT, 1f * UNIT));

        // Add chase trigger marker
        GameObject triggerMarker = new GameObject("ChaseTriggerPoint");
        triggerMarker.transform.SetParent(section.transform);
        triggerMarker.transform.position = new Vector3(_currentX + 3f * UNIT, -19f * UNIT, 0f);
        triggerMarker.tag = "EditorOnly";

        // Narrow corridor platforms (requires fast movement)
        float chaseX = _currentX + 6f * UNIT;
        float chaseY = -20f * UNIT;
        float platformSpacing = 5f * UNIT;

        for (int i = 0; i < 8; i++)
        {
            // Vary platform height and width for interest
            float yVariation = Mathf.Sin(i * 0.5f) * 1.5f * UNIT;
            float platformWidth = (i % 2 == 0) ? 3f * UNIT : 2.5f * UNIT;

            CreatePlatform($"ChasePlatform{i + 1}", section.transform,
                new Vector3(chaseX + (i * platformSpacing), chaseY + yVariation, 0f),
                new Vector2(platformWidth, 1f * UNIT));
        }

        // Wall jump section (tall wall)
        CreateWall("ChaseWall", section.transform,
            new Vector3(_currentX + 30f * UNIT, -17f * UNIT, 0f),
            new Vector2(1f * UNIT, 5f * UNIT));

        // Platform after wall
        CreatePlatform("PostWallPlatform", section.transform,
            new Vector3(_currentX + 33f * UNIT, -15f * UNIT, 0f),
            new Vector2(3f * UNIT, 1f * UNIT));

        // Final sprint platforms (collapsing section)
        CreatePlatform("CollapsePlatform1", section.transform,
            new Vector3(_currentX + 38f * UNIT, -15f * UNIT, 0f),
            new Vector2(3f * UNIT, 1f * UNIT));

        CreatePlatform("CollapsePlatform2", section.transform,
            new Vector3(_currentX + 43f * UNIT, -14f * UNIT, 0f),
            new Vector2(3f * UNIT, 1f * UNIT));

        // Safe landing (lantern chamber entrance)
        CreatePlatform("SafeLanding", section.transform,
            new Vector3(_currentX + 48f * UNIT, -13f * UNIT, 0f),
            new Vector2(5f * UNIT, 1f * UNIT));

        // Add enemy spawn marker
        GameObject enemySpawn = new GameObject("ShadowEnemySpawnPoint");
        enemySpawn.transform.SetParent(section.transform);
        enemySpawn.transform.position = new Vector3(_currentX, -20f * UNIT, 0f);
        enemySpawn.tag = "EditorOnly";

        var enemyMarker = enemySpawn.AddComponent<SpriteRenderer>();
        enemyMarker.color = new Color(1f, 0f, 0f, 0.5f); // Red
        enemyMarker.sprite = CreateSimpleSprite();
        enemySpawn.transform.localScale = new Vector3(2f, 2f, 1f);

        _currentX += 52f * UNIT;
        LogSectionComplete("Chase Sequence", 52f);
    }

    #endregion

    #region Section 5: Lantern Chamber

    private void BuildSection5_LanternChamber()
    {
        LogSection("SECTION 5: LANTERN CHAMBER");

        GameObject section = CreateSectionParent("05_LanternChamber");

        // Entry ledge (safe from shadow enemy)
        CreatePlatform("EntryLedge", section.transform,
            new Vector3(_currentX + 3f * UNIT, -13f * UNIT, 0f),
            new Vector2(4f * UNIT, 1f * UNIT));

        // Small drop to central platform
        CreatePlatform("CentralPlatform", section.transform,
            new Vector3(_currentX + 12.5f * UNIT, -16f * UNIT, 0f),
            new Vector2(8f * UNIT, 1f * UNIT));

        // Decorative pillars (environmental storytelling)
        CreateWall("LeftPillar", section.transform,
            new Vector3(_currentX + 6f * UNIT, -13f * UNIT, 0f),
            new Vector2(1f * UNIT, 5f * UNIT));

        CreateWall("RightPillar", section.transform,
            new Vector3(_currentX + 19f * UNIT, -13f * UNIT, 0f),
            new Vector2(1f * UNIT, 5f * UNIT));

        // Lantern spawn point marker (hovering above platform)
        GameObject lanternPoint = new GameObject("LanternSpawnPoint");
        lanternPoint.transform.SetParent(section.transform);
        lanternPoint.transform.position = new Vector3(_currentX + 12.5f * UNIT, -14f * UNIT, 0f);
        lanternPoint.tag = "Respawn"; // Using Respawn tag for important markers

        var lanternMarker = lanternPoint.AddComponent<SpriteRenderer>();
        lanternMarker.color = new Color(1f, 0.9f, 0.3f, 0.7f); // Golden
        lanternMarker.sprite = CreateSimpleSprite();
        lanternPoint.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        // Exit platform (darkness wall will be here)
        CreatePlatform("ExitPlatform", section.transform,
            new Vector3(_currentX + 20f * UNIT, -16f * UNIT, 0f),
            new Vector2(5f * UNIT, 1f * UNIT));

        // Chamber bounds
        CreateWall("ChamberLeftWall", section.transform,
            new Vector3(_currentX, -10f * UNIT, 0f),
            new Vector2(0.5f * UNIT, 12f * UNIT));

        CreateWall("ChamberRightWall", section.transform,
            new Vector3(_currentX + 25f * UNIT, -10f * UNIT, 0f),
            new Vector2(0.5f * UNIT, 12f * UNIT));

        _currentX += 25f * UNIT;
        LogSectionComplete("Lantern Chamber", 25f);
    }

    #endregion

    #region Section 6: First Puzzle

    private void BuildSection6_FirstPuzzle()
    {
        LogSection("SECTION 6: FIRST PUZZLE");

        GameObject section = CreateSectionParent("06_FirstPuzzle");

        // Tutorial platform
        CreatePlatform("TutorialPlatform", section.transform,
            new Vector3(_currentX + 3f * UNIT, -16f * UNIT, 0f),
            new Vector2(5f * UNIT, 1f * UNIT));

        // Hidden platform 1 (obvious, with visual hint)
        GameObject hiddenPlat1 = CreatePlatform("HiddenPlatform1", section.transform,
            new Vector3(_currentX + 10f * UNIT, -16f * UNIT, 0f),
            new Vector2(3f * UNIT, 1f * UNIT));

        // Mark as hidden (will need EnhancedRevealablePlatform script)
        hiddenPlat1.name += " [NEEDS_REVEALABLE_SCRIPT]";
        SetPlatformColorAlpha(hiddenPlat1, 0.3f); // Semi-transparent hint

        // Hidden platform 2 (less obvious)
        GameObject hiddenPlat2 = CreatePlatform("HiddenPlatform2", section.transform,
            new Vector3(_currentX + 16f * UNIT, -15f * UNIT, 0f),
            new Vector2(2.5f * UNIT, 1f * UNIT));

        hiddenPlat2.name += " [NEEDS_REVEALABLE_SCRIPT]";
        SetPlatformColorAlpha(hiddenPlat2, 0.2f);

        // Timed platform (requires quick crossing)
        GameObject timedPlat = CreatePlatform("TimedPlatform", section.transform,
            new Vector3(_currentX + 21f * UNIT, -14f * UNIT, 0f),
            new Vector2(3f * UNIT, 1f * UNIT));

        timedPlat.name += " [NEEDS_TIMED_SCRIPT]";
        SetPlatformColorAlpha(timedPlat, 0.2f);

        // Sustained platform (must keep light on it)
        GameObject sustainedPlat = CreatePlatform("SustainedPlatform", section.transform,
            new Vector3(_currentX + 27f * UNIT, -13f * UNIT, 0f),
            new Vector2(4f * UNIT, 1f * UNIT));

        sustainedPlat.name += " [NEEDS_SUSTAINED_SCRIPT]";
        SetPlatformColorAlpha(sustainedPlat, 0.2f);

        // Exit platform (puzzle complete)
        CreatePlatform("PuzzleExitPlatform", section.transform,
            new Vector3(_currentX + 34f * UNIT, -12f * UNIT, 0f),
            new Vector2(6f * UNIT, 1f * UNIT));

        // Add visual markers for hidden platform locations
        CreateHintMarker("Hint1", section.transform,
            new Vector3(_currentX + 10f * UNIT, -14.5f * UNIT, 0f));

        CreateHintMarker("Hint2", section.transform,
            new Vector3(_currentX + 16f * UNIT, -13.5f * UNIT, 0f));

        _currentX += 40f * UNIT;
        LogSectionComplete("First Puzzle", 40f);
    }

    #endregion

    #region Helper Methods

    private GameObject CreateSectionParent(string sectionName)
    {
        GameObject section = new GameObject(sectionName);
        section.transform.SetParent(transform);
        section.transform.localPosition = Vector3.zero;
        return section;
    }

    private GameObject CreatePlatform(string name, Transform parent, Vector3 position, Vector2 size)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = name;
        platform.transform.SetParent(parent);
        platform.transform.position = position;
        platform.transform.localScale = new Vector3(size.x, size.y, 1f);

        // Remove 3D collider
        DestroyImmediate(platform.GetComponent<BoxCollider>());

        // Add 2D collider
        var collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 1f); // Scale already applied via transform

        // Set layer
        platform.layer = LayerMask.NameToLayer(_groundLayerName);

        // Visual
        var renderer = platform.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = _platformMaterial ?? new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = _platformColor;
        }

        return platform;
    }

    private GameObject CreateWall(string name, Transform parent, Vector3 position, Vector2 size)
    {
        // Walls are just tall platforms
        return CreatePlatform(name, parent, position, size);
    }

    private void CreateHintMarker(string name, Transform parent, Vector3 position)
    {
        GameObject marker = new GameObject(name + "_Marker");
        marker.transform.SetParent(parent);
        marker.transform.position = position;

        var sprite = marker.AddComponent<SpriteRenderer>();
        sprite.color = new Color(1f, 1f, 0.3f, 0.3f); // Faint yellow
        sprite.sprite = CreateSimpleSprite();
        marker.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        // Add particle system for sparkle effect
        var particles = marker.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startColor = new Color(1f, 1f, 0.5f, 0.5f);
        main.startSize = 0.1f;
        main.startLifetime = 1f;
        main.maxParticles = 10;

        var emission = particles.emission;
        emission.rateOverTime = 5f;
    }

    private void SetPlatformColorAlpha(GameObject platform, float alpha)
    {
        var renderer = platform.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color color = renderer.material.color;
            color.a = alpha;
            renderer.material.color = color;
        }
    }

    private Sprite CreateSimpleSprite()
    {
        // Create a simple 1x1 white texture for markers
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private void ClearExistingStructure()
    {
        // Remove all child objects
        int childCount = transform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        if (_showDebugInfo)
            Debug.Log("🗑️ Cleared existing structure");
    }

    private void LogSection(string sectionName)
    {
        if (_showDebugInfo)
        {
            Debug.Log($"───────────────────────────────────");
            Debug.Log($"🏗️ Building: {sectionName}");
        }
    }

    private void LogSectionComplete(string sectionName, float length)
    {
        if (_showDebugInfo)
        {
            Debug.Log($"✅ {sectionName} complete - {length} units");
        }
    }

    #endregion

    #region Editor Menu

#if UNITY_EDITOR
    [MenuItem("Tools/LanternBound/Build Opening Area")]
    private static void BuildFromMenu()
    {
        // Find or create builder object
        OpeningAreaBuilder builder = FindObjectOfType<OpeningAreaBuilder>();

        if (builder == null)
        {
            GameObject builderObj = new GameObject("OpeningAreaBuilder");
            builder = builderObj.AddComponent<OpeningAreaBuilder>();
            Debug.Log("✨ Created OpeningAreaBuilder GameObject");
        }

        // Build the area
        builder.BuildCompleteOpeningArea();

        // Select the builder in hierarchy
        Selection.activeGameObject = builder.gameObject;
    }

    [MenuItem("Tools/LanternBound/Clear Opening Area")]
    private static void ClearFromMenu()
    {
        OpeningAreaBuilder builder = FindObjectOfType<OpeningAreaBuilder>();

        if (builder != null)
        {
            builder.ClearExistingStructure();
            Debug.Log("🗑️ Opening area cleared");
        }
        else
        {
            Debug.LogWarning("⚠️ No OpeningAreaBuilder found in scene");
        }
    }
#endif

    #endregion
}