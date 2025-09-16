using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Light-sensitive enemy that demonstrates "combat as support" philosophy
/// Primary Purpose: Create tactical positioning challenges for puzzle navigation
/// Secondary Purpose: Provide Solar Flare utility demonstration
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class LurkerEnemy : MonoBehaviour, ILightInteractable
{
    [Header("AI Behavior")]
    [SerializeField] private LurkerState _defaultState = LurkerState.Patrol;
    [SerializeField] private float _patrolSpeed = 2f;
    [SerializeField] private float _fleeSpeed = 4f;
    [SerializeField] private float _detectionRange = 6f;
    [SerializeField] private float _personalSpaceRadius = 3f; // Minimum distance from player

    [Header("Light Sensitivity")]
    [SerializeField] private float _lightSensitivity = 0.3f;
    [SerializeField] private float _stunResistance = 0.5f; // How much stun intensity needed
    [SerializeField] private float _maxStunDuration = 5f;
    [SerializeField] private bool _dissolvesInIntenseLight = false;
    [SerializeField] private float _dissolveThreshold = 2f;

    [Header("Patrol Behavior")]
    [SerializeField] private List<Transform> _patrolPoints = new List<Transform>();
    [SerializeField] private bool _autoCreatePatrolPoints = true;
    [SerializeField] private float _patrolPointSpacing = 4f;
    [SerializeField] private float _waitAtPatrolPoint = 2f;
    [SerializeField] private bool _reversePatrolOnEnd = true;

    [Header("Cover Seeking")]
    [SerializeField] private LayerMask _coverLayers = 1 << 8;
    [SerializeField] private float _coverSeekDistance = 8f;
    [SerializeField] private bool _prefersDarkness = true;
    [SerializeField] private float _shadowCheckRadius = 1f;

    [Header("Visual Components")]
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private Light2D _enemyLight;
    [SerializeField] private ParticleSystem _fearParticles;
    [SerializeField] private ParticleSystem _stunParticles;
    [SerializeField] private ParticleSystem _dissolveParticles;

    [Header("Audio")]
    [SerializeField] private AudioClip _detectionSound;
    [SerializeField] private AudioClip _fleeSound;
    [SerializeField] private AudioClip _stunSound;
    [SerializeField] private AudioClip _dissolveSound;

    [Header("Gameplay Integration")]
    [SerializeField] private bool _blocksPuzzleInteraction = true;
    [SerializeField] private bool _dropsLightEssence = true;
    [SerializeField] private int _essenceDropAmount = 3;
    [SerializeField] private GameObject _essenceDropPrefab;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _showPatrolPath = true;
    [SerializeField] private bool _showDetectionRange = true;

    public enum LurkerState
    {
        Patrol,        // Normal patrol behavior
        Investigating, // Checking out player presence
        Fleeing,       // Running away from light
        Hiding,        // Seeking cover in shadows
        Stunned,       // Temporarily disabled by Solar Flare
        Dissolving,    // Being destroyed by intense light
        Dormant        // Inactive/sleeping
    }

    // State tracking
    public LurkerState CurrentState { get; private set; }
    public bool IsCurrentlyIlluminated { get; private set; }
    public LightEffect CurrentActiveEffect { get; private set; }
    public float CurrentLightIntensity { get; private set; }
    public bool IsStunned => CurrentState == LurkerState.Stunned;
    public bool IsActiveEnemy => CurrentState != LurkerState.Stunned && CurrentState != LurkerState.Dissolving && CurrentState != LurkerState.Dormant;

    // Navigation
    private Transform _player;
    private Vector3 _originalPosition;
    private int _currentPatrolIndex = 0;
    private bool _patrolReversed = false;
    private Vector3 _fleeDirection;
    private Vector3 _lastKnownPlayerPosition;

    // Timing and state
    private float _stateTimer = 0f;
    private float _stunTimeRemaining = 0f;
    private float _patrolWaitTimer = 0f;
    private Coroutine _behaviorCoroutine;

    // Components
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private AudioSource _audioSource;

    // Visual state colors
    private Color _normalColor = new Color(0.3f, 0.3f, 0.7f);
    private Color _alertColor = new Color(0.7f, 0.5f, 0.3f);
    private Color _fleeColor = new Color(0.7f, 0.3f, 0.3f);
    private Color _stunColor = new Color(0.9f, 0.9f, 0.3f);
    private Color _dissolveColor = new Color(1f, 1f, 1f, 0.5f);

    // Events for gameplay integration
    public static System.Action<LurkerEnemy> OnLurkerStunned;
    public static System.Action<LurkerEnemy> OnLurkerDissolved;
    public static System.Action<LurkerEnemy, Vector3> OnLurkerDetectedPlayer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();

        _originalPosition = transform.position;

        SetupComponents();

        if (_autoCreatePatrolPoints && _patrolPoints.Count == 0)
        {
            CreateDefaultPatrolPoints();
        }
    }

    private void Start()
    {
        // Find player
        _player = FindObjectOfType<PlayerMovement>()?.transform;
        if (_player == null)
        {
            var lanternController = FindObjectOfType<EnhancedLanternController>();
            if (lanternController != null)
                _player = lanternController.transform;
        }

        if (_player == null)
        {
            Debug.LogWarning($"Lurker {name} could not find player!");
        }

        SetState(_defaultState);

        if (_debugMode)
        {
            Debug.Log($"✓ Lurker {name} initialized");
            Debug.Log($"  Default State: {_defaultState}");
            Debug.Log($"  Patrol Points: {_patrolPoints.Count}");
            Debug.Log($"  Player Found: {_player != null}");
        }
    }

    private void SetupComponents()
    {
        // Setup physics
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 1f;
        _rb.freezeRotation = true;

        // Setup collider as trigger for light detection
        _collider.isTrigger = false; // Solid for player collision

        // Add trigger collider for light detection
        var lightDetector = new GameObject("LightDetector");
        lightDetector.transform.SetParent(transform);
        lightDetector.transform.localPosition = Vector3.zero;
        var lightCollider = lightDetector.AddComponent<CircleCollider2D>();
        lightCollider.isTrigger = true;
        lightCollider.radius = 1.5f; // Detection area for light

        // Setup visual components
        if (_bodyRenderer == null)
        {
            _bodyRenderer = GetComponent<SpriteRenderer>();
            if (_bodyRenderer == null)
            {
                _bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
                _bodyRenderer.sprite = CreateLurkerSprite();
            }
        }

        if (_enemyLight == null)
        {
            GameObject lightObj = new GameObject("EnemyEyeLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            _enemyLight = lightObj.AddComponent<Light2D>();
        }

        _enemyLight.lightType = Light2D.LightType.Point;
        _enemyLight.intensity = 0.3f;
        _enemyLight.pointLightInnerRadius = 0.1f;
        _enemyLight.pointLightOuterRadius = 1f;
        _enemyLight.color = Color.red;

        // Setup particles
        if (_fearParticles == null)
        {
            _fearParticles = CreateParticleSystem("FearParticles", _fleeColor);
        }

        if (_stunParticles == null)
        {
            _stunParticles = CreateParticleSystem("StunParticles", _stunColor);
        }
    }

    private Sprite CreateLurkerSprite()
    {
        // Create a simple lurker sprite (shadow-like creature)
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float bodyRadius = size * 0.3f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance <= bodyRadius)
                {
                    // Create shadowy body
                    float alpha = 1f - (distance / bodyRadius) * 0.3f;
                    pixels[y * size + x] = new Color(0.2f, 0.2f, 0.4f, alpha);
                }

                // Add glowing eyes
                Vector2 leftEye = center + new Vector2(-size * 0.1f, size * 0.1f);
                Vector2 rightEye = center + new Vector2(size * 0.1f, size * 0.1f);

                if (Vector2.Distance(pos, leftEye) <= 3f || Vector2.Distance(pos, rightEye) <= 3f)
                {
                    pixels[y * size + x] = Color.red;
                }

                if (distance > bodyRadius)
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private ParticleSystem CreateParticleSystem(string name, Color color)
    {
        GameObject particleObj = new GameObject(name);
        particleObj.transform.SetParent(transform);
        particleObj.transform.localPosition = Vector3.zero;

        var particles = particleObj.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startLifetime = 1f;
        main.startSpeed = 2f;
        main.startSize = 0.1f;
        main.startColor = color;
        main.maxParticles = 20;

        var emission = particles.emission;
        emission.rateOverTime = 10f;

        particles.Stop();
        return particles;
    }

    private void CreateDefaultPatrolPoints()
    {
        // Create simple left-right patrol
        var leftPoint = new GameObject($"{name}_PatrolLeft");
        leftPoint.transform.position = _originalPosition + Vector3.left * _patrolPointSpacing;
        _patrolPoints.Add(leftPoint.transform);

        var rightPoint = new GameObject($"{name}_PatrolRight");
        rightPoint.transform.position = _originalPosition + Vector3.right * _patrolPointSpacing;
        _patrolPoints.Add(rightPoint.transform);

        if (_debugMode)
        {
            Debug.Log($"Created default patrol points for {name}");
        }
    }

    private void Update()
    {
        UpdateState();
        UpdateVisuals();
        UpdateAI();
    }

    private void UpdateState()
    {
        _stateTimer += Time.deltaTime;

        // Handle stun duration
        if (IsStunned)
        {
            _stunTimeRemaining -= Time.deltaTime;
            if (_stunTimeRemaining <= 0f)
            {
                RecoverFromStun();
            }
        }

        // Handle patrol wait timer
        if (CurrentState == LurkerState.Patrol && _patrolWaitTimer > 0f)
        {
            _patrolWaitTimer -= Time.deltaTime;
        }
    }

    private void UpdateVisuals()
    {
        Color targetColor = CurrentState switch
        {
            LurkerState.Patrol => _normalColor,
            LurkerState.Investigating => _alertColor,
            LurkerState.Fleeing => _fleeColor,
            LurkerState.Hiding => _normalColor * 0.7f,
            LurkerState.Stunned => _stunColor,
            LurkerState.Dissolving => _dissolveColor,
            LurkerState.Dormant => _normalColor * 0.5f,
            _ => _normalColor
        };

        if (_bodyRenderer != null)
        {
            _bodyRenderer.color = Color.Lerp(_bodyRenderer.color, targetColor, Time.deltaTime * 3f);
        }

        // Update enemy light
        if (_enemyLight != null)
        {
            _enemyLight.intensity = IsActiveEnemy ? 0.3f : 0.1f;
            _enemyLight.color = IsStunned ? Color.yellow : Color.red;
        }

        // Update particles
        UpdateParticleEffects();
    }

    private void UpdateParticleEffects()
    {
        // Fear particles when fleeing
        if (_fearParticles != null)
        {
            if (CurrentState == LurkerState.Fleeing && !_fearParticles.isPlaying)
            {
                _fearParticles.Play();
            }
            else if (CurrentState != LurkerState.Fleeing && _fearParticles.isPlaying)
            {
                _fearParticles.Stop();
            }
        }

        // Stun particles when stunned
        if (_stunParticles != null)
        {
            if (IsStunned && !_stunParticles.isPlaying)
            {
                _stunParticles.Play();
            }
            else if (!IsStunned && _stunParticles.isPlaying)
            {
                _stunParticles.Stop();
            }
        }
    }

    #region AI Behavior

    private void UpdateAI()
    {
        if (!IsActiveEnemy) return;

        switch (CurrentState)
        {
            case LurkerState.Patrol:
                HandlePatrolBehavior();
                break;

            case LurkerState.Investigating:
                HandleInvestigatingBehavior();
                break;

            case LurkerState.Fleeing:
                HandleFleeingBehavior();
                break;

            case LurkerState.Hiding:
                HandleHidingBehavior();
                break;
        }

        // Always check for player proximity and light conditions
        CheckPlayerProximity();
        CheckLightConditions();
    }

    private void HandlePatrolBehavior()
    {
        if (_patrolPoints.Count == 0 || _patrolWaitTimer > 0f) return;

        var targetPoint = _patrolPoints[_currentPatrolIndex];
        if (targetPoint == null) return;

        Vector3 direction = (targetPoint.position - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x * _patrolSpeed, _rb.linearVelocity.y);

        // Check if reached patrol point
        if (Vector3.Distance(transform.position, targetPoint.position) < 0.5f)
        {
            _patrolWaitTimer = _waitAtPatrolPoint;
            MoveToNextPatrolPoint();
        }
    }

    private void MoveToNextPatrolPoint()
    {
        if (_reversePatrolOnEnd)
        {
            if (!_patrolReversed)
            {
                _currentPatrolIndex++;
                if (_currentPatrolIndex >= _patrolPoints.Count)
                {
                    _currentPatrolIndex = _patrolPoints.Count - 2;
                    _patrolReversed = true;
                }
            }
            else
            {
                _currentPatrolIndex--;
                if (_currentPatrolIndex < 0)
                {
                    _currentPatrolIndex = 1;
                    _patrolReversed = false;
                }
            }
        }
        else
        {
            _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Count;
        }
    }

    private void HandleInvestigatingBehavior()
    {
        if (_player == null) return;

        // Move toward last known player position
        Vector3 direction = (_lastKnownPlayerPosition - transform.position).normalized;
        _rb.linearVelocity = new Vector2(direction.x * _patrolSpeed * 0.7f, _rb.linearVelocity.y);

        // Return to patrol after investigation time
        if (_stateTimer > 3f)
        {
            SetState(LurkerState.Patrol);
        }
    }

    private void HandleFleeingBehavior()
    {
        // Move away from light source or player
        _rb.linearVelocity = new Vector2(_fleeDirection.x * _fleeSpeed, _rb.linearVelocity.y);

        // Look for cover
        Vector3 coverPosition = FindNearestCover();
        if (coverPosition != Vector3.zero)
        {
            Vector3 toCover = (coverPosition - transform.position).normalized;
            _rb.linearVelocity = new Vector2(toCover.x * _fleeSpeed, _rb.linearVelocity.y);

            // Switch to hiding if close to cover
            if (Vector3.Distance(transform.position, coverPosition) < 1f)
            {
                SetState(LurkerState.Hiding);
            }
        }

        // Return to patrol if fled long enough and no longer illuminated
        if (_stateTimer > 2f && !IsCurrentlyIlluminated)
        {
            SetState(LurkerState.Patrol);
        }
    }

    private void HandleHidingBehavior()
    {
        // Stay still while hiding
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        // Return to patrol if safe for a while
        if (_stateTimer > 3f && !IsCurrentlyIlluminated)
        {
            SetState(LurkerState.Patrol);
        }
    }

    private void CheckPlayerProximity()
    {
        if (_player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        // Detect player if too close
        if (distanceToPlayer < _detectionRange && CurrentState == LurkerState.Patrol)
        {
            _lastKnownPlayerPosition = _player.position;
            SetState(LurkerState.Investigating);

            OnLurkerDetectedPlayer?.Invoke(this, _player.position);

            if (_detectionSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_detectionSound);
            }
        }

        // Flee if player gets too close
        if (distanceToPlayer < _personalSpaceRadius && IsActiveEnemy)
        {
            _fleeDirection = (transform.position - _player.position).normalized;
            SetState(LurkerState.Fleeing);
        }
    }

    private void CheckLightConditions()
    {
        // If illuminated and not already fleeing/stunned, start fleeing
        if (IsCurrentlyIlluminated && CurrentLightIntensity > _lightSensitivity && CurrentState != LurkerState.Fleeing && !IsStunned)
        {
            var lightSource = FindObjectOfType<FloatingLantern>();
            if (lightSource != null)
            {
                _fleeDirection = (transform.position - lightSource.transform.position).normalized;
            }
            else
            {
                _fleeDirection = Vector3.right * (Random.value > 0.5f ? 1f : -1f);
            }

            SetState(LurkerState.Fleeing);

            if (_fleeSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_fleeSound);
            }
        }
    }

    private Vector3 FindNearestCover()
    {
        Collider2D[] coverObjects = Physics2D.OverlapCircleAll(transform.position, _coverSeekDistance, _coverLayers);

        Vector3 nearestCover = Vector3.zero;
        float nearestDistance = float.MaxValue;

        foreach (var coverObject in coverObjects)
        {
            float distance = Vector3.Distance(transform.position, coverObject.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestCover = coverObject.transform.position;
            }
        }

        return nearestCover;
    }

    private void SetState(LurkerState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        _stateTimer = 0f;

        if (_debugMode)
        {
            Debug.Log($"🔄 Lurker {name} state: {newState}");
        }
    }

    #endregion

    #region ILightInteractable Implementation

    public void OnLightEnter(LightEffect effect, float intensity, Vector2 direction)
    {
        IsCurrentlyIlluminated = true;
        CurrentActiveEffect = effect;
        CurrentLightIntensity = intensity;

        if (_debugMode)
        {
            Debug.Log($"💡 Lurker {name} illuminated: {effect} (intensity: {intensity:F2})");
        }

        // Handle different light effects
        switch (effect)
        {
            case LightEffect.Stun:
                // Solar Flare stunning
                if (intensity >= _stunResistance)
                {
                    ApplyStun(intensity);
                }
                break;

            case LightEffect.Reveal:
            case LightEffect.Energize:
                // Basic light causes fleeing behavior
                if (intensity > _lightSensitivity && IsActiveEnemy)
                {
                    _fleeDirection = -direction;
                    SetState(LurkerState.Fleeing);
                }
                break;

            case LightEffect.Purify:
                // Intense purifying light can dissolve lurkers
                if (_dissolvesInIntenseLight && intensity >= _dissolveThreshold)
                {
                    StartDissolving();
                }
                break;
        }
    }

    public void OnLightStay(LightEffect effect, float intensity, Vector2 direction, float deltaTime)
    {
        CurrentLightIntensity = intensity;

        // Continuous dissolving check
        if (effect == LightEffect.Purify && _dissolvesInIntenseLight && intensity >= _dissolveThreshold)
        {
            if (CurrentState != LurkerState.Dissolving)
            {
                StartDissolving();
            }
        }
    }

    public void OnLightExit(LightEffect effect)
    {
        IsCurrentlyIlluminated = false;
        CurrentActiveEffect = LightEffect.Reveal;
        CurrentLightIntensity = 0f;

        if (_debugMode)
        {
            Debug.Log($"🌑 Lurker {name} left light");
        }
    }

    public bool RespondsToEffect(LightEffect effect)
    {
        // Lurkers respond to most light effects
        return effect == LightEffect.Reveal ||
               effect == LightEffect.Energize ||
               effect == LightEffect.Stun ||
               effect == LightEffect.Purify;
    }

    public float GetMinimumIntensity(LightEffect effect)
    {
        return effect == LightEffect.Stun ? _stunResistance : _lightSensitivity;
    }

    #endregion

    #region Stun and Dissolve Effects

    private void ApplyStun(float intensity)
    {
        if (IsStunned) return;

        SetState(LurkerState.Stunned);
        _stunTimeRemaining = Mathf.Min(_maxStunDuration, intensity * 2f);

        // Stop movement
        _rb.linearVelocity = Vector2.zero;

        // Play stun sound
        if (_stunSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_stunSound);
        }

        OnLurkerStunned?.Invoke(this);

        if (_debugMode)
        {
            Debug.Log($"⚡ Lurker {name} stunned for {_stunTimeRemaining:F1}s");
        }
    }

    private void RecoverFromStun()
    {
        if (!IsStunned) return;

        _stunTimeRemaining = 0f;

        // Return to appropriate state
        if (IsCurrentlyIlluminated)
        {
            SetState(LurkerState.Fleeing);
        }
        else
        {
            SetState(LurkerState.Patrol);
        }

        if (_debugMode)
        {
            Debug.Log($"🔄 Lurker {name} recovered from stun");
        }
    }

    private void StartDissolving()
    {
        if (CurrentState == LurkerState.Dissolving) return;

        SetState(LurkerState.Dissolving);

        // Play dissolve sound
        if (_dissolveSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_dissolveSound);
        }

        // Start dissolve effect
        StartCoroutine(DissolveSequence());

        OnLurkerDissolved?.Invoke(this);

        if (_debugMode)
        {
            Debug.Log($"💀 Lurker {name} dissolving");
        }
    }

    private IEnumerator DissolveSequence()
    {
        // Play dissolve particles
        if (_dissolveParticles != null)
        {
            _dissolveParticles.Play();
        }

        // Fade out over time
        float dissolveTime = 2f;
        float elapsed = 0f;
        Color startColor = _bodyRenderer.color;

        while (elapsed < dissolveTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / dissolveTime);

            if (_bodyRenderer != null)
            {
                Color color = startColor;
                color.a = alpha;
                _bodyRenderer.color = color;
            }

            yield return null;
        }

        // Drop essence before destroying
        if (_dropsLightEssence)
        {
            DropLightEssence();
        }

        // Destroy the lurker
        Destroy(gameObject);
    }

    private void DropLightEssence()
    {
        if (_essenceDropPrefab != null)
        {
            Instantiate(_essenceDropPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // Find progression system and award essence directly
            var progressionSystem = FindObjectOfType<DualProgressionSystem>();
            if (progressionSystem != null)
            {
                progressionSystem.AddLightEssence(_essenceDropAmount);
            }
        }

        if (_debugMode)
        {
            Debug.Log($"💎 Lurker {name} dropped {_essenceDropAmount} light essence");
        }
    }

    #endregion

    #region Public API

    public bool IsActiveAndCanBlock() => IsActiveEnemy && _blocksPuzzleInteraction;
    public Vector3 GetOriginalPosition() => _originalPosition;
    public List<Transform> GetPatrolPoints() => new List<Transform>(_patrolPoints);

    public void ForceStun(float duration)
    {
        ApplyStun(duration / 2f); // Convert duration to intensity equivalent
    }

    public void ForceDissolve()
    {
        StartDissolving();
    }

    public void ResetToOriginalPosition()
    {
        transform.position = _originalPosition;
        SetState(_defaultState);
        _stunTimeRemaining = 0f;
        IsCurrentlyIlluminated = false;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Stun Effect")]
    public void TestStunEffect()
    {
        if (Application.isPlaying)
        {
            OnLightEnter(LightEffect.Stun, 1f, Vector2.zero);
        }
    }

    [ContextMenu("Test Dissolve Effect")]
    public void TestDissolveEffect()
    {
        if (Application.isPlaying)
        {
            OnLightEnter(LightEffect.Purify, 3f, Vector2.zero);
        }
    }

    [ContextMenu("Show Lurker Info")]
    public void ShowLurkerInfo()
    {
        Debug.Log($"=== LURKER INFO: {name} ===");
        Debug.Log($"Current State: {CurrentState}");
        Debug.Log($"Is Illuminated: {IsCurrentlyIlluminated}");
        Debug.Log($"Light Intensity: {CurrentLightIntensity:F2}");
        Debug.Log($"Is Stunned: {IsStunned}");
        Debug.Log($"Stun Time Remaining: {_stunTimeRemaining:F1}s");
        Debug.Log($"Patrol Points: {_patrolPoints.Count}");
        Debug.Log($"Current Patrol Index: {_currentPatrolIndex}");
        Debug.Log($"Player Distance: {(_player != null ? Vector3.Distance(transform.position, _player.position).ToString("F1") : "No Player")}");
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        // Draw personal space
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _personalSpaceRadius);

        // Draw cover seek distance
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _coverSeekDistance);

        // Draw patrol path
        if (_showPatrolPath && _patrolPoints.Count > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < _patrolPoints.Count - 1; i++)
            {
                if (_patrolPoints[i] != null && _patrolPoints[i + 1] != null)
                {
                    Gizmos.DrawLine(_patrolPoints[i].position, _patrolPoints[i + 1].position);
                }
            }

            // Draw current target
            if (_currentPatrolIndex < _patrolPoints.Count && _patrolPoints[_currentPatrolIndex] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _patrolPoints[_currentPatrolIndex].position);
            }
        }

        // Show current state
        if (Application.isPlaying)
        {
            Gizmos.color = CurrentState switch
            {
                LurkerState.Patrol => Color.green,
                LurkerState.Investigating => Color.yellow,
                LurkerState.Fleeing => Color.red,
                LurkerState.Hiding => Color.blue,
                LurkerState.Stunned => Color.magenta,
                LurkerState.Dissolving => Color.white,
                _ => Color.gray
            };
            Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 0.5f);
        }
    }
}