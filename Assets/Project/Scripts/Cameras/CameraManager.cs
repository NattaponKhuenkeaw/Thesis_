using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Unity.Behavior;
using Project.Core.ZoneState;

/// <summary>
/// Central camera orchestrator. Manages Explore / Combat virtual camera switching
/// via Cinemachine vcam priorities, maintains a CinemachineTargetGroup for combat
/// framing, and provides screen shake via CinemachineImpulseSource.
///
/// Scene Setup:
///   CameraSetUp                          [CameraManager]
///   ├── MainCamera                       [Camera, CinemachineBrain, PixelPerfectCamera]
///   ├── CinemachineCamera_Explore        [CinemachineCamera, PositionComposer, Confiner2D, ImpulseListener]
///   ├── CinemachineCamera_Combat         [CinemachineCamera, GroupFraming, Confiner2D, ImpulseListener]
///   ├── CombatTargetGroup                [CinemachineTargetGroup]
///   ├── ConfinderBound                   [BoxCollider2D]
///   └── CameraLookAheadTarget            [CameraLookAhead]
/// </summary>
public class CameraManager : MonoBehaviour
{
    // =========================================================
    // Singleton
    // =========================================================
    public static CameraManager instance;

    // =========================================================
    // Camera References
    // =========================================================
    [Header("Virtual Cameras")]
    [Tooltip("The exploration / follow-player virtual camera.")]
    [SerializeField] private CinemachineCamera _exploreCam;

    [Tooltip("The combat framing virtual camera (uses TargetGroup).")]
    [SerializeField] private CinemachineCamera _combatCam;

    [Tooltip("The static virtual camera (no follow / fixed transform).")]
    [SerializeField] private CinemachineCamera _staticCam;

    [Tooltip("The ultimate virtual camera (focuses on player during ultimate).")]
    [SerializeField] private CinemachineCamera _ultimateCam;

    [Header("Zone State")]
    [Tooltip("The ZoneStateProvider that manages room state changes.")]
    [SerializeField] private ZoneStateProvider _zoneStateProvider;

    [Header("Target Group")]
    [Tooltip("The CinemachineTargetGroup that the combat cam tracks.")]
    [SerializeField] private CinemachineTargetGroup _combatTargetGroup;

    [Header("Player Reference")]
    [Tooltip("Player transform. Auto-found by tag if left blank.")]
    [SerializeField] private Transform _playerTransform;

    // =========================================================
    // Camera Priorities
    // =========================================================
    [Header("Priority Settings")]
    [Tooltip("Priority assigned to the active (foreground) camera.")]
    [SerializeField] private int _activePriority = 20;

    [Tooltip("Priority assigned to the inactive (background) camera.")]
    [SerializeField] private int _inactivePriority = 0;

    // =========================================================
    // Combat TargetGroup Weights & Transitions
    // =========================================================
    [Header("Combat Transition Settings")]
    [Tooltip("How fast the camera weight transitions when entering/leaving combat.")]
    [SerializeField] private float _weightLerpSpeed = 2.0f;

    [Header("Combat Range Bands")]
    [Tooltip("Extra distance added after DetectRadius before an enemy leaves the combat target group.")]
    [SerializeField] private float _secondRangeExtension = 3f;

    [Tooltip("Minimum weight factor while the enemy is still inside DetectRadius.")]
    [SerializeField] [Range(0f, 1f)] private float _firstRangeMinWeightFactor = 0.5f;

    [Tooltip("Minimum weight factor while the enemy is inside the second range band.")]
    [SerializeField] [Range(0f, 1f)] private float _secondRangeMinWeightFactor = 0.1f;

    [Header("Combat TargetGroup Settings")]
    [Tooltip("Weight of the player in the combat TargetGroup.")]
    [SerializeField] private float _playerWeight = 1.0f;

    [Tooltip("Default weight of an enemy in the combat TargetGroup.")]
    [SerializeField] private float _defaultEnemyWeight = 0.8f;

    [Tooltip("Radius of the player target for framing.")]
    [SerializeField] private float _playerRadius = 1f;

    [Tooltip("Radius of each enemy target for framing.")]
    [SerializeField] private float _enemyRadius = 1f;

    [Header("Boss Combat Framing")]
    [Tooltip("Ortho size range while an elite/boss is in the combat target group.")]
    [SerializeField] private Vector2 _bossOrthoSizeRange = new Vector2(9.5f, 12f);

    [Tooltip("Group framing size while fighting a boss (lower = more arena visible).")]
    [SerializeField] [Range(0.15f, 0.9f)] private float _bossFramingSize = 0.32f;

    [Tooltip("Target-group radius used for elite/boss bodies.")]
    [SerializeField] private float _bossEnemyRadius = 2.75f;

    // =========================================================
    // Y Damping Settings (kept from original)
    // =========================================================
    [Header("Y Damping Settings")]
    [SerializeField] private float _fallPanAmount = 0.25f;
    [SerializeField] private float _normYPanAmount = 2f;
    [SerializeField] private float _fallYPanTime = 0.35f;

    // =========================================================
    // Screen Shake (Impulse)
    // =========================================================
    [Header("Screen Shake — Impulse")]
    [Tooltip("CinemachineImpulseSource used for screen shake. Auto-found if null.")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [Header("Shake Intensity by Impact Tier")]
    [SerializeField] private float _lowShake = 0.15f;
    [SerializeField] private float _mediumShake = 0.4f;
    [SerializeField] private float _heavyShake = 0.65f;
    [SerializeField] private float _ultimateShake = 1.0f;

    // =========================================================
    // Ultimate Camera Settings
    // =========================================================
    [Header("Ultimate Zoom Transition")]
    [Tooltip("Duration (seconds) for the blend into/out of the ultimate camera.")]
    [SerializeField] private float _ultimateTransitionDuration = 0.35f;

    // =========================================================
    // Runtime State
    // =========================================================
    private class EngagedEnemyContext
    {
        public float targetMaxWeight;
        public float currentWeight;
        public bool isLeaving;
    }

    private bool _isInCombat;
    private readonly Dictionary<EnemyController, EngagedEnemyContext> _engagedEnemies = new Dictionary<EnemyController, EngagedEnemyContext>();

    // Y-damping
    private CinemachinePositionComposer _explorePositionComposer;
    private Coroutine _lerpYPanCoroutine;
    private Coroutine _temporaryPlayerSnapCoroutine;
    private CinemachineBrain _brain;
    private CinemachineBlendDefinition _cachedBrainBlend;
    private Vector3 _cachedExploreDamping;
    private bool _isTemporarilySnappedToPlayer;
    private bool _isTemporarilyFocused;
    private Transform _cachedFocusExploreFollow;
    private Transform _cachedFocusExploreLookAt;
    private Transform _cachedFocusStaticFollow;
    private Transform _cachedFocusStaticLookAt;
    private float _originalExploreSize = -1f;
    private float _originalCombatSize = -1f;
    private float _originalStaticSize = -1f;
    private float _originalUltimateSize = -1f;
    private bool _hasCachedBrainBlend = false;
    private Coroutine _ultimateZoomCoroutine;

    private CinemachineGroupFraming _combatGroupFraming;
    private Vector2 _defaultCombatOrthoSizeRange = new Vector2(6.8f, 8f);
    private float _defaultCombatFramingSize = 0.47f;
    private bool _bossFramingActive;

    public bool IsInCombat => _isInCombat;
    public bool IsLerpingYDamping { get; private set; }
    public bool LerpedFromPlayerFalling { get; set; }

    /// <summary>
    /// Returns the collection of enemies currently being tracked by the camera group.
    /// Used for targeting and snapping logic.
    /// </summary>
    public IEnumerable<EnemyController> EngagedEnemies => _engagedEnemies.Keys;

    // =========================================================
    // Unity Methods
    // =========================================================
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
#if UNITY_EDITOR
            // Guard against MissingReferenceException in Inspector
            if (UnityEditor.Selection.activeGameObject != null && 
                (UnityEditor.Selection.activeGameObject == gameObject || UnityEditor.Selection.activeGameObject.transform.IsChildOf(transform)))
            {
                UnityEditor.Selection.activeGameObject = null;
            }
#endif
            Destroy(gameObject);
            return;
        }

        // Auto-find player
        if (_playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                _playerTransform = playerObj.transform;
        }

        // Auto-find impulse source
        if (_impulseSource == null)
            _impulseSource = GetComponentInChildren<CinemachineImpulseSource>();

        if (_brain == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _brain = mainCamera.GetComponent<CinemachineBrain>();
            }
        }

        // Auto-find static camera
        if (_staticCam == null)
        {
            Transform staticCamTransform = transform.Find("CinemachineCamera_Static");
            if (staticCamTransform != null)
            {
                _staticCam = staticCamTransform.GetComponent<CinemachineCamera>();
            }
            else
            {
                CinemachineCamera[] cams = GetComponentsInChildren<CinemachineCamera>(true);
                foreach (var cam in cams)
                {
                    if (cam.gameObject.name.Contains("Static"))
                    {
                        _staticCam = cam;
                        break;
                    }
                }
            }
        }

        // Auto-find ultimate camera
        if (_ultimateCam == null)
        {
            Transform ultimateCamTransform = transform.Find("CinemachineCamera_Ultimate");
            if (ultimateCamTransform != null)
            {
                _ultimateCam = ultimateCamTransform.GetComponent<CinemachineCamera>();
            }
            else
            {
                CinemachineCamera[] cams = GetComponentsInChildren<CinemachineCamera>(true);
                foreach (var cam in cams)
                {
                    if (cam.gameObject.name.Contains("Ultimate"))
                    {
                        _ultimateCam = cam;
                        break;
                    }
                }
            }
        }

        // Auto-load zone state provider
        if (_zoneStateProvider == null)
        {
            _zoneStateProvider = Resources.Load<ZoneStateProvider>("CombatSO/ZoneStateProvider");
        }

        // Cache explore cam's PositionComposer for Y-damping
        if (_exploreCam != null)
            _explorePositionComposer = _exploreCam.GetComponent<CinemachinePositionComposer>();

        CacheCombatGroupFraming();

        // Start in explore mode
        SetExploreMode();
    }

    private void CacheCombatGroupFraming()
    {
        if (_combatCam == null)
            return;

        _combatGroupFraming = _combatCam.GetComponent<CinemachineGroupFraming>();
        if (_combatGroupFraming == null)
            return;

        _defaultCombatOrthoSizeRange = _combatGroupFraming.OrthoSizeRange;
        _defaultCombatFramingSize = _combatGroupFraming.FramingSize;
    }

    private void Start()
    {
        if (_zoneStateProvider != null)
        {
            _zoneStateProvider.OnRoomChanged += HandleRoomChanged;
            UpdateCameraForRoom(_zoneStateProvider.CurrentRoom);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (_zoneStateProvider != null)
        {
            _zoneStateProvider.OnRoomChanged -= HandleRoomChanged;
        }
    }

    private void LateUpdate()
    {
        if (!_isInCombat || _combatTargetGroup == null || _playerTransform == null) 
            return;

        List<EnemyController> toRemove = null;

        // Process dynamic weights and range checks for each engaged enemy
        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;

            // Handle destroyed or dead enemies
            bool isDestroyed = enemy == null;
            if (isDestroyed || !enemy.IsAlive)
            {
                ctx.isLeaving = true;
            }
            else if (ShouldKeepEnemyInCombatGroup(enemy))
            {
                ctx.isLeaving = false;
            }

            float desiredWeight = 0f;

            // Only calculate range-based weights if the enemy still exists
            if (!ctx.isLeaving && !isDestroyed)
            {
                desiredWeight = EvaluateDesiredEnemyWeight(enemy, ctx, out bool shouldLeaveByRange);
                if (shouldLeaveByRange)
                {
                    ctx.isLeaving = true;
                }
            }

            // Smoothly move the current weight towards the desired weight (0 if leaving)
            ctx.currentWeight = Mathf.MoveTowards(ctx.currentWeight, desiredWeight, _weightLerpSpeed * Time.deltaTime);

            // Apply to TargetGroup with null-safety
            if (!isDestroyed)
            {
                UpdateTargetWeight(enemy.transform, ctx.currentWeight);
            }
            else
            {
                // If the key is null, we can't update it, but we'll remove it below
                ctx.currentWeight = 0f; 
            }

            // Removal criteria: if destroyed OR completely faded out while leaving
            if (isDestroyed || (ctx.isLeaving && ctx.currentWeight <= 0.01f))
            {
                toRemove ??= new List<EnemyController>();
                toRemove.Add(enemy);
            }
        }

        // Clean up left enemies
        if (toRemove != null)
        {
            foreach (var enemy in toRemove)
            {
                if (enemy != null)
                {
                    RemoveFromTargetGroup(enemy.transform);
                }
                else
                {
                    // Full sweep for null transforms if the enemy object itself was lost
                    CleanupNullTargets();
                }

                _engagedEnemies.Remove(enemy);
            }

            RefreshBossCombatFraming();
        }

        // Return to explore mode if no engaged enemies remain
        if (_engagedEnemies.Count == 0)
        {
            ExitCombatMode();
        }
    }

    // =========================================================
    // Public API — Combat State
    // =========================================================

    /// <summary>
    /// Register an enemy as engaged in combat. Adds them to the TargetGroup gracefully.
    /// switches to combat camera if not already active.
    /// </summary>
    public void EnterCombat(EnemyController enemy, float weight = -1f)
    {
        if (enemy == null) return;

        float targetWeight = weight < 0f ? _defaultEnemyWeight : weight;
        float targetRadius = enemy.isEliteOrBoss ? _bossEnemyRadius : _enemyRadius;

        if (!_engagedEnemies.TryGetValue(enemy, out var ctx))
        {
            // Add enemy to target group with 0 weight natively to fade it in
            AddToTargetGroup(enemy.transform, 0f, targetRadius);
            
            _engagedEnemies[enemy] = new EngagedEnemyContext
            {
                targetMaxWeight = targetWeight,
                currentWeight = 0f,
                isLeaving = false
            };

            Debug.Log($"[CameraManager] EnterCombat: added {enemy.name} (weight={targetWeight:F2})");
        }
        else
        {
            // Update weight if already engaged, and cancel any "leaving" state
            ctx.targetMaxWeight = targetWeight;
            ctx.isLeaving = false;
            UpdateTargetRadius(enemy.transform, targetRadius);
        }

        RefreshBossCombatFraming();

        if (!_isInCombat)
        {
            _isInCombat = true;
            EnsurePlayerInTargetGroup();
            SetCombatMode();
        }
    }

    /// <summary>
    /// Tag an enemy to be removed from the combat TargetGroup.
    /// It will fade out smoothly before disengaging.
    /// </summary>
    public void LeaveCombat(EnemyController enemy)
    {
        if (enemy == null) return;

        if (_engagedEnemies.TryGetValue(enemy, out var ctx))
        {
            ctx.isLeaving = true;
            Debug.Log($"[CameraManager] LeaveCombat: fading out {enemy.name}");
        }
    }

    /// <summary>
    /// Dynamically update an engaged enemy's max weight.
    /// </summary>
    public void UpdateCombatTarget(EnemyController enemy, float weight)
    {
        if (enemy == null) return;
        
        if (_engagedEnemies.TryGetValue(enemy, out var ctx))
        {
            ctx.targetMaxWeight = weight;
        }
    }

    public EnemyController GetNearestEngagedEnemy(Vector2 fromPosition, bool includeLeavingTargets = false)
    {
        EnemyController nearestEnemy = null;
        float nearestDistanceSqr = float.MaxValue;
        float playerFacingDir = _playerTransform != null && _playerTransform.eulerAngles.y > 90f ? -1f : 1f;

        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (!includeLeavingTargets && ctx.isLeaving)
            {
                continue;
            }

            float distanceSqr = ((Vector2)enemy.transform.position - fromPosition).sqrMagnitude;
            float enemyDirX = enemy.transform.position.x - fromPosition.x;
            
            // Penalize enemies behind the player heavily so they are only chosen if no one is in front
            if (enemyDirX * playerFacingDir < 0f)
            {
                distanceSqr += 1000000f;
            }

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    public EnemyController GetProjectileTrackingTarget(Vector2 fromPosition, float facingSign, float maxRadius = 0f, bool includeLeavingTargets = false)
    {
        EnemyController inFront = GetWeightedEngagedEnemy(fromPosition, facingSign, maxRadius, includeLeavingTargets, true);
        return inFront != null
            ? inFront
            : GetWeightedEngagedEnemy(fromPosition, facingSign, maxRadius, includeLeavingTargets, false);
    }

    public EnemyController GetNearestEngagedEnemyInRadius(Vector2 fromPosition, float maxRadius = 0f, bool includeLeavingTargets = false)
    {
        EnemyController nearestEnemy = null;
        float nearestDistanceSqr = maxRadius > 0f ? maxRadius * maxRadius : float.MaxValue;

        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (!includeLeavingTargets && ctx.isLeaving)
            {
                continue;
            }

            float distanceSqr = ((Vector2)enemy.transform.position - fromPosition).sqrMagnitude;
            if (distanceSqr > nearestDistanceSqr)
            {
                continue;
            }

            nearestDistanceSqr = distanceSqr;
            nearestEnemy = enemy;
        }

        return nearestEnemy;
    }

    public void NotifyPlayerWarp(Vector3 positionDelta)
    {
        if (_playerTransform == null)
        {
            return;
        }

        if (_exploreCam != null)
        {
            if (positionDelta.sqrMagnitude > 0.000001f)
            {
                _exploreCam.OnTargetObjectWarped(_playerTransform, positionDelta);
            }
            _exploreCam.PreviousStateIsValid = false;
        }

        if (_combatCam != null)
        {
            if (positionDelta.sqrMagnitude > 0.000001f)
            {
                _combatCam.OnTargetObjectWarped(_playerTransform, positionDelta);
            }
            _combatCam.PreviousStateIsValid = false;
        }
    }

    public void BeginTemporaryPlayerSnap(float duration, bool smoothTransition = false, float smoothTime = 0.15f)
    {
        if (_exploreCam == null || _playerTransform == null)
        {
            return;
        }

        if (_temporaryPlayerSnapCoroutine != null)
        {
            StopCoroutine(_temporaryPlayerSnapCoroutine);
            _temporaryPlayerSnapCoroutine = null;
        }

        if (!_isTemporarilySnappedToPlayer)
        {
            _cachedExploreDamping = _explorePositionComposer != null ? _explorePositionComposer.Damping : Vector3.zero;
            if (_brain != null)
            {
                _cachedBrainBlend = _brain.DefaultBlend;
            }
        }

        _isTemporarilySnappedToPlayer = true;

        if (_explorePositionComposer != null)
        {
            _explorePositionComposer.Damping = smoothTransition ? new Vector3(0.25f, 0.25f, 0.25f) : Vector3.zero;
        }

        if (_brain != null)
        {
            _brain.DefaultBlend = smoothTransition 
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, smoothTime) 
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        _exploreCam.Priority = _activePriority + 100;
        if (_combatCam != null)
        {
            _combatCam.Priority = _inactivePriority;
        }
        if (_staticCam != null)
        {
            _staticCam.Priority = _inactivePriority;
        }
        if (_ultimateCam != null)
        {
            _ultimateCam.Priority = _inactivePriority;
        }

        NotifyPlayerWarp(Vector3.zero);

        if (duration > 0f)
        {
            _temporaryPlayerSnapCoroutine = StartCoroutine(EndTemporaryPlayerSnapAfterDelay(duration));
        }
    }

    public void EndTemporaryPlayerSnap()
    {
        if (_temporaryPlayerSnapCoroutine != null)
        {
            StopCoroutine(_temporaryPlayerSnapCoroutine);
            _temporaryPlayerSnapCoroutine = null;
        }

        if (!_isTemporarilySnappedToPlayer)
        {
            return;
        }

        _isTemporarilySnappedToPlayer = false;

        if (_explorePositionComposer != null)
        {
            _explorePositionComposer.Damping = _cachedExploreDamping;
        }

        if (_brain != null)
        {
            _brain.DefaultBlend = _cachedBrainBlend;
        }

        if (_isInCombat)
        {
            SetCombatMode();
        }
        else
        {
            SetExploreMode();
        }

        NotifyPlayerWarp(Vector3.zero);
    }

    /// <summary>
    /// Temporarily frames an authored Transform (Level-room display, etc.) above room cameras.
    /// Pair with <see cref="EndTemporaryFocus"/>.
    /// </summary>
    public void BeginTemporaryFocus(Transform focusTarget, float blendSeconds = 0.35f)
    {
        if (focusTarget == null)
        {
            return;
        }

        EndTemporaryPlayerSnap();

        if (!_isTemporarilyFocused)
        {
            if (_exploreCam != null)
            {
                _cachedFocusExploreFollow = _exploreCam.Follow;
                _cachedFocusExploreLookAt = _exploreCam.LookAt;
            }

            if (_staticCam != null)
            {
                _cachedFocusStaticFollow = _staticCam.Follow;
                _cachedFocusStaticLookAt = _staticCam.LookAt;
            }

            if (_brain != null)
            {
                _cachedBrainBlend = _brain.DefaultBlend;
                _hasCachedBrainBlend = true;
            }
        }

        _isTemporarilyFocused = true;

        if (_brain != null)
        {
            float blend = Mathf.Max(0f, blendSeconds);
            _brain.DefaultBlend = blend <= 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend);
        }

        if (_exploreCam != null)
        {
            _exploreCam.Follow = focusTarget;
            _exploreCam.LookAt = focusTarget;
            _exploreCam.Priority = _activePriority + 100;
        }

        if (_combatCam != null)
        {
            _combatCam.Priority = _inactivePriority;
        }

        if (_staticCam != null)
        {
            _staticCam.Follow = focusTarget;
            _staticCam.LookAt = focusTarget;
            _staticCam.Priority = _inactivePriority;
        }

        if (_ultimateCam != null)
        {
            _ultimateCam.Priority = _inactivePriority;
        }

        NotifyPlayerWarp(Vector3.zero);
    }

    public void EndTemporaryFocus()
    {
        if (!_isTemporarilyFocused)
        {
            return;
        }

        _isTemporarilyFocused = false;

        if (_exploreCam != null)
        {
            _exploreCam.Follow = _cachedFocusExploreFollow != null ? _cachedFocusExploreFollow : _playerTransform;
            _exploreCam.LookAt = _cachedFocusExploreLookAt != null ? _cachedFocusExploreLookAt : _playerTransform;
        }

        if (_staticCam != null)
        {
            _staticCam.Follow = _cachedFocusStaticFollow;
            _staticCam.LookAt = _cachedFocusStaticLookAt;
        }

        if (_brain != null && _hasCachedBrainBlend)
        {
            _brain.DefaultBlend = _cachedBrainBlend;
        }

        RoomData currentRoom = _zoneStateProvider != null ? _zoneStateProvider.CurrentRoom : null;
        UpdateCameraForRoom(currentRoom);
        NotifyPlayerWarp(Vector3.zero);
    }

    // =========================================================
    // Public API — Screen Shake
    // =========================================================

    /// <summary>
    /// Fire a screen shake impulse with raw strength.
    /// Works with any active vcam that has a CinemachineImpulseListener.
    /// </summary>
    public void EmitImpulse(float strength)
    {
        if (_impulseSource == null || strength <= 0f) return;
        _impulseSource.GenerateImpulse(strength);
    }

    public void Shake(float strength)
    {
        EmitImpulse(strength);
    }

    /// <summary>
    /// Fire a screen shake impulse mapped to a DamageImpactTier.
    /// Designed to be called during HitStop for dramatic effect.
    /// </summary>
    public void ShakeByImpact(DamageImpactTier tier)
    {
        if (GameImpulseController.Instance != null)
        {
            GameImpulseController.Instance.PlayImpact(tier);
            return;
        }

        float strength = tier switch
        {
            DamageImpactTier.Low      => _lowShake,
            DamageImpactTier.Medium   => _mediumShake,
            DamageImpactTier.Heavy    => _heavyShake,
            DamageImpactTier.Ultimate => _ultimateShake,
            _                         => _lowShake
        };
        Shake(strength);
    }

    // =========================================================
    // Public API — Ultimate Zoom
    // =========================================================

    public void SetUltimateZoom(bool enable, float size = 5f)
    {
        // Stop any in-progress zoom coroutine
        if (_ultimateZoomCoroutine != null)
        {
            StopCoroutine(_ultimateZoomCoroutine);
            _ultimateZoomCoroutine = null;
        }

        if (enable)
        {
            if (_brain != null && !_hasCachedBrainBlend)
            {
                _cachedBrainBlend = _brain.DefaultBlend;
                _hasCachedBrainBlend = true;
            }

            if (_ultimateCam != null)
            {
                // Cache original sizes
                if (_originalUltimateSize < 0f)
                    _originalUltimateSize = _ultimateCam.Lens.OrthographicSize;

                // Set ultimate cam lens to target size, then let the brain blend
                // smoothly transition FROM the current active camera TO this one.
                // Do NOT change the current active camera's size — that would cause an instant jump.
                SetCameraSize(_ultimateCam, size);

                if (_brain != null)
                {
                    _brain.DefaultBlend = new CinemachineBlendDefinition(
                        CinemachineBlendDefinition.Styles.EaseInOut, _ultimateTransitionDuration);
                }
                _ultimateCam.Priority = _activePriority + 20;
            }
            else
            {
                // Fallback: lerp the currently active cam's OrthographicSize
                CinemachineCamera activeCam = _isInCombat ? _combatCam : _exploreCam;
                if (activeCam != null)
                {
                    if (_isInCombat)
                    {
                        if (_originalCombatSize < 0f) _originalCombatSize = activeCam.Lens.OrthographicSize;
                    }
                    else
                    {
                        if (_originalExploreSize < 0f) _originalExploreSize = activeCam.Lens.OrthographicSize;
                    }
                    _ultimateZoomCoroutine = StartCoroutine(LerpCameraSize(activeCam, size, _ultimateTransitionDuration));
                }
            }
        }
        else
        {
            if (_ultimateCam != null)
            {
                // Let the brain blend back to the normal active camera
                if (_brain != null && _hasCachedBrainBlend)
                {
                    _brain.DefaultBlend = new CinemachineBlendDefinition(
                        CinemachineBlendDefinition.Styles.EaseInOut, _ultimateTransitionDuration);
                }
                _ultimateCam.Priority = _inactivePriority;

                // After blend completes, restore brain blend and ultimate cam size
                _ultimateZoomCoroutine = StartCoroutine(RestoreAfterUltimateBlend());
            }
            else
            {
                // Fallback: lerp the active cam back to its original size
                CinemachineCamera activeCam = _isInCombat ? _combatCam : _exploreCam;
                float originalSize = _isInCombat ? _originalCombatSize : _originalExploreSize;
                if (activeCam != null && originalSize > 0f)
                {
                    _ultimateZoomCoroutine = StartCoroutine(LerpCameraSize(activeCam, originalSize, _ultimateTransitionDuration));
                }

                if (_brain != null && _hasCachedBrainBlend)
                {
                    _brain.DefaultBlend = _cachedBrainBlend;
                    _hasCachedBrainBlend = false;
                }
            }
        }
    }

    private System.Collections.IEnumerator LerpCameraSize(CinemachineCamera cam, float targetSize, float duration)
    {
        if (cam == null) yield break;
        float startSize = cam.Lens.OrthographicSize;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetCameraSize(cam, Mathf.Lerp(startSize, targetSize, t));
            yield return null;
        }
        SetCameraSize(cam, targetSize);
        _ultimateZoomCoroutine = null;
    }

    private System.Collections.IEnumerator RestoreAfterUltimateBlend()
    {
        // Wait for the brain blend to finish before resetting
        yield return new WaitForSecondsRealtime(_ultimateTransitionDuration);

        if (_originalUltimateSize > 0f)
            SetCameraSize(_ultimateCam, _originalUltimateSize);

        if (_brain != null && _hasCachedBrainBlend)
        {
            _brain.DefaultBlend = _cachedBrainBlend;
            _hasCachedBrainBlend = false;
        }

        _ultimateZoomCoroutine = null;
    }

    private void SetCameraSize(CinemachineCamera cam, float size)
    {
        if (cam == null) return;
        var lens = cam.Lens;
        lens.OrthographicSize = size;
        cam.Lens = lens;
    }

    // =========================================================
    // Public API — Y Damping (Explore cam only)
    // =========================================================

    public void LerpYDamping(bool isPlayerFalling)
    {
        if (_explorePositionComposer == null) return;

        if (_lerpYPanCoroutine != null)
            StopCoroutine(_lerpYPanCoroutine);

        _lerpYPanCoroutine = StartCoroutine(LerpYAction(isPlayerFalling));
    }

    // =========================================================
    // Private — Camera Mode Switching (Priority-Based)
    // =========================================================
    private void SetExploreMode()
    {
        if (_zoneStateProvider != null && _zoneStateProvider.CurrentRoom != null && _zoneStateProvider.CurrentRoom.UseStaticCamera)
        {
            SetStaticMode();
            return;
        }

        if (_exploreCam != null)
            _exploreCam.Priority = _activePriority;

        if (_combatCam != null)
            _combatCam.Priority = _inactivePriority;

        if (_staticCam != null)
            _staticCam.Priority = _inactivePriority;

        if (_ultimateCam != null)
            _ultimateCam.Priority = _inactivePriority;
    }

    private void SetCombatMode()
    {
        if (_zoneStateProvider != null && _zoneStateProvider.CurrentRoom != null && _zoneStateProvider.CurrentRoom.UseStaticCamera)
        {
            SetStaticMode();
            return;
        }

        if (_combatCam != null)
            _combatCam.Priority = _activePriority;

        if (_exploreCam != null)
            _exploreCam.Priority = _inactivePriority;

        if (_staticCam != null)
            _staticCam.Priority = _inactivePriority;

        if (_ultimateCam != null)
            _ultimateCam.Priority = _inactivePriority;
    }

    private void SetStaticMode()
    {
        if (_staticCam != null)
        {
            _staticCam.Priority = _activePriority + 10;
        }

        if (_exploreCam != null)
        {
            _exploreCam.Priority = _inactivePriority;
        }

        if (_combatCam != null)
        {
            _combatCam.Priority = _inactivePriority;
        }

        if (_ultimateCam != null)
        {
            _ultimateCam.Priority = _inactivePriority;
        }
    }

    private void HandleRoomChanged(RoomData room)
    {
        UpdateCameraForRoom(room);
    }

    private void UpdateCameraForRoom(RoomData room)
    {
        if (room != null && room.UseStaticCamera)
        {
            if (_staticCam != null)
            {
                _staticCam.Follow = null;
                _staticCam.LookAt = null;
            }
            SetStaticMode();
        }
        else
        {
            if (_staticCam != null)
            {
                _staticCam.Priority = _inactivePriority;
            }

            if (_isInCombat)
            {
                SetCombatMode();
            }
            else
            {
                SetExploreMode();
            }
        }
    }

    private void ExitCombatMode()
    {
        _isInCombat = false;
        RestoreDefaultCombatFraming();

        // Leave the Player inside the TargetGroup so the CombatCam doesn't snap
        // to (0,0), allowing Cinemachine to smoothly blend to the ExploreCam.
        
        SetExploreMode();

        Debug.Log("[CameraManager] Exited combat mode → Explore");
    }

    // =========================================================
    // Private — TargetGroup Management
    // =========================================================
    private void EnsurePlayerInTargetGroup()
    {
        if (_combatTargetGroup == null || _playerTransform == null) return;

        // Check if player is already in the group
        for (int i = 0; i < _combatTargetGroup.Targets.Count; i++)
        {
            if (_combatTargetGroup.Targets[i].Object == _playerTransform)
                return;
        }

        _combatTargetGroup.Targets.Add(new CinemachineTargetGroup.Target
        {
            Object = _playerTransform,
            Weight = _playerWeight,
            Radius = _playerRadius
        });
    }

    private void AddToTargetGroup(Transform target, float weight, float radius)
    {
        if (_combatTargetGroup == null) return;

        _combatTargetGroup.Targets.Add(new CinemachineTargetGroup.Target
        {
            Object = target,
            Weight = weight,
            Radius = radius
        });
    }

    private void RemoveFromTargetGroup(Transform target)
    {
        if (_combatTargetGroup == null || target == null) return;

        for (int i = _combatTargetGroup.Targets.Count - 1; i >= 0; i--)
        {
            if (_combatTargetGroup.Targets[i].Object == target)
            {
                _combatTargetGroup.Targets.RemoveAt(i);
                break;
            }
        }
    }

    private void UpdateTargetWeight(Transform target, float weight)
    {
        if (_combatTargetGroup == null || target == null) return;

        for (int i = 0; i < _combatTargetGroup.Targets.Count; i++)
        {
            if (_combatTargetGroup.Targets[i].Object == target)
            {
                var t = _combatTargetGroup.Targets[i];
                t.Weight = weight;
                _combatTargetGroup.Targets[i] = t;
                break;
            }
        }
    }

    private void UpdateTargetRadius(Transform target, float radius)
    {
        if (_combatTargetGroup == null || target == null) return;

        for (int i = 0; i < _combatTargetGroup.Targets.Count; i++)
        {
            if (_combatTargetGroup.Targets[i].Object == target)
            {
                var t = _combatTargetGroup.Targets[i];
                t.Radius = radius;
                _combatTargetGroup.Targets[i] = t;
                break;
            }
        }
    }

    private void RefreshBossCombatFraming()
    {
        bool wantsBossFraming = false;
        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            if (enemy != null && enemy.IsAlive && enemy.isEliteOrBoss && !kvp.Value.isLeaving)
            {
                wantsBossFraming = true;
                break;
            }
        }

        if (wantsBossFraming)
            ApplyBossCombatFraming();
        else
            RestoreDefaultCombatFraming();
    }

    private void ApplyBossCombatFraming()
    {
        if (_combatGroupFraming == null)
            CacheCombatGroupFraming();
        if (_combatGroupFraming == null)
            return;

        _combatGroupFraming.OrthoSizeRange = _bossOrthoSizeRange;
        _combatGroupFraming.FramingSize = _bossFramingSize;
        _bossFramingActive = true;
    }

    private void RestoreDefaultCombatFraming()
    {
        if (!_bossFramingActive || _combatGroupFraming == null)
            return;

        _combatGroupFraming.OrthoSizeRange = _defaultCombatOrthoSizeRange;
        _combatGroupFraming.FramingSize = _defaultCombatFramingSize;
        _bossFramingActive = false;
    }

    private void CleanupNullTargets()
    {
        if (_combatTargetGroup == null) return;
        
        for (int i = _combatTargetGroup.Targets.Count - 1; i >= 0; i--)
        {
            if (_combatTargetGroup.Targets[i].Object == null)
            {
                _combatTargetGroup.Targets.RemoveAt(i);
            }
        }
    }

    private EnemyController GetWeightedEngagedEnemy(
        Vector2 fromPosition,
        float facingSign,
        float maxRadius,
        bool includeLeavingTargets,
        bool requireInFront)
    {
        EnemyController bestEnemy = null;
        float bestWeight = float.MinValue;
        float bestDistanceSqr = float.MaxValue;
        float maxRadiusSqr = maxRadius > 0f ? maxRadius * maxRadius : float.MaxValue;
        float resolvedFacing = facingSign < 0f ? -1f : 1f;

        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (!includeLeavingTargets && ctx.isLeaving)
            {
                continue;
            }

            Vector2 toEnemy = (Vector2)enemy.transform.position - fromPosition;
            float distanceSqr = toEnemy.sqrMagnitude;
            if (distanceSqr > maxRadiusSqr)
            {
                continue;
            }

            if (requireInFront && toEnemy.x * resolvedFacing < 0f)
            {
                continue;
            }

            float candidateWeight = ctx.currentWeight;
            if (bestEnemy == null
                || candidateWeight > bestWeight
                || (Mathf.Approximately(candidateWeight, bestWeight) && distanceSqr < bestDistanceSqr))
            {
                bestEnemy = enemy;
                bestWeight = candidateWeight;
                bestDistanceSqr = distanceSqr;
            }
        }

        return bestEnemy;
    }

    private float EvaluateDesiredEnemyWeight(EnemyController enemy, EngagedEnemyContext ctx, out bool shouldLeaveByRange)
    {
        shouldLeaveByRange = false;

        float detectionRadius = Mathf.Max(0.01f, GetEnemyDetectionRadius(enemy));
        float secondRangeLimit = detectionRadius + Mathf.Max(0f, _secondRangeExtension);
        float distanceToPlayer = Vector2.Distance(_playerTransform.position, enemy.transform.position);
        bool keepBeyondRange = ShouldKeepEnemyInCombatGroup(enemy);

        if (distanceToPlayer <= detectionRadius)
        {
            float firstBandT = Mathf.Clamp01(distanceToPlayer / detectionRadius);
            float weightFactor = Mathf.Lerp(1f, _firstRangeMinWeightFactor, firstBandT);
            return ctx.targetMaxWeight * weightFactor;
        }

        if (distanceToPlayer <= secondRangeLimit || keepBeyondRange)
        {
            if (secondRangeLimit <= detectionRadius)
            {
                return ctx.targetMaxWeight * _secondRangeMinWeightFactor;
            }

            float clampedDistance = keepBeyondRange
                ? Mathf.Min(distanceToPlayer, secondRangeLimit)
                : distanceToPlayer;
            float secondBandT = Mathf.InverseLerp(detectionRadius, secondRangeLimit, clampedDistance);
            float weightFactor = Mathf.Lerp(_firstRangeMinWeightFactor, _secondRangeMinWeightFactor, secondBandT);
            return ctx.targetMaxWeight * weightFactor;
        }

        shouldLeaveByRange = true;
        return 0f;
    }

    private float GetEnemyDetectionRadius(EnemyController enemy)
    {
        float detectionRadius = 8f;
        if (enemy == null)
        {
            return detectionRadius;
        }

        if (enemy.isEliteOrBoss)
        {
            return Mathf.Max(detectionRadius, 48f);
        }

        var behaviorAgent = enemy.GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent != null && behaviorAgent.GetVariable("DetectRadius", out BlackboardVariable<float> radiusVar) && radiusVar != null)
        {
            detectionRadius = radiusVar.Value;
        }

        return detectionRadius;
    }

    private static bool ShouldKeepEnemyInCombatGroup(EnemyController enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        // Boss fights stay on combat framing for the whole encounter.
        if (enemy.isEliteOrBoss)
        {
            return true;
        }

        EnemyReactionState reactionState = enemy.CurrentReactionState;
        return reactionState == EnemyReactionState.Knockup
            || reactionState == EnemyReactionState.Knockfall;
    }

    // =========================================================
    // Private — Y Damping Lerp (Explore cam)
    // =========================================================
    private IEnumerator LerpYAction(bool isPlayerFalling)
    {
        if (_explorePositionComposer == null)
            yield break;

        IsLerpingYDamping = true;

        float startDampAmount = _explorePositionComposer.Damping.y;
        float endDampAmount;

        if (isPlayerFalling)
        {
            endDampAmount = _fallPanAmount;
            LerpedFromPlayerFalling = true;
        }
        else
        {
            endDampAmount = _normYPanAmount;
        }

        float elapsedTime = 0f;
        while (elapsedTime < _fallYPanTime)
        {
            elapsedTime += Time.deltaTime;
            float lerpedPanAmount = Mathf.Lerp(startDampAmount, endDampAmount, elapsedTime / _fallYPanTime);

            Vector3 currentDamping = _explorePositionComposer.Damping;
            currentDamping.y = lerpedPanAmount;
            _explorePositionComposer.Damping = currentDamping;

            yield return null;
        }

        Vector3 finalDamping = _explorePositionComposer.Damping;
        finalDamping.y = endDampAmount;
        _explorePositionComposer.Damping = finalDamping;

        IsLerpingYDamping = false;
    }

    private IEnumerator EndTemporaryPlayerSnapAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        EndTemporaryPlayerSnap();
    }
}
