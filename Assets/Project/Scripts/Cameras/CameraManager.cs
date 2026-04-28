using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Unity.Behavior;

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
            if (UnityEditor.Selection.activeGameObject == gameObject)
                UnityEditor.Selection.activeGameObject = null;
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

        // Cache explore cam's PositionComposer for Y-damping
        if (_exploreCam != null)
            _explorePositionComposer = _exploreCam.GetComponent<CinemachinePositionComposer>();

        // Start in explore mode
        SetExploreMode();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
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

        if (!_engagedEnemies.TryGetValue(enemy, out var ctx))
        {
            // Add enemy to target group with 0 weight natively to fade it in
            AddToTargetGroup(enemy.transform, 0f, _enemyRadius);
            
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
        }

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

    public void BeginTemporaryPlayerSnap(float duration)
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
            _explorePositionComposer.Damping = Vector3.zero;
        }

        if (_brain != null)
        {
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        _exploreCam.Priority = _activePriority + 100;
        if (_combatCam != null)
        {
            _combatCam.Priority = _inactivePriority;
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
        if (_exploreCam != null)
            _exploreCam.Priority = _activePriority;

        if (_combatCam != null)
            _combatCam.Priority = _inactivePriority;
    }

    private void SetCombatMode()
    {
        if (_combatCam != null)
            _combatCam.Priority = _activePriority;

        if (_exploreCam != null)
            _exploreCam.Priority = _inactivePriority;
    }

    private void ExitCombatMode()
    {
        _isInCombat = false;

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
