using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Simple chase enemy for opening sequence
/// Purpose: Create urgency and drive player toward lantern chamber
/// NOT a complex AI - just relentless pursuit with minimum distance
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ShadowChaseEnemy : MonoBehaviour
{
    [Header("Chase Behavior")]
    [SerializeField] private float _chaseSpeed = 6f;
    [SerializeField] private float _minimumDistance = 3f; // Stays this far behind player
    [SerializeField] private float _maximumDistance = 12f; // Despawns if player gets too far ahead
    
    [Header("Spawn Settings")]
    [SerializeField] private float _spawnDelay = 0.5f; // Delay after trigger before chase starts
    [SerializeField] private Vector3 _spawnOffset = new Vector3(-5f, 0f, 0f); // Spawn behind player
    
    [Header("Stop Conditions")]
    [SerializeField] private bool _stopsAtLanternChamber = true;
    [SerializeField] private string _stopTriggerTag = "LanternChamber";
    [SerializeField] private float _stopDistance = 5f; // How far from stop trigger to halt
    
    [Header("Visual & Audio")]
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private Light2D _enemyGlow;
    [SerializeField] private ParticleSystem _shadowTrail;
    [SerializeField] private AudioClip _spawnSound;
    [SerializeField] private AudioClip _chaseLoopSound;
    [SerializeField] private AudioClip _despawnSound;
    
    [Header("Player Detection")]
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField] private float _playerDetectionRadius = 20f;
    
    [Header("Camera Effects")]
    [SerializeField] private bool _enableCameraShakeOnSpawn = true;
    [SerializeField] private float _spawnShakeIntensity = 0.5f;
    [SerializeField] private float _spawnShakeDuration = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool _debugMode = true;
    [SerializeField] private bool _showDetectionRadius = true;
    [SerializeField] private bool _showMinDistance = true;

    // State
    private Transform _player;
    private Rigidbody2D _rb;
    private bool _isChasing = false;
    private bool _hasStopped = false;
    private GameObject _stopTrigger;
    private AudioSource _audioSource;
    
    // Movement
    private Vector2 _moveDirection;
    private float _currentSpeed;

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // Floats, not affected by gravity
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        
        // Setup audio
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.7f; // Mostly 2D but with some spatial

        if (_debugMode)
            Debug.Log($"🌑 Shadow Chase Enemy created: {name}");
    }

    private void Start()
    {
        // Find player
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (_player == null)
        {
            Debug.LogError("❌ ShadowChaseEnemy: No player found!");
            enabled = false;
            return;
        }

        // Find stop trigger if applicable
        if (_stopsAtLanternChamber)
        {
            _stopTrigger = GameObject.FindGameObjectWithTag(_stopTriggerTag);
            if (_stopTrigger == null && _debugMode)
            {
                Debug.LogWarning($"⚠️ No stop trigger found with tag '{_stopTriggerTag}'");
            }
        }

        // Initialize as inactive
        SetActive(false);
    }

    private void Update()
    {
        if (!_isChasing || _hasStopped) return;

        // Check if should stop at trigger
        if (_stopsAtLanternChamber && _stopTrigger != null)
        {
            float distanceToTrigger = Vector2.Distance(transform.position, _stopTrigger.transform.position);
            if (distanceToTrigger <= _stopDistance)
            {
                StopChase();
                return;
            }
        }

        // Check if player escaped (too far ahead)
        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        if (distanceToPlayer > _maximumDistance)
        {
            if (_debugMode)
                Debug.Log("🏃 Player escaped! Despawning chase enemy.");
            Despawn();
            return;
        }
    }

    private void FixedUpdate()
    {
        if (!_isChasing || _hasStopped) return;

        UpdateChaseMovement();
    }

    #endregion

    #region Chase Logic

    private void UpdateChaseMovement()
    {
        if (_player == null) return;

        // Calculate direction to player
        Vector2 toPlayer = (_player.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        // Only move if beyond minimum distance
        if (distanceToPlayer > _minimumDistance)
        {
            _moveDirection = toPlayer;
            _currentSpeed = _chaseSpeed;
            
            // Move toward player
            _rb.linearVelocity = _moveDirection * _currentSpeed;
            
            // Face player
            if (toPlayer.x < 0)
            {
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
            else
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
        }
        else
        {
            // Maintain distance - slow down
            _rb.linearVelocity = _rb.linearVelocity * 0.8f;
        }

        if (_debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"🌑 Chasing: Distance={distanceToPlayer:F2}, Speed={_currentSpeed:F2}");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Start chasing the player
    /// Called by ChaseSequenceTrigger
    /// </summary>
    public void StartChase()
    {
        if (_isChasing) return;

        StartCoroutine(SpawnSequence());
    }

    /// <summary>
    /// Stop chase and begin despawn
    /// </summary>
    public void StopChase()
    {
        if (_hasStopped) return;

        _hasStopped = true;
        _isChasing = false;
        _rb.linearVelocity = Vector2.zero;

        if (_debugMode)
            Debug.Log("🛑 Chase stopped - reached stop point");

        StartCoroutine(DespawnSequence());
    }

    /// <summary>
    /// Immediately despawn
    /// </summary>
    public void Despawn()
    {
        StartCoroutine(DespawnSequence());
    }

    #endregion

    #region Spawn/Despawn

    private IEnumerator SpawnSequence()
    {
        // Initial delay
        yield return new WaitForSeconds(_spawnDelay);

        // Position behind player
        transform.position = _player.position + _spawnOffset;

        // Visual spawn effect
        SetActive(true);
        
        // Play spawn sound
        if (_spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(_spawnSound, transform.position);
        }

        // Start chase loop sound
        if (_chaseLoopSound != null)
        {
            _audioSource.clip = _chaseLoopSound;
            _audioSource.Play();
        }

        // Camera shake
        if (_enableCameraShakeOnSpawn)
        {
            // If you have a CameraShake system, trigger it here
            // CameraShake.Instance?.Shake(_spawnShakeIntensity, _spawnShakeDuration);
            if (_debugMode)
                Debug.Log("📸 Camera shake triggered!");
        }

        // Start shadow trail
        if (_shadowTrail != null)
        {
            _shadowTrail.Play();
        }

        _isChasing = true;

        if (_debugMode)
            Debug.Log("🌑 CHASE STARTED - Shadow enemy spawned!");
    }

    private IEnumerator DespawnSequence()
    {
        // Fade out audio
        if (_audioSource.isPlaying)
        {
            float fadeTime = 1f;
            float startVolume = _audioSource.volume;
            
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                _audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
                yield return null;
            }
            
            _audioSource.Stop();
        }

        // Play despawn sound
        if (_despawnSound != null)
        {
            AudioSource.PlayClipAtPoint(_despawnSound, transform.position);
        }

        // Visual fade out
        if (_bodyRenderer != null)
        {
            float fadeTime = 1f;
            Color startColor = _bodyRenderer.color;
            
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                Color newColor = startColor;
                newColor.a = Mathf.Lerp(1f, 0f, t / fadeTime);
                _bodyRenderer.color = newColor;
                yield return null;
            }
        }

        // Stop particles
        if (_shadowTrail != null)
        {
            _shadowTrail.Stop();
        }

        if (_debugMode)
            Debug.Log("👻 Shadow enemy despawned");

        // Destroy or deactivate
        Destroy(gameObject);
    }

    private void SetActive(bool active)
    {
        if (_bodyRenderer != null)
            _bodyRenderer.enabled = active;
        
        if (_enemyGlow != null)
            _enemyGlow.enabled = active;
        
        if (_shadowTrail != null)
        {
            if (active)
                _shadowTrail.Play();
            else
                _shadowTrail.Stop();
        }
    }

    #endregion

    #region Collision

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // If enemy touches player, could trigger game over or damage
        // For now, this chase enemy just pursues - doesn't actually harm
        if (collision.CompareTag("Player") && _debugMode)
        {
            Debug.Log("⚠️ Shadow enemy touched player! (No damage in current implementation)");
        }

        // Check for stop trigger
        if (_stopsAtLanternChamber && collision.gameObject.CompareTag(_stopTriggerTag))
        {
            StopChase();
        }
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!_showDetectionRadius && !_showMinDistance) return;

        // Detection radius
        if (_showDetectionRadius)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _playerDetectionRadius);
        }

        // Minimum chase distance
        if (_showMinDistance)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _minimumDistance);
        }

        // Maximum distance before despawn
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _maximumDistance);

        // Draw line to player if chasing
        if (Application.isPlaying && _isChasing && _player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _player.position);
        }

        // Draw line to stop trigger
        if (_stopTrigger != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _stopTrigger.transform.position);
            Gizmos.DrawWireSphere(_stopTrigger.transform.position, _stopDistance);
        }
    }

    #endregion
}
