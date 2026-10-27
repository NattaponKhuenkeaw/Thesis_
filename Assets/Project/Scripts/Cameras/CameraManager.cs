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
public partial class CameraManager : MonoBehaviour
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

    private CameraLookAhead _cameraLookAhead;

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
    [Tooltip("If true, dynamic step damping changes will be triggered on vertical falls. Defaults to false (single source of truth via CameraLookAhead proxy target).")]
    [SerializeField] private bool _enableLegacyDynamicDamping = false;
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
    private Coroutine _focusReturnCoroutine;
    private CinemachineCamera _focusCam;
    private Transform _bossSpawnFocusTarget;
    private CinemachineBlendDefinition _focusOriginalBlend;
    private bool _hasFocusOriginalBlend;
    private Vector3 _focusReturnPosition;
    private float _focusReturnSize;
    private float _originalExploreSize = -1f;
    private float _originalCombatSize = -1f;
    private float _originalStaticSize = -1f;
    private float _originalUltimateSize = -1f;
    private bool _hasCachedBrainBlend = false;
    private Coroutine _ultimateZoomCoroutine;

    private CinemachineGroupFraming _combatGroupFraming;
    private Vector2 _defaultCombatOrthoSizeRange = new Vector2(6.8f, 8f);
    private float _defaultCombatFramingSize = 0.47f;
    private float _defaultCombatFramingDamping = 2f;
    private bool _bossFramingActive;
    private bool _fastVerticalFollow;
    private Rigidbody2D _playerBody;

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

        _cameraLookAhead = GetComponentInChildren<CameraLookAhead>(true);
        if (_cameraLookAhead == null)
        {
            _cameraLookAhead = Object.FindFirstObjectByType<CameraLookAhead>();
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
        _defaultCombatFramingDamping = _combatGroupFraming.Damping;
    }

    private void Start()
    {
        if (_zoneStateProvider != null)
        {
            _zoneStateProvider.OnRoomChanged += HandleRoomChanged;
            UpdateCameraForRoom(_zoneStateProvider.CurrentRoom);
        }

        SyncEngagedEnemiesFromRegistry();
    }

    private void OnDestroy()
    {
        ClearActionCamera();
        if (instance == this)
            instance = null;

        if (_zoneStateProvider != null)
        {
            _zoneStateProvider.OnRoomChanged -= HandleRoomChanged;
        }
    }

    private void OnDisable()
    {
        ClearActionCamera();
        FinishPendingFocusReturn();
        DisposeFocusCamera();
    }

    private void LateUpdate()
    {
        if (_bossSpawnFocusTarget != null && _focusCam != null && _focusReturnCoroutine == null)
        {
            Collider2D body = _bossSpawnFocusTarget.GetComponent<EnemyController>()?.bodyCollider;
            Vector3 center = body != null ? body.bounds.center : _bossSpawnFocusTarget.position;
            _focusCam.transform.position = new Vector3(center.x, center.y, _focusCam.transform.position.z);
        }
        TickVerticalFollow();
        TickEngagedEnemies();
        FitCombatZoomToRoom();
    }

    private void FitCombatZoomToRoom()
    {
        if (_combatCam == null || _combatGroupFraming == null)
            return;

        Vector2 range = _bossFramingActive ? _bossOrthoSizeRange : _defaultCombatOrthoSizeRange;
        RoomData room = _zoneStateProvider != null ? _zoneStateProvider.CurrentRoom : null;
        Camera output = _brain != null ? _brain.OutputCamera : Camera.main;
        var confiner = _combatCam.GetComponent<CinemachineConfiner2D>();
        if (room != null && room.RoomKind == RoomKind.Combat && !room.UseStaticCamera
            && output != null && confiner != null && confiner.enabled
            && confiner.BoundingShape2D is BoxCollider2D bounds && bounds.enabled)
        {
            range = RoomBoundaryMath.LimitCombatOrthoRange(range, bounds.bounds.size.x, output.aspect);
        }

        _combatGroupFraming.OrthoSizeRange = range;
    }

    private void TickEngagedEnemies()
    {
        if (_engagedEnemies.Count == 0)
        {
            RefreshCombatCameraMode(false);
            return;
        }

        bool anyFraming = false;
        List<EnemyController> toRemove = null;
        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            bool isDestroyed = enemy == null;
            if (isDestroyed || !CanRemainEngaged(enemy))
            {
                ctx.isLeaving = true;
            }

            float desiredWeight = 0f;
            if (!ctx.isLeaving && !isDestroyed && _playerTransform != null)
            {
                desiredWeight = EvaluateDesiredEnemyWeight(enemy, ctx);
            }

            ctx.currentWeight = Mathf.MoveTowards(ctx.currentWeight, desiredWeight, _weightLerpSpeed * Time.deltaTime);
            if (!isDestroyed)
            {
                UpdateTargetWeight(enemy.transform, ctx.currentWeight);
            }
            else
            {
                ctx.currentWeight = 0f;
            }

            if (!ctx.isLeaving && (desiredWeight > FramingWeightEpsilon || ctx.currentWeight > FramingWeightEpsilon))
            {
                anyFraming = true;
            }

            if (isDestroyed || (ctx.isLeaving && ctx.currentWeight <= FramingWeightEpsilon))
            {
                toRemove ??= new List<EnemyController>();
                toRemove.Add(enemy);
            }
        }

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
                    CleanupNullTargets();
                }

                _engagedEnemies.Remove(enemy);
            }

            RefreshBossCombatFraming();
        }

        RefreshCombatCameraMode(anyFraming && _engagedEnemies.Count > 0);
    }

    private const float FramingWeightEpsilon = 0.01f;

    private bool ShouldUseCombatFraming()
    {
        if (_playerTransform == null)
        {
            return false;
        }

        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            if (enemy == null || ctx.isLeaving || !CanRemainEngaged(enemy))
            {
                continue;
            }

            if (ctx.currentWeight > FramingWeightEpsilon)
            {
                return true;
            }

            if (EvaluateDesiredEnemyWeight(enemy, ctx) > FramingWeightEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanSwitchRoomCameras()
    {
        if (IsStaticCameraRoom() || _isTemporarilyFocused || _isTemporarilySnappedToPlayer)
        {
            return false;
        }

        if (_ultimateCam != null && _ultimateCam.Priority.Value >= _activePriority)
        {
            return false;
        }

        return true;
    }

    private void RefreshCombatCameraMode(bool wantsFraming)
    {
        if (!CanSwitchRoomCameras())
        {
            return;
        }

        if (wantsFraming)
        {
            if (_isInCombat)
            {
                return;
            }

            _isInCombat = true;
            EnsurePlayerInTargetGroup();
            SetCombatMode();
            return;
        }

        if (_isInCombat)
        {
            ExitCombatMode();
        }
    }

    private void ClearEngagedEnemies()
    {
        if (_engagedEnemies.Count == 0)
        {
            return;
        }

        foreach (var kvp in _engagedEnemies)
        {
            if (kvp.Key != null)
            {
                RemoveFromTargetGroup(kvp.Key.transform);
            }
        }

        _engagedEnemies.Clear();
        CleanupNullTargets();
        RefreshBossCombatFraming();
    }

    /// <summary>
    /// Combat flags that fired before this camera existed are rebound here. Does not mark
    /// idle room enemies as in combat.
    /// </summary>
    public void SyncEngagedEnemiesFromRegistry()
    {
        List<EnemyController> buffer = new List<EnemyController>();
        RoomEnemyRegistry.FillTargetable(buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            EnemyController enemy = buffer[i];
            if (enemy != null && enemy.InCombat)
            {
                EnterCombat(enemy);
            }
        }
    }

    private static bool CanRemainEngaged(EnemyController enemy)
    {
        return enemy != null && enemy.IsTargetable && enemy.InCombat;
    }

    private static bool CanSelectEngagedTarget(EnemyController enemy, EngagedEnemyContext ctx, bool includeLeavingTargets)
    {
        if (!CanRemainEngaged(enemy) || ctx == null)
        {
            return false;
        }

        return includeLeavingTargets || !ctx.isLeaving;
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
        if (enemy == null || !enemy.IsTargetable)
            return;
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
        RefreshCombatCameraMode(ShouldUseCombatFraming());
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
            if (!CanSelectEngagedTarget(enemy, ctx, includeLeavingTargets))
            {
                continue;
            }

            float distanceSqr = ((Vector2)enemy.transform.position - fromPosition).sqrMagnitude;
            if (!IsWithinQueryRange(enemy, distanceSqr, 0f))
            {
                continue;
            }

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
        float nearestDistanceSqr = float.MaxValue;

        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            if (!CanSelectEngagedTarget(enemy, ctx, includeLeavingTargets))
            {
                continue;
            }

            float distanceSqr = ((Vector2)enemy.transform.position - fromPosition).sqrMagnitude;
            if (!IsWithinQueryRange(enemy, distanceSqr, maxRadius))
            {
                continue;
            }

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

    /// <summary>
    /// Room enter / layout swap: drop leftover combat framing and snap follow to the player.
    /// </summary>
    public void SnapToPlayerForRoomEnter(Vector3 warpDelta)
    {
        if (_playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
        }

        ClearEngagedEnemies();
        ExitCombatMode();
        CameraLookAhead lookAhead = Object.FindFirstObjectByType<CameraLookAhead>();
        lookAhead?.SnapToPlayer();
        NotifyPlayerWarp(warpDelta);
        BeginTemporaryPlayerSnap(0.05f, smoothTransition: false);
    }

    public void ResetLookAheadForCharacterSwap(Rigidbody2D incomingBody, float centerSeconds)
    {
        ClearActionCamera();
        if (incomingBody == null) return;
        _playerTransform = incomingBody.transform;
        if (_cameraLookAhead == null) _cameraLookAhead = Object.FindFirstObjectByType<CameraLookAhead>();
        float duration = Mathf.Max(Time.unscaledDeltaTime, centerSeconds);
        _cameraLookAhead?.ResetHorizontalForCharacterSwap(incomingBody, duration);
        float release = _cameraLookAhead != null ? _cameraLookAhead.horizontalLookAheadDuration : 0.25f;
        CenterQteIntroX(_exploreCam, incomingBody.transform, duration, release);
        CenterQteIntroX(_combatCam, incomingBody.transform, duration, release);
        CenterQteIntroX(_staticCam, incomingBody.transform, duration, release);
        CenterQteIntroX(_ultimateCam, incomingBody.transform, duration, release);
    }

    private static void CenterQteIntroX(CinemachineCamera camera, Transform target, float duration, float release)
    {
        if (camera == null) return;
        GetQteIntroCameraImpact(camera).CenterHorizontal(target, duration, release);
    }

    public void PlayQteIntroImpact(float sizeReduction, int frames)
    {
        if (sizeReduction <= 0f) return;
        PlayQteIntroImpact(_exploreCam, sizeReduction, frames);
        PlayQteIntroImpact(_combatCam, sizeReduction, frames);
        PlayQteIntroImpact(_staticCam, sizeReduction, frames);
        PlayQteIntroImpact(_ultimateCam, sizeReduction, frames);
    }

    private static void PlayQteIntroImpact(CinemachineCamera camera, float sizeReduction, int frames)
    {
        if (camera == null) return;
        GetQteIntroCameraImpact(camera).Play(sizeReduction, frames);
    }

    private static QteIntroCameraImpact GetQteIntroCameraImpact(CinemachineCamera camera)
    {
        QteIntroCameraImpact impact = camera.GetComponent<QteIntroCameraImpact>();
        return impact != null ? impact : camera.gameObject.AddComponent<QteIntroCameraImpact>();
    }

    public void BeginTemporaryPlayerSnap(float duration, bool smoothTransition = false, float smoothTime = 0.15f)
    {
        if (_exploreCam == null || _playerTransform == null)
        {
            return;
        }

        // Static rooms frame authored content, never the player. Snapping raised the explore
        // camera with a zero-length blend, so entering a safe room popped to player framing
        // and back within a few frames.
        if (IsStaticCameraRoom())
        {
            NotifyPlayerWarp(Vector3.zero);
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
    public bool TryBeginBossSpawnFocus(Transform boss, float blendSeconds)
    {
        if (boss == null || !isActiveAndEnabled || _isTemporarilyFocused || _focusReturnCoroutine != null
            || _exploreCam == null || !CanSwitchRoomCameras())
            return false;

        BeginTemporaryFocus(boss, blendSeconds, true);
        _bossSpawnFocusTarget = boss;
        return _focusCam != null;
    }

    public void EndBossSpawnFocus(Transform boss, float blendSeconds)
    {
        if (_bossSpawnFocusTarget == boss && boss != null)
            EndTemporaryFocus(blendSeconds);
    }

    public void BeginTemporaryFocus(Transform focusTarget, float blendSeconds = 0.35f, bool bossSpawn = false)
    {
        ClearActionCamera();
        CinemachineCamera sourceCam = bossSpawn ? _exploreCam : (_staticCam != null ? _staticCam : _exploreCam);
        if (focusTarget == null || sourceCam == null)
        {
            return;
        }

        FinishPendingFocusReturn();
        EndTemporaryPlayerSnap();

        if (!_isTemporarilyFocused)
        {
            if (_brain != null)
            {
                _focusOriginalBlend = _brain.DefaultBlend;
                _hasFocusOriginalBlend = true;
            }

            _focusReturnPosition = sourceCam.transform.position;
            _focusReturnSize = sourceCam.Lens.OrthographicSize;
            _focusCam = Instantiate(sourceCam, sourceCam.transform.parent);
            _focusCam.name = "CinemachineCamera_EventFocus";
        }

        _isTemporarilyFocused = true;

        if (_brain != null)
        {
            float blend = Mathf.Max(0f, blendSeconds);
            _brain.DefaultBlend = blend <= 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend);
        }

        ConfigureEventFocusCamera(_focusCam, focusTarget);
        if (bossSpawn)
        {
            var confiner = _focusCam.GetComponent<CinemachineConfiner2D>();
            if (confiner != null)
                confiner.enabled = true;
        }
        else
            _bossSpawnFocusTarget = null;
        _focusCam.Priority = _activePriority + 100;
        _focusCam.gameObject.SetActive(true);
        _focusCam.PreviousStateIsValid = false;
    }

    private void ConfigureEventFocusCamera(CinemachineCamera cam, Transform focusTarget)
    {
        if (cam == null || focusTarget == null)
        {
            return;
        }

        CinemachineConfiner2D confiner = cam.GetComponent<CinemachineConfiner2D>();
        if (confiner != null)
        {
            confiner.enabled = false;
        }

        CinemachinePositionComposer composer = cam.GetComponent<CinemachinePositionComposer>();
        if (composer != null)
        {
            composer.enabled = false;
        }

        cam.Follow = null;
        cam.LookAt = null;

        Vector3 sourcePosition = cam.transform.position;
        Vector3 focusPosition = focusTarget.position;
        cam.transform.position = new Vector3(focusPosition.x, focusPosition.y, sourcePosition.z);

        if (_exploreCam != null)
        {
            SetCameraSize(cam, _exploreCam.Lens.OrthographicSize);
        }
    }

    public void EndTemporaryFocus(float blendSeconds = -1f)
    {
        if (!_isTemporarilyFocused && _focusReturnCoroutine == null)
        {
            return;
        }

        float duration = blendSeconds >= 0f ? Mathf.Max(0f, blendSeconds) : 0f;
        if (duration <= 0f || _focusCam == null || !isActiveAndEnabled)
        {
            FinishPendingFocusReturn();
            DisposeFocusCamera();
            return;
        }

        if (_focusReturnCoroutine != null)
        {
            return;
        }

        _focusReturnCoroutine = StartCoroutine(ReturnFocusToRoom(duration));
    }

    private IEnumerator ReturnFocusToRoom(float seconds)
    {
        CinemachineCamera cam = _focusCam;
        if (cam == null)
        {
            _focusReturnCoroutine = null;
            DisposeFocusCamera();
            yield break;
        }

        if (_brain != null)
        {
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        Vector3 startPos = cam.transform.position;
        float startSize = cam.Lens.OrthographicSize;
        Quaternion rotation = cam.transform.rotation;
        float elapsed = 0f;
        while (elapsed < seconds && cam != null)
        {
            // The player can move during the reveal; return to the live gameplay camera.
            if (_bossSpawnFocusTarget != null && _exploreCam != null)
            {
                CinemachineCamera destination = _isInCombat && _combatCam != null ? _combatCam : _exploreCam;
                _focusReturnPosition = destination.State.GetFinalPosition();
                _focusReturnSize = destination.State.Lens.OrthographicSize;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
            Vector3 position = Vector3.Lerp(startPos, _focusReturnPosition, t);
            cam.transform.SetPositionAndRotation(position, rotation);
            SetCameraSize(cam, Mathf.Lerp(startSize, _focusReturnSize, t));
            cam.PreviousStateIsValid = false;
            yield return null;
        }

        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(_focusReturnPosition, rotation);
            SetCameraSize(cam, _focusReturnSize);
            cam.PreviousStateIsValid = false;
        }

        _focusReturnCoroutine = null;
        DisposeFocusCamera();
    }

    private void DisposeFocusCamera()
    {
        _bossSpawnFocusTarget = null;
        _isTemporarilyFocused = false;
        if (_focusCam != null)
        {
            _focusCam.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(_focusCam.gameObject);
            else
                DestroyImmediate(_focusCam.gameObject);
            _focusCam = null;
        }
        if (_brain != null && _hasFocusOriginalBlend)
            _brain.DefaultBlend = _focusOriginalBlend;
        _hasFocusOriginalBlend = false;
    }

    private void FinishPendingFocusReturn()
    {
        if (_focusReturnCoroutine == null)
            return;

        StopCoroutine(_focusReturnCoroutine);
        _focusReturnCoroutine = null;
        DisposeFocusCamera();
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
        if (enable) ClearActionCamera();
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
    private bool IsStaticCameraRoom()
    {
        return _zoneStateProvider != null
            && _zoneStateProvider.CurrentRoom != null
            && _zoneStateProvider.CurrentRoom.UseStaticCamera;
    }

    private void SetExploreMode()
    {
        if (IsStaticCameraRoom())
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
        if (IsStaticCameraRoom())
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
        ClearActionCamera();
        FinishPendingFocusReturn();
        DisposeFocusCamera();
        ClearEngagedEnemies();
        ExitCombatMode();
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

        Transform playerCameraTarget = _cameraLookAhead != null
            ? _cameraLookAhead.transform
            : _playerTransform;

        int proxyIndex = -1;
        int playerIndex = -1;

        for (int i = 0; i < _combatTargetGroup.Targets.Count; i++)
        {
            Transform target = _combatTargetGroup.Targets[i].Object;
            if (target == playerCameraTarget)
            {
                proxyIndex = i;
            }
            else if (target == _playerTransform)
            {
                playerIndex = i;
            }
        }

        if (proxyIndex >= 0)
        {
            if (playerIndex >= 0)
            {
                _combatTargetGroup.Targets.RemoveAt(playerIndex);
            }
            return;
        }

        if (playerIndex >= 0)
        {
            CinemachineTargetGroup.Target existingTarget = _combatTargetGroup.Targets[playerIndex];
            existingTarget.Object = playerCameraTarget;
            _combatTargetGroup.Targets[playerIndex] = existingTarget;
            return;
        }

        _combatTargetGroup.Targets.Add(new CinemachineTargetGroup.Target
        {
            Object = playerCameraTarget,
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
        float resolvedFacing = facingSign < 0f ? -1f : 1f;

        foreach (var kvp in _engagedEnemies)
        {
            EnemyController enemy = kvp.Key;
            EngagedEnemyContext ctx = kvp.Value;
            if (!CanSelectEngagedTarget(enemy, ctx, includeLeavingTargets))
            {
                continue;
            }

            Vector2 toEnemy = (Vector2)enemy.transform.position - fromPosition;
            float distanceSqr = toEnemy.sqrMagnitude;
            if (!IsWithinQueryRange(enemy, distanceSqr, maxRadius))
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

    private float EvaluateDesiredEnemyWeight(EnemyController enemy, EngagedEnemyContext ctx)
    {
        float detectionRadius = Mathf.Max(0.01f, GetEnemyDetectionRadius(enemy));
        float secondRangeLimit = detectionRadius + Mathf.Max(0f, _secondRangeExtension);
        float distanceToPlayer = Vector2.Distance(_playerTransform.position, enemy.transform.position);
        bool keepBeyondRange = ShouldKeepEnemyInCombatGroup(enemy);

        if (distanceToPlayer <= detectionRadius)
        {
            float firstBandT = Mathf.Clamp01(distanceToPlayer / detectionRadius);
            float weightFactor = Mathf.Lerp(1f, _firstRangeMinWeightFactor, firstBandT);
            return ScaleWeightByVerticalSeparation(enemy, ctx.targetMaxWeight * weightFactor);
        }

        if (distanceToPlayer <= secondRangeLimit || keepBeyondRange)
        {
            if (secondRangeLimit <= detectionRadius)
            {
                return ScaleWeightByVerticalSeparation(enemy, ctx.targetMaxWeight * _secondRangeMinWeightFactor);
            }

            float clampedDistance = keepBeyondRange
                ? Mathf.Min(distanceToPlayer, secondRangeLimit)
                : distanceToPlayer;
            float secondBandT = Mathf.InverseLerp(detectionRadius, secondRangeLimit, clampedDistance);
            float weightFactor = Mathf.Lerp(_firstRangeMinWeightFactor, _secondRangeMinWeightFactor, secondBandT);
            return ScaleWeightByVerticalSeparation(enemy, ctx.targetMaxWeight * weightFactor);
        }

        return 0f;
    }

    private bool IsWithinQueryRange(EnemyController enemy, float distanceSqr, float maxRadius)
    {
        float limit = maxRadius > 0f ? maxRadius : GetEnemyDetectionRadius(enemy);
        return distanceSqr <= limit * limit;
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

    private float ScaleWeightByVerticalSeparation(EnemyController enemy, float weight)
    {
        if (enemy == null || enemy.isEliteOrBoss || _playerTransform == null)
        {
            return weight;
        }

        float verticalSeparation = Mathf.Abs(enemy.transform.position.y - _playerTransform.position.y);
        return weight * VerticalFramingWeightScale(verticalSeparation);
    }

    /// <summary>
    /// Enemies on another floor should not hold the combat camera.
    /// Full weight within 2.5 units; none by 5.5 (about one platform drop).
    /// </summary>
    public static float VerticalFramingWeightScale(float verticalSeparation)
    {
        const float fullWeightSeparation = 2.5f;
        const float zeroWeightSeparation = 5.5f;
        if (verticalSeparation <= fullWeightSeparation)
        {
            return 1f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(zeroWeightSeparation, fullWeightSeparation, verticalSeparation));
    }

    private void EnsurePlayerReferences()
    {
        if (_playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
        }

        if (_playerBody == null && _playerTransform != null)
        {
            _playerBody = _playerTransform.GetComponent<Rigidbody2D>();
        }
    }

    private void TickVerticalFollow()
    {
        EnsurePlayerReferences();

        // Architectural design: vertical fall leading is delegated to CameraLookAhead (Proxy Target)
        // to ensure mathematically seamless C1 damping without velocity spikes or threshold fluttering.
        if (!_enableLegacyDynamicDamping)
        {
            return;
        }

        if (_playerBody == null)
        {
            return;
        }

        Camera outputCamera = _brain != null ? _brain.OutputCamera : Camera.main;
        float cameraY = outputCamera != null ? outputCamera.transform.position.y : _playerBody.position.y;
        bool falling = _playerBody.linearVelocity.y < -0.8f;
        bool farFromCamera = Mathf.Abs(_playerBody.position.y - cameraY) > 2.25f;
        bool wantFast = falling || farFromCamera;
        if (wantFast == _fastVerticalFollow)
        {
            return;
        }

        _fastVerticalFollow = wantFast;
        LerpYDamping(wantFast);
        if (_combatGroupFraming != null)
        {
            _combatGroupFraming.Damping = wantFast ? _fallPanAmount : _defaultCombatFramingDamping;
        }
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
