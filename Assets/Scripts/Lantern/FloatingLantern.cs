using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Rendering.Universal;

/// <summary>
/// FIXED: Separate lantern object with proper hierarchy search for EnhancedLanternController
/// </summary>
public class FloatingLantern : MonoBehaviour
{
    [Header("Lantern Positioning")]
    [SerializeField] private Transform _player;
    [SerializeField] private Vector3 _offsetFromPlayer = new Vector3(-1.2f, 0.8f, 0f);
    [SerializeField] private float _followSpeed = 8f;
    [SerializeField] private bool _smoothRotation = true;
    [SerializeField] private float _rotationSpeed = 5f;

    [Header("Floating Animation")]
    [SerializeField] private bool _enableFloating = true;
    [SerializeField] private float _floatAmplitude = 0.15f;
    [SerializeField] private float _floatFrequency = 2f;

    [Header("2D Lighting")]
    [SerializeField] private Light2D _lanternLight2D;

    [Header("Beam System")]
    [SerializeField] private LineRenderer _beamRenderer;
    [SerializeField] private Light _spotLight;
    [SerializeField] private Transform _beamOrigin;

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _glowParticles;
    [SerializeField] private SpriteRenderer _lanternSprite;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;

    // Private state
    private Vector3 _targetPosition;
    private Vector3 _currentVelocity;
    private Vector3 _baseOffset;
    private bool _isActive = false;
    private EnhancedLanternController _controller;

    private void Awake()
    {
        SetupLanternObject();

        // Start inactive
        SetLanternActive(false);

        if (_debugMode)
        {
            Debug.Log("✓ FloatingLantern initialized");
        }
    }

    private void Start()
    {
        // Find player if not assigned
        if (_player == null)
        {
            // FIXED: Search for player by finding PlayerMovement or EnhancedLanternController
            _player = FindPlayerTransform();
            if (_player == null)
            {
                Debug.LogError("❌ FloatingLantern: No player found!");
                return;
            }
        }

        // FIXED: Find controller using hierarchy search
        _controller = FindEnhancedLanternController(_player.gameObject);
        if (_controller == null)
        {
            Debug.LogError("❌ FloatingLantern: No EnhancedLanternController found in player hierarchy!");
        }

        _baseOffset = _offsetFromPlayer;
        UpdateTargetPosition();

        // Start at target position
        transform.position = _targetPosition;

        if (_debugMode)
        {
            Debug.Log($"✓ FloatingLantern setup complete");
            Debug.Log($"  Player: {_player.name}");
            Debug.Log($"  Controller: {(_controller != null ? _controller.name : "Not Found")}");
        }
    }

    /// <summary>
    /// FIXED: Find player transform by searching for known player components
    /// </summary>
    private Transform FindPlayerTransform()
    {
        // Try to find by PlayerMovement component
        var playerMovement = FindObjectOfType<PlayerMovement>();
        if (playerMovement != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found player via PlayerMovement: {playerMovement.name}");
            }
            return playerMovement.transform;
        }

        // Try to find by EnhancedLanternController
        var lanternController = FindObjectOfType<EnhancedLanternController>();
        if (lanternController != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found player via EnhancedLanternController: {lanternController.name}");
            }
            return lanternController.transform;
        }

        // Try to find by Player tag
        var playerByTag = GameObject.FindWithTag("Player");
        if (playerByTag != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found player via Player tag: {playerByTag.name}");
            }
            return playerByTag.transform;
        }

        return null;
    }

    /// <summary>
    /// FIXED: Search hierarchy to find EnhancedLanternController
    /// </summary>
    private EnhancedLanternController FindEnhancedLanternController(GameObject startObject)
    {
        if (_debugMode)
        {
            Debug.Log($"🔍 Searching for EnhancedLanternController starting from: {startObject.name}");
        }

        // Check current object
        var controller = startObject.GetComponent<EnhancedLanternController>();
        if (controller != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found EnhancedLanternController on: {startObject.name}");
            }
            return controller;
        }

        // Check parent hierarchy
        Transform current = startObject.transform.parent;
        while (current != null)
        {
            controller = current.GetComponent<EnhancedLanternController>();
            if (controller != null)
            {
                if (_debugMode)
                {
                    Debug.Log($"✓ Found EnhancedLanternController on parent: {current.name}");
                }
                return controller;
            }
            current = current.parent;
        }

        // Check children hierarchy
        controller = startObject.GetComponentInChildren<EnhancedLanternController>();
        if (controller != null)
        {
            if (_debugMode)
            {
                Debug.Log($"✓ Found EnhancedLanternController on child: {controller.name}");
            }
            return controller;
        }

        if (_debugMode)
        {
            Debug.LogError($"❌ No EnhancedLanternController found in hierarchy of: {startObject.name}");
        }
        return null;
    }

    private void Update()
    {
        if (_player == null) return;

        UpdateTargetPosition();
        UpdatePosition();
        UpdateRotation();

        if (_enableFloating && _isActive)
        {
            ApplyFloatingAnimation();
        }
    }

    private void SetupLanternObject()
    {
        // Setup 2D Light
        if (_lanternLight2D == null)
        {
            _lanternLight2D = GetComponent<Light2D>();
            if (_lanternLight2D == null)
            {
                _lanternLight2D = gameObject.AddComponent<Light2D>();
            }
        }

        _lanternLight2D.lightType = Light2D.LightType.Point;
        _lanternLight2D.intensity = 2f;
        _lanternLight2D.pointLightInnerRadius = 0.5f;
        _lanternLight2D.pointLightOuterRadius = 8f;
        _lanternLight2D.color = new Color(1f, 0.9f, 0.6f);

        // Setup beam origin
        if (_beamOrigin == null)
        {
            GameObject beamOriginObj = new GameObject("BeamOrigin");
            beamOriginObj.transform.SetParent(transform);
            beamOriginObj.transform.localPosition = Vector3.zero;
            _beamOrigin = beamOriginObj.transform;
        }

        // Setup beam renderer
        if (_beamRenderer == null)
        {
            GameObject beamObj = new GameObject("LanternBeam");
            beamObj.transform.SetParent(_beamOrigin);
            _beamRenderer = beamObj.AddComponent<LineRenderer>();
        }

        _beamRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _beamRenderer.startWidth = 0.05f;
        _beamRenderer.endWidth = 0.8f;
        _beamRenderer.positionCount = 2;
        _beamRenderer.startColor = new Color(1f, 0.9f, 0.6f, 0.7f);

        // Setup spotlight
        if (_spotLight == null)
        {
            _spotLight = _beamOrigin.gameObject.AddComponent<Light>();
        }

        _spotLight.type = UnityEngine.LightType.Spot;
        _spotLight.color = new Color(1f, 0.9f, 0.6f);
        _spotLight.intensity = 1.5f;
        _spotLight.range = 10f;
        _spotLight.spotAngle = 30f;

        // Setup lantern sprite (simple circle for now)
        if (_lanternSprite == null)
        {
            _lanternSprite = GetComponent<SpriteRenderer>();
            if (_lanternSprite == null)
            {
                _lanternSprite = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        // Create a simple circle sprite
        _lanternSprite.sprite = CreateCircleSprite();
        _lanternSprite.color = new Color(1f, 0.9f, 0.6f, 0.8f);
    }

    private Sprite CreateCircleSprite()
    {
        // Create a simple circle texture for the lantern
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.4f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance <= radius)
                {
                    float alpha = 1f - (distance / radius) * 0.3f;
                    pixels[y * size + x] = new Color(1f, 0.9f, 0.6f, alpha);
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

    private void UpdateTargetPosition()
    {
        if (_player == null) return;

        // Calculate target position relative to player
        Vector3 worldOffset = _player.TransformDirection(_baseOffset);
        _targetPosition = _player.position + worldOffset;
    }

    private void UpdatePosition()
    {
        // Smooth follow movement
        transform.position = Vector3.SmoothDamp(
            transform.position,
            _targetPosition,
            ref _currentVelocity,
            1f / _followSpeed
        );
    }

    private void UpdateRotation()
    {
        if (!_smoothRotation || !_isActive) return;

        // Get mouse position for aiming
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        // Calculate direction from lantern to mouse
        Vector2 aimDirection = ((Vector2)mouseWorldPos - (Vector2)_beamOrigin.position).normalized;

        // Rotate lantern to face aim direction (optional - makes it feel more responsive)
        if (aimDirection.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.z;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, _rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.AngleAxis(newAngle, Vector3.forward);
        }
    }

    private void ApplyFloatingAnimation()
    {
        // Add subtle floating motion
        float floatOffset = Mathf.Sin(Time.time * _floatFrequency) * _floatAmplitude;
        Vector3 floatPosition = _targetPosition + Vector3.up * floatOffset;

        // Blend floating with smooth following
        Vector3 currentPos = transform.position;
        currentPos.y = Mathf.Lerp(currentPos.y, floatPosition.y, Time.deltaTime * _followSpeed);
        transform.position = currentPos;
    }

    public void SetLanternActive(bool active)
    {
        _isActive = active;

        if (_lanternLight2D != null)
            _lanternLight2D.enabled = active;

        if (_beamRenderer != null)
            _beamRenderer.enabled = active;

        if (_spotLight != null)
            _spotLight.enabled = active;

        if (_glowParticles != null)
        {
            if (active)
                _glowParticles.Play();
            else
                _glowParticles.Stop();
        }

        if (_debugMode)
        {
            Debug.Log($"🔦 FloatingLantern set active: {active}");
        }
    }

    public void UpdateBeam(Vector2 direction, float range, Color beamColor)
    {
        if (!_isActive || _beamRenderer == null || _beamOrigin == null) return;

        // Update beam visuals
        Vector3 startPos = _beamOrigin.position;
        Vector3 endPos = startPos + (Vector3)(direction * range);

        _beamRenderer.SetPosition(0, startPos);
        _beamRenderer.SetPosition(1, endPos);
        _beamRenderer.startColor = beamColor;

        // Update spotlight
        if (_spotLight != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _spotLight.transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
            _spotLight.color = beamColor;
        }
    }

    public Vector3 GetBeamOrigin()
    {
        return _beamOrigin != null ? _beamOrigin.position : transform.position;
    }

    public Transform GetBeamOriginTransform()
    {
        return _beamOrigin;
    }

    private void OnDrawGizmosSelected()
    {
        if (_player == null) return;

        // Show target position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_targetPosition, 0.2f);

        // Show connection to player
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _player.position);

        // Show floating range
        if (_enableFloating)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_targetPosition, new Vector3(0.1f, _floatAmplitude * 2f, 0.1f));
        }
    }
}