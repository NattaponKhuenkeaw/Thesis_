using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared movement for Behavior Graph enemies. Nodes ask this for "enter attack slot"
/// instead of each action hard-coding X-only chase.
/// Ground/Fly drive Rigidbody2D velocity; Warp teleports. Do not use MovePosition
/// on a Dynamic body (fights gravity and contacts).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyController))]
public class EnemyLocomotion : MonoBehaviour
{
    [SerializeField] private EnemyMoveMode moveMode = EnemyMoveMode.Ground;

    [Tooltip("Slot relative to the target. X = distance in front (current side), Y = height for Fly.")]
    [SerializeField] private Vector2 attackSlotOffset = new Vector2(1.5f, 1.2f);

    [SerializeField, Min(0.05f)] private float arrivalThreshold = 0.2f;
    [Tooltip("While moving, face the walk direction. Attack still turns to the player when standing.")]
    [SerializeField] private bool faceTargetOnMove = true;

    [Tooltip("Enable for Fly/Warp kits. Ground kits leave this off.")]
    [SerializeField] private bool allowNonGroundMove = false;

    [Header("Ground Reach")]
    [Tooltip("If the target's feet are farther than this in Y, treat as another platform.")]
    [SerializeField, Min(0.05f)] private float samePlatformMaxY = 0.85f;
    [SerializeField, Min(0.05f)] private float ledgeProbeDistance = 0.2f;
    [SerializeField, Min(0.05f)] private float groundProbeDistance = 0.22f;
    [SerializeField] private LayerMask groundLayers;

    [Header("Fixed Height Flight")]
    [Tooltip("Use platform-relative flight, or enable roaming player-relative height below.")]
    [SerializeField] private bool fixedHeightFlight;
    [Tooltip("World-space clearance between the body feet and the supporting platform.")]
    [SerializeField, Min(0.1f)] private float flightHeight = 1.5f;
    [Tooltip("Roaming flight measures height above the player instead of the spawn platform.")]
    [SerializeField] private bool flightHeightRelativeToPlayer;
    [Tooltip("Wait before rising with a jumping player. Zero keeps immediate height following.")]
    [SerializeField, Min(0f)] private float flightPlayerRiseDelay;
    [Tooltip("Scales the complete player-relative altitude, including variation and minimum head clearance.")]
    [SerializeField, Min(1f)] private float flightHeightMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float flightGroundTolerance = 0.2f;
    [SerializeField, Min(0.1f)] private float flightGroundSearchDistance = 20f;
    [Tooltip("How far the flyer travels left and right once the target is already in shot range.")]
    [SerializeField, Min(0.5f)] private float flightPatrolSpan = 6f;
    [SerializeField, Min(0.1f)] private float flightPatrolSpeed = 3f;
    [SerializeField] private bool roamingFlight;
    [SerializeField] private Vector2 flightTargetDistance = new Vector2(3f, 5f);
    [SerializeField, Min(0.1f)] private float flightTravelDuration = 1.1f;
    [Tooltip("Zero uses legacy roaming. Positive values enable acceleration-limited velocity steering.")]
    [SerializeField, Min(0f)] private float flightSteeringAcceleration;
    [SerializeField, Min(0.1f)] private float flightBrakingAcceleration = 12f;
    [Tooltip("Minimum time to prefer the same flank while roaming. Obstructed routes can still use the other flank.")]
    [SerializeField, Min(0f)] private float flightSideHoldTime;
    [Tooltip("Zero keeps roaming-only speed. Positive values enable faster approach and early firing positions.")]
    [SerializeField, Min(0f)] private float flightCatchUpSpeed;
    [SerializeField, Min(0.1f)] private float flightCatchUpEnterDistance = 8f;
    [SerializeField, Min(0f)] private float flightCatchUpExitDistance = 6f;
    [SerializeField, Min(0f)] private float flightArcHeight = 0.35f;
    [SerializeField, Min(0f)] private float flightHeightVariation = 0.15f;
    [SerializeField, Min(0f)] private float flightInterruptedRest = 1.2f;
    [SerializeField, Min(0f)] private float flightPostAttackRest = 1f;
    [SerializeField, Min(0f)] private float flightAttackHeight;
    private float flightAttackBodyY;
    private bool flightAttackDescending;
    [Tooltip("Ground-anchored roaming changes floor only after the player has landed for this long.")]
    [SerializeField, Min(0.1f)] private float flightGroundSettleTime = 0.6f;
    [Tooltip("Maximum horizontal search around the authored spawn when the flying body would overlap scenery.")]
    [SerializeField, Min(0f)] private float flightSpawnSearchRadius = 3f;

    // Both distances below are measured against the body, which is 1.35 wide on Enemy1-3. Anything
    // smaller reads two bodies as separate while they are visibly inside one another.
    [Header("Unstack")]
    [Tooltip("If another enemy is closer than this on X, walk apart. Keep it at roughly the body width.")]
    [SerializeField, Min(0.1f)] private float overlapRadius = 1.35f;
    [SerializeField, Min(0.1f)] private float unstackSpeed = 2.2f;
    [SerializeField, Min(0.05f)] private float overlapSameY = 0.85f;
    [Tooltip("Gap between queued bodies. Below the body width the formation asks them to stand inside each other.")]
    [SerializeField, Min(0.1f)] private float crowdSlotSpacing = 1.5f;
    [Tooltip("Seconds overlap must last before they walk apart.")]
    [SerializeField, Min(0f)] private float overlapConfirmTime = 0.4f;
    [Tooltip("Once they start splitting, keep that direction this long so they do not flip every frame.")]
    [SerializeField, Min(0f)] private float unstackCommitTime = 0.45f;

    // Two bodies within this much of the same distance count as tied, so they do not swap queue slots
    // every frame while they jostle.
    private const float CrowdRankEpsilon = 0.05f;

    [Header("Ground Melee Flanking")]
    [Tooltip("Split this melee pack across the player's two flanks. Only enabled on Enemy101.")]
    [SerializeField] private bool groundMeleeFlanking;
    [Tooltip("Gently spread grounded melee packs without restoring solid collisions between flankers.")]
    [SerializeField] private bool softGroundSeparation;
    [SerializeField, Min(0.1f)] private float softSeparationSpacing = 0.55f;
    [SerializeField, Min(0.1f)] private float softSeparationSpeed = 1.8f;
    [Tooltip("Maximum extra spread either side of a collapsed pack. Does not pull an already wider pack together.")]
    [SerializeField, Min(0.1f)] private float softSeparationHalfWidth = 0.8f;
    private float softWalkingUntil;
    private readonly List<EnemyLocomotion> softSeparationPack = new List<EnemyLocomotion>(5);
    private Transform meleeFlankTarget;
    private float meleeFlankSide;
    private float meleeFlankTargetX;
    private float meleeFlankFootY;
    private bool meleeFlankCrossing;
    private float meleeFlankArrivalGap;
    private readonly List<Collider2D> meleePassThroughBodies = new List<Collider2D>(4);

    private static readonly List<EnemyController> livingEnemies = new List<EnemyController>(16);

    private EnemyController enemy;
    private Rigidbody2D body;
    private Transform cachedTransform;
    private float cachedGravityScale = 1f;
    private float platformPatrolSign = 1f;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[32];
    private float overlapElapsed;
    private float unstackUntil;
    private float unstackSign;
    private bool flightInitialized;
    private float flightWorldY;
    private float flightGroundY;
    private RigidbodyConstraints2D flightOriginalConstraints;
    private bool flightOwnsConstraints;
    private bool flightReactionAllowsVertical;
    private float flightPatrolAnchorX;
    private float flightPatrolSign = 1f;
    private PlayerCharacter flightPatrolPlayer;
    private Transform flightHeightPlayer;
    private float flightPlayerReferenceY;
    private float flightPlayerRiseElapsed;
    private bool flightPlayerRiseUnlocked;
    private bool WaitingForPlayerRise => flightPlayerRiseDelay > 0f && flightPlayerRiseElapsed > 0f && !flightPlayerRiseUnlocked;
    private enum FlightBeat { Moving, Ready, Attacking, Resting }
    private FlightBeat flightBeat;
    private float flightBeatTime;
    private Vector2 flightMoveStart;
    private Vector2 flightMoveEnd;
    private float flightMoveDuration;
    private float flightMovePlayerOffsetX;
    private float flightMoveHeight;
    private float flightTravelVelocityX;
    private Vector2 flightSteeringVelocity;
    private float flightRoamSide;
    private float flightSideUntil;
    private bool flightCatchingUp;
    private readonly RaycastHit2D[] flightWallHits = new RaycastHit2D[32];
    private readonly Collider2D[] flightOverlapHits = new Collider2D[32];
    private Transform flightGroundPlayer;
    private float flightPendingGroundY;
    private float flightGroundSettleElapsed;
    private bool UsesGroundAnchoredRoaming => UsesRoamingFlight && !flightHeightRelativeToPlayer;
    private bool UsesSmoothRoaming => UsesRoamingFlight && flightSteeringAcceleration > 0f;
    private bool UsesFlightCatchUp => UsesSmoothRoaming && flightCatchUpSpeed > 0f;

    public EnemyMoveMode MoveMode => moveMode;
    private bool UsesMeleeFlanking => groundMeleeFlanking && moveMode == EnemyMoveMode.Ground
        && enemy != null && !enemy.isEliteOrBoss && enemy.CombatRole == EnemyCombatRole.InterruptibleMelee;
    public bool UsesSoftGroundSeparation => softGroundSeparation && UsesMeleeFlanking;
    public Vector2 AttackSlotOffset => attackSlotOffset;
    public bool UsesFixedHeightFlight => allowNonGroundMove && moveMode == EnemyMoveMode.Fly && fixedHeightFlight;
    public float SpawnFlightHeight => flightHeight;
    public bool FlightInitialized => flightInitialized;
    public float FlightWorldY => flightWorldY;
    public bool UsesRoamingFlight => UsesFixedHeightFlight && roamingFlight;
    public bool UsesFlightAttackDescent => UsesGroundAnchoredRoaming && flightAttackHeight > 0f;
    public bool FlightCombatReady => !UsesRoamingFlight || FlightPlayerReady();
    public bool CanStartFlightAttack => !UsesRoamingFlight || (enemy != null && enemy.IsGameplayActive
        && enemy.CurrentReactionState == EnemyReactionState.None && flightInitialized
        && (!UsesFlightCatchUp || flightSteeringVelocity.sqrMagnitude <= .01f)
        && flightBeat == FlightBeat.Ready && FlightPlayerReady());
    public bool HoldsFlightPlaneForReaction => UsesFixedHeightFlight && !roamingFlight && flightInitialized && !flightReactionAllowsVertical;

    public bool TryGetFlightTravelFacing(out float directionX)
    {
        directionX = flightTravelVelocityX;
        return UsesRoamingFlight && faceTargetOnMove && flightBeat == FlightBeat.Moving
            && (!UsesSmoothRoaming || Mathf.Abs(directionX) >= .15f)
            && enemy != null && enemy.IsGameplayActive && enemy.CurrentReactionState == EnemyReactionState.None
            && (enemy.hitReactionHandler == null || !enemy.hitReactionHandler.OwnsPositionReactionMotion);
    }

    private void Awake()
    {
        Cache();
        if (body != null)
            cachedGravityScale = body.gravityScale;

        if (groundLayers.value == 0)
        {
            EnemyHitReactionHandler handler = GetComponent<EnemyHitReactionHandler>();
            if (handler != null && handler.groundLayers.value != 0)
            {
                groundLayers = handler.groundLayers;
            }
            else
            {
                groundLayers = Physics2D.DefaultRaycastLayers;
                int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
                if (oneWayLayer >= 0)
                    groundLayers |= 1 << oneWayLayer;
            }
        }

        ApplyModeGravity();
    }

    public void SetMoveMode(EnemyMoveMode mode)
    {
        if (!allowNonGroundMove)
            mode = EnemyMoveMode.Ground;

        moveMode = mode;
        Cache();
        ApplyModeGravity();
    }

    private void OnValidate()
    {
        if (!allowNonGroundMove)
            moveMode = EnemyMoveMode.Ground;
    }

    public bool PrepareSpawnFlight()
    {
        Cache();
        ReleaseFlightConstraints();
        flightInitialized = false;
        flightHeightPlayer = null;
        flightPlayerRiseElapsed = 0f;
        flightPlayerRiseUnlocked = false;
        flightGroundPlayer = null;
        flightGroundSettleElapsed = 0f;
        flightSteeringVelocity = Vector2.zero;
        flightRoamSide = 0f;
        flightSideUntil = 0f;
        flightCatchingUp = false;
        if (!UsesFixedHeightFlight)
            return false;

        Collider2D collider = ResolveBodyCollider();
        if (collider == null)
            return false;

        Physics2D.SyncTransforms();
        Vector3 current = BodyPosition();
        Vector2 origin = new Vector2(collider.bounds.center.x, Mathf.Max(current.y, collider.bounds.min.y) + 0.1f);
        bool followsPlayer = UsesRoamingFlight && flightHeightRelativeToPlayer
            && TryGetFlightPatrolPlayer(out _);
        float groundY;
        if (UsesGroundAnchoredRoaming)
        {
            if (!TryResolveFlightSpawn(current, collider, out Vector2 placement, out groundY))
            {
                Debug.LogWarning($"{name}: No clear supported flight spawn near the authored point.", this);
                return false;
            }
            current.x = placement.x;
            current.y = placement.y;
        }
        else if (!TryFindFlightGround(origin, flightGroundSearchDistance, out groundY) && !followsPlayer)
        {
            Debug.LogWarning($"{name}: Fixed-height flight needs supporting ground below its spawn point.", this);
            return false;
        }

        flightGroundY = groundY;
        if (followsPlayer) UpdatePlayerFlightHeightReference(flightPatrolPlayer.transform, 0f);
        float feetY = followsPlayer
            ? ResolvePlayerFlightFeetY(flightPatrolPlayer.transform, flightHeight)
            : groundY + flightHeight;
        flightWorldY = UsesGroundAnchoredRoaming ? current.y : feetY - (collider.bounds.min.y - current.y);
        flightInitialized = true;
        flightPatrolAnchorX = current.x;
        flightPatrolSign = EnemyFacingUtil.GetFacingSign(cachedTransform);
        flightBeat = FlightBeat.Resting;
        flightTravelVelocityX = 0f;
        flightBeatTime = Random.Range(0.2f, 0.6f);
        current.y = flightWorldY;
        cachedTransform.position = current;
        if (body != null)
        {
            body.position = current;
            body.linearVelocity = Vector2.zero;
            flightOriginalConstraints = body.constraints;
            flightOwnsConstraints = true;
            if (!roamingFlight) body.constraints |= RigidbodyConstraints2D.FreezePositionY;
            body.gravityScale = 0f;
        }
        Physics2D.SyncTransforms();
        return true;
    }

    private bool TryResolveFlightSpawn(Vector2 authored, Collider2D collider, out Vector2 placement, out float groundY)
    {
        placement = authored;
        groundY = 0f;
        Vector2 centerOffset = (Vector2)collider.bounds.center - authored;
        float footOffset = collider.bounds.min.y - authored.y;
        const float searchStep = 0.5f;
        int steps = Mathf.CeilToInt(flightSpawnSearchRadius / searchStep);
        for (int attempt = 0; attempt <= steps * 2; attempt++)
        {
            float offset = attempt == 0 ? 0f
                : Mathf.Min(((attempt + 1) / 2) * searchStep, flightSpawnSearchRadius) * (attempt % 2 == 1 ? 1f : -1f);
            float x = authored.x + offset;
            Vector2 probe = new Vector2(x + centerOffset.x, Mathf.Max(authored.y, collider.bounds.min.y) + .1f);
            if (!TryFindFlightGround(probe, flightGroundSearchDistance, out float floor)) continue;
            // Try the intended height first, then lower clearances for low ceilings.
            for (float clearance = Mathf.Max(.1f, flightHeight); clearance >= .1f; clearance -= .25f)
            {
                Vector2 candidate = new Vector2(x, floor + clearance - footOffset);
                if (!IsFlightPositionClear(candidate) || !IsFlightSpawnLiftClear(candidate, floor)) continue;
                placement = candidate;
                groundY = floor;
                return true;
            }
        }
        return false;
    }

    private bool IsFlightSpawnLiftClear(Vector2 destination, float floor)
    {
        Collider2D collider = ResolveBodyCollider();
        if (collider == null) return false;
        Bounds bounds = collider.bounds;
        Vector2 current = BodyPosition();
        float footOffset = bounds.min.y - current.y;
        Vector2 start = new Vector2(destination.x, floor + .04f - footOffset);
        float distance = destination.y - start.y;
        if (distance <= 0f) return true;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(collider.gameObject.layer));
        filter.useTriggers = false;
        // A free endpoint above a roof is not connected to the authored ground below it.
        int count = Physics2D.BoxCast(start + (Vector2)bounds.center - current,
            (Vector2)bounds.size + Vector2.one * .04f, 0f, Vector2.up, filter, flightWallHits, distance + .02f);
        if (count == flightWallHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = flightWallHits[i];
            if (IsActorCollider(hit.collider, collider) || Physics2D.GetIgnoreCollision(collider, hit.collider)) continue;
            if (hit.normal.y <= .1f) return false;
        }
        return true;
    }

    private bool IsFlightPositionClear(Vector2 position)
    {
        Collider2D collider = ResolveBodyCollider();
        if (collider == null) return false;
        Bounds bounds = collider.bounds;
        Vector2 center = position + (Vector2)bounds.center - (Vector2)BodyPosition();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(collider.gameObject.layer));
        filter.useTriggers = false;
        int count = Physics2D.OverlapBox(center, (Vector2)bounds.size + Vector2.one * .04f, 0f, filter, flightOverlapHits);
        if (count == flightOverlapHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = flightOverlapHits[i];
            if (!IsActorCollider(hit, collider) && !Physics2D.GetIgnoreCollision(collider, hit)) return false;
        }
        return true;
    }

    public void ReleaseFlightConstraints()
    {
        if (body != null && flightOwnsConstraints)
        {
            body.constraints = flightOriginalConstraints;
            body.gravityScale = cachedGravityScale;
        }
        flightOwnsConstraints = false;
    }

    private void FixedUpdate()
    {
        if (!UsesFixedHeightFlight || !flightInitialized || body == null)
            return;
        if (enemy != null && !enemy.IsAlive)
        {
            ReleaseFlightConstraints();
            return;
        }
        if (enemy != null && EnemyController.IsRoomTraversalBlockingCombat
            && enemy.CurrentReactionState == EnemyReactionState.None)
        {
            body.linearVelocity = new Vector2(0f, roamingFlight ? 0f : body.linearVelocity.y);
            flightSteeringVelocity = Vector2.zero;
            flightCatchingUp = false;
            return;
        }
        if (UsesRoamingFlight)
        {
            TickRoamingFlight(PhysicsStep());
            return;
        }
        // Do not overwrite a MovePosition scheduled by the hit-reaction motor.
        if (enemy != null && enemy.hitReactionHandler != null && enemy.hitReactionHandler.OwnsPositionReactionMotion)
            return;
        if (enemy != null && enemy.CurrentReactionState != EnemyReactionState.None && flightReactionAllowsVertical)
        {
            // Reaction/grab ownership must finish before locomotion resumes its flight plane.
            ReleaseFlightConstraints();
            return;
        }
        if (enemy == null || enemy.CurrentReactionState == EnemyReactionState.None)
            flightReactionAllowsVertical = false;
        if (!flightOwnsConstraints)
        {
            flightOriginalConstraints = body.constraints;
            flightOwnsConstraints = true;
        }
        body.constraints |= RigidbodyConstraints2D.FreezePositionY;
        body.gravityScale = 0f;
        Vector2 position = body.position;
        position.y = flightWorldY;
        body.position = position;
        float vx = body.linearVelocity.x;
        if (TrySampleFlightPatrol(out float patrolVx))
        {
            if (Mathf.Abs(patrolVx) > 0.001f && !HasFlightSupport(patrolVx))
            {
                flightPatrolSign = -Mathf.Sign(patrolVx);
                patrolVx = flightPatrolSign * flightPatrolSpeed;
                if (!HasFlightSupport(patrolVx))
                    patrolVx = 0f;
            }

            vx = patrolVx;
        }
        else if (!HasFlightSupport(0f) || (Mathf.Abs(vx) > 0.001f && !HasFlightSupport(vx)))
        {
            vx = 0f;
        }

        body.linearVelocity = new Vector2(vx, 0f);
    }

    private void OnDisable()
    {
        Collider2D self = ResolveBodyCollider();
        if (self != null)
            foreach (var other in meleePassThroughBodies)
                if (other != null) Physics2D.IgnoreCollision(self, other, false);
        meleePassThroughBodies.Clear();
        meleeFlankTarget = null;
        meleeFlankSide = 0f;
        meleeFlankCrossing = false;
        softWalkingUntil = 0f;
        if (UsesSmoothRoaming && body != null && enemy.CurrentReactionState == EnemyReactionState.None
            && (enemy.hitReactionHandler == null || !enemy.hitReactionHandler.OwnsPositionReactionMotion))
            body.linearVelocity = Vector2.zero;
        flightSteeringVelocity = Vector2.zero;
        flightCatchingUp = false;
        ReleaseFlightConstraints();
    }

    public void ConstrainFlightReaction(ref HitReactionData data)
    {
        if (!UsesFixedHeightFlight || !flightInitialized)
            return;
        if (UsesRoamingFlight)
        {
            flightReactionAllowsVertical = true;
            ReleaseFlightConstraints();
            return;
        }
        // Grabs retain socket ownership. Ordinary hits keep the authored horizontal flight plane
        // and cannot enter an airborne reaction that waits forever for a grounded landing.
        flightReactionAllowsVertical = data.reactionKind == EnemyReactionKind.Grab;
        if (flightReactionAllowsVertical)
        {
            ReleaseFlightConstraints();
            return;
        }
        if (data.reactionKind == EnemyReactionKind.Knockup || data.isKnockdown || data.reactionVelocity.y > 0f)
            data.reactionKind = EnemyReactionKind.Knockback;
        data.launchSpeed = 0f;
        data.reactionVelocity.y = 0f;
        data.isKnockdown = false;
        data.knockupFrames = 0;
        data.knockfallFrames = 0;
        data.canAirJuggle = false;
        data.landingBehavior = EnemyLandingBehavior.Recover;
        if (data.hasSnapWorldPosition)
            data.snapWorldPosition.y = flightWorldY;
        else
        {
            data.useSnapToAnchor = false;
            data.snapToAttacker = false;
        }
    }

    public Vector3 ResolveAttackSlot(Transform target)
    {
        Cache();
        Vector3 self = BodyPosition();
        if (target == null)
            return self;

        Vector3 targetPos = target.position;
        float dx = self.x - targetPos.x;
        float side = Mathf.Sign(dx);
        if (Mathf.Abs(dx) < 0.15f)
            side = EnemyFacingUtil.GetFacingSign(cachedTransform);

        float x = targetPos.x + side * (Mathf.Abs(attackSlotOffset.x) + ResolveCrowdSlotOffset(target, side));
        float y = UsesFixedHeightFlight ? flightWorldY
            : moveMode == EnemyMoveMode.Ground ? self.y : targetPos.y + attackSlotOffset.y;
        return new Vector3(x, y, self.z);
    }

    public bool IsInAttackSlot(Transform target, float rangeOverride = -1f)
    {
        if (target == null)
            return false;

        Cache();
        float threshold = rangeOverride > 0f ? rangeOverride : arrivalThreshold;
        Vector3 slot = ResolveAttackSlot(target);
        if (moveMode == EnemyMoveMode.Ground)
            return EnemyFacingUtil.HorizontalDistance(BodyPosition(), slot) <= threshold;

        return EnemyFacingUtil.PlanarDistance(BodyPosition(), slot) <= threshold;
    }

    public bool IsInAttackRangeOfTarget(Transform target, float attackRange)
    {
        if (target == null)
            return false;

        Cache();
        if (moveMode != EnemyMoveMode.Ground)
            return IsInAttackSlot(target, attackRange);

        if (!IsOnSamePlatform(target))
            return false;

        if (UsesMeleeFlanking)
        {
            float side = ResolveMeleeFlankSide(target, enemy.ResolveHoldRange(attackRange));
            if (IsCrossingToMeleeFlank() || (BodyPosition().x - target.position.x) * side <= 0f) return false;
        }

        return EnemyFacingUtil.HorizontalDistance(BodyPosition(), target.position) <= attackRange;
    }

    public bool IsOnSamePlatform(Transform target)
    {
        if (moveMode != EnemyMoveMode.Ground)
            return true;
        if (target == null)
            return false;

        Cache();
        float selfFoot = FootY(ResolveBodyCollider(), BodyPosition().y);
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        float targetFoot = FootY(targetCollider, target.position.y);
        return Mathf.Abs(targetFoot - selfFoot) <= samePlatformMaxY;
    }

    public void WarpToAttackSlot(Transform target)
    {
        if (target == null)
            return;

        Cache();
        Vector3 slot = ResolveAttackSlot(target);
        Teleport(slot);
        if (faceTargetOnMove)
            EnemyFacingUtil.FaceHorizontal(cachedTransform, target.position.x - slot.x);
    }

    public bool MoveTowardsAttackSlot(Transform target, float speed, float deltaTime)
    {
        if (target == null || speed <= 0f)
            return false;

        Cache();
        Vector3 slot = ResolveAttackSlot(target);
        Vector3 current = BodyPosition();

        if (moveMode == EnemyMoveMode.Ground)
        {
            DriveGroundX(slot.x, speed);
            return EnemyFacingUtil.HorizontalDistance(BodyPosition(), slot) <= arrivalThreshold;
        }

        DriveFly(slot, speed);
        return EnemyFacingUtil.PlanarDistance(BodyPosition(), slot) <= arrivalThreshold;
    }

    /// <summary>
    /// Walk to this body's stand-off slot. Chase is done when the body is on that slot, not when the
    /// player merely falls inside attack range. Ranged will close from far away but will not back up
    /// once the player has walked inside the hold band.
    /// </summary>
    public bool MoveTowardsTargetRange(Transform target, float attackRange, float speed, float deltaTime)
    {
        if (target == null)
            return false;

        Cache();
        if (UsesRoamingFlight) return IsAtHoldSlot(target, attackRange);
        if (!TryResolveHoldPoint(target, attackRange, out Vector3 holdPoint, out float holdGap, out float currentGap))
            return false;

        // Jumping puts the player on another platform without changing X. Stay put so melee does not
        // stroll to a new slot while they are in the air; striking still requires same-platform.
        if (moveMode == EnemyMoveMode.Ground
            && !IsOnSamePlatform(target)
            && currentGap <= Mathf.Max(0.1f, attackRange)
            && !(UsesMeleeFlanking && IsCrossingToMeleeFlank()))
        {
            if (!IsUnstacking)
                StopHorizontal();
            return false;
        }

        bool rangedHold = enemy != null && enemy.UsesRangedAttack;
        if (rangedHold && currentGap <= holdGap + arrivalThreshold)
        {
            if (UsesFixedHeightFlight)
                return false;

            if (moveMode == EnemyMoveMode.Fly)
            {
                float yError = Mathf.Abs(BodyPosition().y - holdPoint.y);
                if (yError > arrivalThreshold)
                {
                    Vector3 hover = BodyPosition();
                    hover.y = holdPoint.y;
                    DriveFly(hover, Mathf.Max(0f, speed));
                    return false;
                }

                if (!IsUnstacking)
                    StopMovement();
                return true;
            }

            if (!IsUnstacking)
                StopHorizontal();
            return true;
        }

        if (moveMode == EnemyMoveMode.Warp)
        {
            Teleport(holdPoint);
            return true;
        }

        if (moveMode == EnemyMoveMode.Ground)
            DriveGroundX(holdPoint.x, Mathf.Max(0f, speed));
        else
            DriveFly(holdPoint, Mathf.Max(0f, speed));

        return EnemyFacingUtil.HorizontalDistance(BodyPosition(), holdPoint) <= arrivalThreshold;
    }

    public bool IsAtHoldSlot(Transform target, float attackRange)
    {
        Cache();
        if (UsesRoamingFlight)
            return target != null && CanStartFlightAttack
                && EnemyFacingUtil.HorizontalDistance(BodyPosition(), target.position) <= attackRange;
        if (enemy != null && enemy.ShieldGuard != null && enemy.ShieldGuard.CanEngage(target))
            return true;
        if (!TryResolveHoldPoint(target, attackRange, out Vector3 holdPoint, out float holdGap, out float currentGap))
            return false;

        if (moveMode == EnemyMoveMode.Ground && !IsOnSamePlatform(target))
            return false;

        bool rangedHold = enemy != null && enemy.UsesRangedAttack;
        if (rangedHold)
        {
            if (currentGap > holdGap + arrivalThreshold)
                return false;

            if (moveMode != EnemyMoveMode.Fly)
                return true;

            return Mathf.Abs(BodyPosition().y - holdPoint.y) <= arrivalThreshold;
        }

        return EnemyFacingUtil.HorizontalDistance(BodyPosition(), holdPoint) <= arrivalThreshold;
    }

    private bool TryResolveHoldPoint(
        Transform target,
        float attackRange,
        out Vector3 holdPoint,
        out float holdGap,
        out float currentGap)
    {
        holdPoint = default;
        holdGap = 0f;
        currentGap = 0f;
        if (target == null)
            return false;

        Vector3 current = BodyPosition();
        Vector3 targetPos = target.position;
        currentGap = EnemyFacingUtil.HorizontalDistance(current, targetPos);

        float side = Mathf.Approximately(current.x - targetPos.x, 0f)
            ? -EnemyFacingUtil.GetFacingSign(target)
            : Mathf.Sign(current.x - targetPos.x);

        float range = Mathf.Max(0.1f, attackRange);
        holdGap = enemy != null ? enemy.ResolveHoldRange(range) : range * 0.85f;
        if (UsesMeleeFlanking) side = ResolveMeleeFlankSide(target, holdGap);
        holdGap += ResolveCrowdSlotOffset(target, side);

        float holdX = targetPos.x + side * holdGap;
        holdPoint = UsesFixedHeightFlight
            ? new Vector3(holdX, flightWorldY, current.z)
            : moveMode == EnemyMoveMode.Ground
            ? new Vector3(holdX, current.y, current.z)
            : new Vector3(holdX, targetPos.y + attackSlotOffset.y, current.z);
        return true;
    }

    /// <summary>
    /// Fly in the plane toward a world point. Requires Allow Non Ground Move.
    /// </summary>
    public bool FlyTowards(Vector3 worldPoint, float speed)
    {
        Cache();
        if (!allowNonGroundMove || speed <= 0f)
            return false;

        if (moveMode != EnemyMoveMode.Fly)
            SetMoveMode(EnemyMoveMode.Fly);

        worldPoint.z = BodyPosition().z;
        DriveFly(worldPoint, speed);
        return EnemyFacingUtil.PlanarDistance(BodyPosition(), worldPoint) <= arrivalThreshold;
    }

    public void StopMovement()
    {
        Cache();
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    public void StopHorizontal()
    {
        Cache();
        if (!CanDriveMovement() || IsUnstacking)
            return;

        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    public void SetHorizontalSpeed(float signedSpeed)
    {
        Cache();
        if (moveMode == EnemyMoveMode.Ground && enemy != null && enemy.Kit != null)
            signedSpeed *= enemy.Kit.WalkingSpeedMultiplier;
        if (!CanDriveMovement())
            return;

        if (UsesFixedHeightFlight && !HasFlightSupport(signedSpeed))
            signedSpeed = 0f;

        FaceWalkDirection(signedSpeed);
        if (body != null)
        {
            body.linearVelocity = new Vector2(signedSpeed, UsesFixedHeightFlight ? 0f : body.linearVelocity.y);
            return;
        }

        cachedTransform.position += new Vector3(signedSpeed * Time.deltaTime, 0f, 0f);
    }

    /// <summary>
    /// Walk on X toward a world point. Used by Patrol so idle/patrol do not fight the Dynamic body.
    /// </summary>
    public bool MoveTowardsX(float targetX, float speed)
    {
        Cache();
        if (IsUnstacking)
            return false;

        Vector3 current = BodyPosition();
        EnemyFacingUtil.FaceHorizontal(cachedTransform, targetX - current.x);

        if (moveMode == EnemyMoveMode.Fly)
        {
            Vector3 dest = new Vector3(targetX, current.y, current.z);
            DriveFly(dest, speed);
            return EnemyFacingUtil.HorizontalDistance(BodyPosition(), dest) <= arrivalThreshold;
        }

        float dir = Mathf.Sign(targetX - current.x);
        if (!Mathf.Approximately(dir, 0f) && !HasGroundInDirection(dir))
        {
            StopHorizontal();
            return true;
        }

        DriveGroundX(targetX, speed);
        return EnemyFacingUtil.HorizontalDistance(BodyPosition(), new Vector3(targetX, current.y, current.z)) <= arrivalThreshold;
    }

    public bool CanWalkTo(Transform target)
    {
        if (moveMode != EnemyMoveMode.Ground)
            return true;
        if (target == null)
            return false;

        Cache();
        float dx = target.position.x - BodyPosition().x;
        if (Mathf.Abs(dx) <= arrivalThreshold)
            return true;

        return HasGroundInDirection(dx);
    }

    public void BeginPlatformPatrol(float preferredDirectionX)
    {
        platformPatrolSign = Mathf.Sign(preferredDirectionX);
        if (Mathf.Approximately(platformPatrolSign, 0f))
            platformPatrolSign = 1f;
    }

    private void LateUpdate()
    {
        SyncMeleePassThroughBodies();
        UnstackIfOverlapping();
    }

    /// <summary>
    /// If another living enemy is overlapping, walk the opposite way.
    /// Returns true when a split was applied.
    /// </summary>
    public bool UnstackIfOverlapping()
    {
        return UnstackIfOverlapping(Time.deltaTime);
    }

    public bool UnstackIfOverlapping(float deltaTime)
    {
        if (UsesSoftGroundSeparation)
        {
            overlapElapsed = 0f;
            unstackUntil = 0f;
            if (!CanDriveMovement() || enemy.FacingLocked)
            {
                softWalkingUntil = 0f;
                return false;
            }
            if (TryGetSoftGroundSeparationStep(PhysicsStep(), out float correction))
            {
                // Chase yields while this brief correction owns walking. No velocity accumulates.
                softWalkingUntil = Time.time + .06f;
                body.linearVelocity = new Vector2(correction / PhysicsStep(), body.linearVelocity.y);
                return true;
            }
            if (Time.time < softWalkingUntil && body != null)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            softWalkingUntil = 0f;
            return false;
        }
        if (UsesMeleeFlanking && enemy.FacingLocked)
        {
            overlapElapsed = 0f;
            unstackUntil = 0f;
            return false;
        }
        if (enemy != null && enemy.ShieldGuard != null && enemy.ShieldGuard.IsEngaged)
        {
            overlapElapsed = 0f;
            unstackUntil = 0f;
            return false;
        }
        if (enemy != null && enemy.Kit != null && enemy.Kit.BodyCharge && enemy.FacingLocked)
        {
            overlapElapsed = 0f;
            unstackUntil = 0f;
            return false;
        }
        if (!CanDriveMovement())
        {
            overlapElapsed = 0f;
            return false;
        }

        bool overlapping = TryResolveUnstackSign(out float sign);
        if (IsUnstacking)
        {
            ApplyUnstackWalk(unstackSign);
            return true;
        }

        if (!overlapping)
        {
            overlapElapsed = 0f;
            return false;
        }

        overlapElapsed += Mathf.Max(0f, deltaTime);
        if (overlapElapsed < overlapConfirmTime)
            return false;

        if (!HasGroundInDirection(sign))
        {
            sign = -sign;
            if (!HasGroundInDirection(sign))
            {
                overlapElapsed = 0f;
                return false;
            }
        }

        unstackSign = sign;
        unstackUntil = Time.time + unstackCommitTime;
        overlapElapsed = 0f;
        ApplyUnstackWalk(unstackSign);
        return true;
    }

    public bool IsUnstacking => Time.time < unstackUntil || Time.time < softWalkingUntil;

    /// <summary>Plans a terrain-safe correction; the current motion owner applies it.</summary>
    public bool TryGetSoftGroundSeparationStep(float deltaTime, out float correction)
    {
        correction = 0f;
        Cache();
        if (deltaTime <= 0f || !CanJoinSoftGroundPack()) return false;
        Collider2D self = ResolveBodyCollider();
        float feet = self.bounds.min.y;
        RoomEnemyRegistry.FillLiving(livingEnemies);
        softSeparationPack.Clear();
        softSeparationPack.Add(this);
        // Include connected neighbours so a pile of five receives five distinct, stable slots.
        for (int p = 0; p < softSeparationPack.Count; p++)
        {
            EnemyLocomotion member = softSeparationPack[p];
            float memberX = member.ResolveBodyCollider().bounds.center.x;
            foreach (EnemyController other in livingEnemies)
            {
                if (other == null || other == enemy) continue;
                var movement = other.GetComponent<EnemyLocomotion>();
                if (movement == null || softSeparationPack.Contains(movement) || !movement.CanJoinSoftGroundPack()) continue;
                Collider2D peer = movement.ResolveBodyCollider();
                if (Mathf.Abs(peer.bounds.min.y - feet) > .15f
                    || Mathf.Abs(peer.bounds.center.x - memberX) > softSeparationSpacing + .08f
                    || IsPassingOppositeFlank(member, movement)) continue;
                softSeparationPack.Add(movement);
            }
        }
        if (softSeparationPack.Count < 2) return false;
        softSeparationPack.Sort((a, b) => {
            int order = a.ResolveBodyCollider().bounds.center.x.CompareTo(b.ResolveBodyCollider().bounds.center.x);
            return order != 0 ? order : a.enemy.GetInstanceID().CompareTo(b.enemy.GetInstanceID());
        });
        float left = softSeparationPack[0].ResolveBodyCollider().bounds.center.x;
        float right = softSeparationPack[softSeparationPack.Count - 1].ResolveBodyCollider().bounds.center.x;
        float span = Mathf.Max(right - left,
            Mathf.Min(softSeparationSpacing * (softSeparationPack.Count - 1), softSeparationHalfWidth * 2f));
        float slot = softSeparationPack.IndexOf(this) / (float)(softSeparationPack.Count - 1) - .5f;
        float desired = (left + right) * .5f + slot * span - self.bounds.center.x;
        if (Mathf.Abs(desired) <= .015f || !HasGroundInDirection(desired)) return false;
        float step = Mathf.Clamp(desired, -softSeparationSpeed * deltaTime, softSeparationSpeed * deltaTime);
        correction = ClipFlightTravel(Vector2.right * step).x;
        return Mathf.Abs(correction) > .0001f;
    }

    private bool CanJoinSoftGroundPack()
    {
        Cache();
        if (!isActiveAndEnabled || !UsesSoftGroundSeparation || !enemy.IsCombatReady || body == null
            || body.linearVelocity.y > .1f) return false;
        EnemyReactionState state = enemy.CurrentReactionState;
        if (state != EnemyReactionState.None && state != EnemyReactionState.Recovering && state != EnemyReactionState.Knockdown)
            return false;
        if (state == EnemyReactionState.None && enemy.FacingLocked) return false;
        Collider2D collider = ResolveBodyCollider();
        return collider != null && collider.enabled && enemy.hitReactionHandler != null
            && enemy.hitReactionHandler.AllowsGroundSeparation && enemy.hitReactionHandler.IsGroundedForCombat;
    }

    private static bool IsPassingOppositeFlank(EnemyLocomotion a, EnemyLocomotion b)
    {
        return a.enemy.CurrentReactionState == EnemyReactionState.None && b.enemy.CurrentReactionState == EnemyReactionState.None
            && a.meleeFlankTarget != null && a.meleeFlankTarget == b.meleeFlankTarget
            && a.meleeFlankSide != 0f && b.meleeFlankSide != 0f && a.meleeFlankSide != b.meleeFlankSide
            && (a.IsCrossingToMeleeFlank() || b.IsCrossingToMeleeFlank());
    }

    private void ApplyUnstackWalk(float sign)
    {
        SetHorizontalSpeed(sign * unstackSpeed);
    }

    public void PatrolCurrentPlatform(float speed)
    {
        Cache();
        if (moveMode != EnemyMoveMode.Ground || speed <= 0f)
        {
            StopHorizontal();
            return;
        }

        if (!HasGroundInDirection(platformPatrolSign))
            platformPatrolSign = -platformPatrolSign;

        if (!HasGroundInDirection(platformPatrolSign))
        {
            StopHorizontal();
            return;
        }

        EnemyFacingUtil.FaceHorizontal(cachedTransform, platformPatrolSign);
        SetHorizontalSpeed(platformPatrolSign * speed);
    }

    private Vector3 BodyPosition()
    {
        if (body != null)
        {
            Vector2 p = body.position;
            return new Vector3(p.x, p.y, cachedTransform.position.z);
        }

        return cachedTransform.position;
    }

    private void DriveGroundX(float targetX, float speed)
    {
        if (enemy != null && enemy.Kit != null)
            speed *= enemy.Kit.WalkingSpeedMultiplier;
        if (!CanDriveMovement() || IsUnstacking)
            return;

        Vector3 current = BodyPosition();
        float dx = targetX - current.x;
        if (Mathf.Abs(dx) <= arrivalThreshold)
        {
            StopHorizontal();
            return;
        }

        float dir = Mathf.Sign(dx);
        FaceWalkDirection(dir);
        if (!HasGroundInDirection(dir))
        {
            StopHorizontal();
            return;
        }

        if (body == null)
        {
            float newX = Mathf.MoveTowards(current.x, targetX, speed * Time.deltaTime);
            cachedTransform.position = new Vector3(newX, current.y, current.z);
            return;
        }

        float step = speed * PhysicsStep();
        float vx = Mathf.Abs(dx) <= step ? dx / PhysicsStep() : dir * speed;
        body.linearVelocity = new Vector2(vx, body.linearVelocity.y);
    }

    private void DriveFly(Vector3 target, float speed)
    {
        if (!CanDriveMovement() || IsUnstacking)
            return;

        ApplyModeGravity();
        Vector3 current = BodyPosition();
        if (UsesFixedHeightFlight)
        {
            target.y = flightWorldY;
            float dx = target.x - current.x;
            float vx = Mathf.Clamp(dx / PhysicsStep(), -speed, speed);
            if (!flightInitialized || Mathf.Abs(dx) <= arrivalThreshold || !HasFlightSupport(vx))
            {
                if (body != null)
                    body.linearVelocity = Vector2.zero;
                return;
            }
            FaceWalkDirection(dx);
            if (body != null)
            {
                body.linearVelocity = new Vector2(vx, 0f);
            }
            else
                cachedTransform.position = new Vector3(Mathf.MoveTowards(current.x, target.x, speed * Time.deltaTime), flightWorldY, current.z);
            return;
        }
        Vector3 delta = target - current;
        delta.z = 0f;
        float dist = delta.magnitude;
        if (dist <= arrivalThreshold)
        {
            if (body != null)
                body.linearVelocity = Vector2.zero;
            return;
        }

        FaceWalkDirection(delta.x);
        if (body == null)
        {
            cachedTransform.position = Vector3.MoveTowards(current, target, speed * Time.deltaTime);
            return;
        }

        float step = speed * PhysicsStep();
        Vector2 vel = dist <= step
            ? (Vector2)(delta / PhysicsStep())
            : (Vector2)(delta / dist * speed);
        body.linearVelocity = vel;
    }

    private void FaceWalkDirection(float directionX)
    {
        if (!faceTargetOnMove)
            return;

        EnemyFacingUtil.FaceHorizontal(cachedTransform, directionX);
    }

    private void Teleport(Vector3 worldPosition)
    {
        if (!CanDriveMovement())
            return;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = worldPosition;
        }

        cachedTransform.position = worldPosition;
    }

    private void ApplyModeGravity()
    {
        if (body == null)
            return;

        body.gravityScale = moveMode == EnemyMoveMode.Fly ? 0f : cachedGravityScale;
    }

    private static float PhysicsStep()
    {
        return Mathf.Max(Time.fixedDeltaTime, 0.0001f);
    }

    private void Cache()
    {
        if (cachedTransform == null)
            cachedTransform = transform;

        if (enemy == null)
            enemy = GetComponent<EnemyController>();

        if (body == null)
            body = GetComponent<Rigidbody2D>();
    }

    private Collider2D ResolveBodyCollider()
    {
        if (enemy != null && enemy.bodyCollider != null)
            return enemy.bodyCollider;
        return GetComponent<Collider2D>();
    }

    private static float FootY(Collider2D collider, float fallbackY)
    {
        return collider != null ? collider.bounds.min.y : fallbackY;
    }

    private bool HasGroundInDirection(float dirSign)
    {
        if (UsesFixedHeightFlight)
            return HasFlightSupport(dirSign);
        if (Mathf.Approximately(dirSign, 0f))
            return true;

        Collider2D col = ResolveBodyCollider();
        if (col == null)
            return true;

        float dir = Mathf.Sign(dirSign);
        Vector2 origin = new Vector2(
            col.bounds.center.x + dir * (col.bounds.extents.x + ledgeProbeDistance),
            col.bounds.min.y + 0.02f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = groundLayers.value != 0;
        if (filter.useLayerMask)
            filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;

        int hitCount = Physics2D.Raycast(origin, Vector2.down, filter, groundHits, groundProbeDistance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null || IsActorCollider(hit.collider, col))
                continue;
            if (hit.normal.y >= 0.25f)
                return true;
        }

        return false;
    }

    private bool FlightPlayerReady()
    {
        if (!TryGetFlightPatrolPlayer(out _)) return false;
        PlayerStateMachine state = flightPatrolPlayer.StateMachine;
        return !flightPatrolPlayer.IsDead && state != null && state.CurrentState != null
            && state.CurrentStateId != PlayerStateId.Intro
            && state.CurrentStateId != PlayerStateId.Exit
            && state.CurrentStateId != PlayerStateId.Dead;
    }

    public bool BeginFlightAttack()
    {
        if (!UsesRoamingFlight) return true;
        if (!CanStartFlightAttack) return false;
        flightBeat = FlightBeat.Attacking;
        var collider = ResolveBodyCollider();
        flightAttackDescending = UsesFlightAttackDescent && collider != null;
        if (flightAttackDescending)
            flightAttackBodyY = Mathf.Min(body.position.y,
                body.position.y + flightGroundY + flightAttackHeight - collider.bounds.min.y);
        if (!UsesSmoothRoaming) body.linearVelocity = Vector2.zero;
        return true;
    }

    public void HoldFlightAttackAltitude() => flightAttackDescending = false;

    public void EndFlightAttack(bool interrupted)
    {
        if (!UsesRoamingFlight || flightBeat != FlightBeat.Attacking) return;
        flightAttackDescending = false;
        flightBeat = FlightBeat.Resting;
        flightBeatTime = interrupted ? flightInterruptedRest : flightPostAttackRest;
    }

    private void TickRoamingFlight(float dt)
    {
        if (enemy == null) return;
        if (enemy.CurrentReactionState != EnemyReactionState.None
            || (enemy.hitReactionHandler != null && enemy.hitReactionHandler.OwnsPositionReactionMotion))
        {
            flightBeat = FlightBeat.Resting;
            flightBeatTime = flightInterruptedRest;
            flightPlayerRiseElapsed = 0f;
            flightPlayerRiseUnlocked = false;
            flightGroundSettleElapsed = 0f;
            flightSteeringVelocity = Vector2.zero;
            flightTravelVelocityX = 0f;
            flightCatchingUp = false;
            return;
        }
        body.gravityScale = 0f;
        if (!UsesSmoothRoaming) body.linearVelocity = Vector2.zero;
        if (!enemy.IsGameplayActive || !FlightPlayerReady())
        {
            if (UsesSmoothRoaming)
            {
                flightSteeringVelocity = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                flightCatchingUp = false;
            }
            flightBeat = FlightBeat.Resting;
            flightBeatTime = 0.3f;
            flightGroundSettleElapsed = 0f;
            return;
        }
        if (UsesGroundAnchoredRoaming) UpdateGroundFlightReference(dt);
        if (flightHeightRelativeToPlayer && TryGetFlightPatrolPlayer(out Transform heightPlayer))
            UpdatePlayerFlightHeightReference(heightPlayer, dt);
        if (flightHeightRelativeToPlayer
            && (flightBeat == FlightBeat.Resting || flightBeat == FlightBeat.Ready))
            AdjustPlayerRelativeFlightHeight(dt);
        if (flightBeat == FlightBeat.Resting)
        {
            if (UsesSmoothRoaming) ApplySmoothFlightVelocity(Vector2.zero, flightBrakingAcceleration, dt);
            flightBeatTime -= dt;
            if (flightBeatTime <= 0f && !WaitingForPlayerRise) SelectFlightDestination();
            return;
        }
        if (flightBeat == FlightBeat.Ready)
        {
            if (UsesSmoothRoaming) ApplySmoothFlightVelocity(Vector2.zero, flightBrakingAcceleration, dt);
            flightBeatTime += dt;
            bool targetEscaped = UsesFlightCatchUp && flightBeatTime >= .2f
                && TryGetFlightPatrolPlayer(out Transform escapedPlayer)
                && EnemyFacingUtil.HorizontalDistance(body.position, escapedPlayer.position)
                    > Mathf.Max(flightCatchUpEnterDistance, enemy.ResolveAttackRange(12f)) + 1f;
            if ((flightBeatTime >= 1.5f || targetEscaped) && !WaitingForPlayerRise) SelectFlightDestination();
            return;
        }
        if (flightBeat == FlightBeat.Attacking && flightAttackDescending)
        {
            if (UsesSmoothRoaming)
            {
                flightSteeringVelocity = Vector2.zero;
                body.linearVelocity = Vector2.zero;
            }
            float step = Mathf.MoveTowards(body.position.y, flightAttackBodyY, 12f * dt) - body.position.y;
            Vector2 safe = ClipFlightTravel(Vector2.up * step);
            body.position += safe;
            if (Mathf.Abs(body.position.y - flightAttackBodyY) < .001f || Mathf.Abs(safe.y - step) > .001f)
            {
                flightAttackDescending = false;
                enemy.CommitRangedAim();
            }
            return;
        }
        if (flightBeat == FlightBeat.Attacking && UsesSmoothRoaming)
        {
            ApplySmoothFlightVelocity(Vector2.zero, flightBrakingAcceleration, dt);
            return;
        }
        if (flightBeat != FlightBeat.Moving) return;
        TickFlightTravel(dt);
    }

    private void TickFlightTravel(float dt)
    {
        if (!TryGetFlightPatrolPlayer(out Transform player))
        {
            flightBeat = FlightBeat.Resting;
            flightBeatTime = .3f;
            return;
        }
        Collider2D collider = ResolveBodyCollider();
        if (collider == null) return;
        float footOffset = collider.bounds.min.y - body.position.y;
        // Follow horizontal travel in both modes; jumping only affects player-relative altitude.
        float feetY = flightHeightRelativeToPlayer
            ? ResolvePlayerFlightFeetY(player, flightMoveHeight) : flightGroundY + flightMoveHeight;
        flightMoveEnd = new Vector2(flightHeightRelativeToPlayer && WaitingForPlayerRise
            ? flightMoveEnd.x : player.position.x + flightMovePlayerOffsetX, feetY - footOffset);
        flightBeatTime += dt;
        float t = Mathf.Clamp01(flightBeatTime / Mathf.Max(.1f, flightMoveDuration));
        if (UsesSmoothRoaming)
        {
            float cruiseSpeed = ResolveFlightCruiseSpeed(EnemyFacingUtil.HorizontalDistance(body.position, player.position));
            // A ranged enemy can fight from a clear shot, without first finishing its roaming path.
            // Brake before Ready admits an attack, so the warning and committed aim begin at rest.
            if (UsesFlightCatchUp && enemy.HasClearStraightShot(player, enemy.ResolveAttackRange(12f)))
            {
                flightBeat = FlightBeat.Ready;
                flightBeatTime = 0f;
                flightWorldY = body.position.y;
                ApplySmoothFlightVelocity(Vector2.zero, flightBrakingAcceleration, dt);
                return;
            }
            Vector2 destination = flightMoveEnd + Vector2.up * (Mathf.Sin(t * Mathf.PI) * flightArcHeight);
            Vector2 offset = destination - body.position;
            float stoppingAcceleration = Mathf.Min(flightSteeringAcceleration, flightBrakingAcceleration);
            float speed = Mathf.Min(cruiseSpeed, Mathf.Sqrt(2f * stoppingAcceleration * offset.magnitude));
            bool blocked = ApplySmoothFlightVelocity(offset.normalized * speed, flightSteeringAcceleration, dt);
            if (Mathf.Abs(flightTravelVelocityX) >= .15f) FaceWalkDirection(flightTravelVelocityX);
            if (blocked || (t >= 1f && Vector2.Distance(body.position, flightMoveEnd) <= arrivalThreshold))
            {
                flightBeat = FlightBeat.Ready;
                flightBeatTime = 0f;
                flightWorldY = body.position.y;
            }
            return;
        }
        Vector2 next = Vector2.Lerp(flightMoveStart, flightMoveEnd, Mathf.SmoothStep(0f, 1f, t));
        next.y += Mathf.Sin(t * Mathf.PI) * flightArcHeight;
        // A moving endpoint must not pull the body faster than its authored flight speed.
        next = Vector2.MoveTowards(body.position, next, Mathf.Max(.1f, flightPatrolSpeed) * dt);
        Vector2 desired = next - body.position;
        Vector2 safe = ClipFlightTravel(desired);
        body.position += safe;
        flightTravelVelocityX = safe.x / Mathf.Max(dt, .0001f);
        FaceWalkDirection(flightTravelVelocityX);
        bool arrived = t >= 1f && Vector2.Distance(body.position, flightMoveEnd) <= arrivalThreshold;
        if ((safe - desired).sqrMagnitude > .0001f || arrived)
        {
            flightBeat = FlightBeat.Ready;
            flightBeatTime = 0f;
            flightWorldY = body.position.y;
        }
    }

    private void SelectFlightDestination()
    {
        if (!TryGetFlightPatrolPlayer(out Transform player)) return;
        Collider2D collider = ResolveBodyCollider();
        if (collider == null) return;
        float footOffset = collider.bounds.min.y - body.position.y;
        float side = Random.value < .5f ? -1f : 1f;
        if (UsesSmoothRoaming && flightSideHoldTime > 0f)
        {
            if (flightRoamSide == 0f) flightRoamSide = body.position.x >= player.position.x ? 1f : -1f;
            side = flightRoamSide;
            if (Time.time >= flightSideUntil && flightSideUntil > 0f && Random.value < .25f) side = -side;
        }
        float minimum = Mathf.Max(1f, flightTargetDistance.x);
        float maximum = Mathf.Max(minimum, flightTargetDistance.y);
        // Platform-bound flight rejects gaps; player-relative flight still sweeps room walls.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float candidateSide = UsesSmoothRoaming && flightSideHoldTime > 0f
                ? (attempt < 3 ? side : -side) : side;
            float x = player.position.x + candidateSide * Random.Range(minimum, maximum);
            if (!UsesSmoothRoaming || flightSideHoldTime <= 0f) side = -side;
            float height = Mathf.Max(.2f, flightHeight + Random.Range(-flightHeightVariation, flightHeightVariation));
            float feetY;
            if (flightHeightRelativeToPlayer)
                feetY = ResolvePlayerFlightFeetY(player, height);
            else
            {
                Vector2 probe = new Vector2(x, flightGroundY + flightHeight + flightHeightVariation + .1f);
                if (!TryFindFlightGround(probe, flightGroundSearchDistance, out float groundY)
                    || Mathf.Abs(groundY - flightGroundY) > flightGroundTolerance) continue;
                feetY = groundY + height;
            }
            Vector2 destination = new Vector2(x, feetY - footOffset);
            if (UsesGroundAnchoredRoaming && !IsFlightPositionClear(destination)) continue;
            Vector2 delta = destination - body.position;
            if ((ClipFlightTravel(delta) - delta).sqrMagnitude > .0001f) continue;
            flightMoveStart = body.position;
            flightMoveEnd = destination;
            flightMovePlayerOffsetX = x - player.position.x;
            flightMoveHeight = height;
            flightTravelVelocityX = UsesSmoothRoaming ? flightSteeringVelocity.x : 0f;
            if (UsesSmoothRoaming && flightSideHoldTime > 0f)
            {
                if (candidateSide != flightRoamSide || Time.time >= flightSideUntil)
                    flightSideUntil = Time.time + flightSideHoldTime;
                flightRoamSide = candidateSide;
            }
            flightMoveDuration = Mathf.Max(flightTravelDuration, delta.magnitude / Mathf.Max(.1f, flightPatrolSpeed));
            flightBeatTime = 0f;
            flightBeat = FlightBeat.Moving;
            return;
        }
        // A cramped room can still offer a shot without endlessly searching for a flight point.
        flightBeat = FlightBeat.Ready;
        flightBeatTime = 0f;
    }

    private bool ApplySmoothFlightVelocity(Vector2 desired, float acceleration, float dt)
    {
        flightSteeringVelocity = Vector2.MoveTowards(flightSteeringVelocity, desired, acceleration * dt);
        Vector2 step = flightSteeringVelocity * dt;
        Vector2 safe = ClipFlightTravel(step);
        bool blocked = (safe - step).sqrMagnitude > .0001f;
        flightSteeringVelocity = safe / Mathf.Max(dt, .0001f);
        // Velocity lets physics interpolation render the travel instead of teleporting every fixed step.
        body.linearVelocity = flightSteeringVelocity;
        flightTravelVelocityX = flightSteeringVelocity.x;
        return blocked;
    }

    private float ResolveFlightCruiseSpeed(float targetDistance)
    {
        if (!UsesFlightCatchUp) return flightPatrolSpeed;
        float enter = Mathf.Max(.1f, flightCatchUpEnterDistance);
        float exit = Mathf.Clamp(flightCatchUpExitDistance, 0f, enter - .1f);
        if (flightCatchingUp)
        {
            if (targetDistance <= exit) flightCatchingUp = false;
        }
        else if (targetDistance > enter) flightCatchingUp = true;
        return flightCatchingUp ? Mathf.Max(flightPatrolSpeed, flightCatchUpSpeed) : flightPatrolSpeed;
    }

    private void UpdateGroundFlightReference(float dt)
    {
        if (!TryGetFlightPatrolPlayer(out Transform player)) return;
        Collider2D playerBody = player.GetComponent<Collider2D>();
        if (player != flightGroundPlayer)
        {
            flightGroundPlayer = player;
            flightGroundSettleElapsed = 0f;
        }
        PlayerMovement movement = flightPatrolPlayer.Movement;
        if (movement == null || !movement.IsPhysicallyGrounded() || playerBody == null
            || !playerBody.enabled || playerBody.isTrigger
            || !TryFindFlightGround(new Vector2(playerBody.bounds.center.x, playerBody.bounds.min.y + .1f), .3f, out float floor))
        {
            flightGroundSettleElapsed = 0f;
            return;
        }
        if (flightGroundSettleElapsed == 0f || Mathf.Abs(floor - flightPendingGroundY) > flightGroundTolerance)
        {
            flightPendingGroundY = floor;
            flightGroundSettleElapsed = 0f;
        }
        flightGroundSettleElapsed += Mathf.Max(0f, dt);
        if (flightGroundSettleElapsed >= flightGroundSettleTime)
            flightGroundY = floor;
    }

    private float ResolvePlayerFlightFeetY(Transform player, float height)
    {
        Collider2D playerBody = player.GetComponent<Collider2D>();
        float headY = playerBody != null && playerBody.enabled && !playerBody.isTrigger
            ? playerBody.bounds.max.y : player.position.y;
        float altitude = Mathf.Max(height, headY + .35f - player.position.y);
        float referenceY = flightPlayerRiseDelay > 0f && flightHeightPlayer == player
            ? flightPlayerReferenceY : player.position.y;
        return referenceY + altitude * Mathf.Max(1f, flightHeightMultiplier);
    }

    private void UpdatePlayerFlightHeightReference(Transform player, float dt)
    {
        float y = player.position.y;
        if (flightHeightPlayer != player || flightPlayerRiseDelay <= 0f)
        {
            flightHeightPlayer = player;
            flightPlayerReferenceY = y;
            flightPlayerRiseElapsed = 0f;
            flightPlayerRiseUnlocked = false;
            return;
        }
        // After the grace period, follow the rest of this ascent without repeated pauses.
        if (flightPlayerRiseUnlocked)
        {
            if (y < flightPlayerReferenceY - .01f) flightPlayerRiseUnlocked = false;
            flightPlayerReferenceY = y;
            return;
        }
        if (y <= flightPlayerReferenceY + .1f)
        {
            flightPlayerReferenceY = y;
            flightPlayerRiseElapsed = 0f;
            return;
        }
        flightPlayerRiseElapsed += Mathf.Max(0f, dt);
        if (flightPlayerRiseElapsed < flightPlayerRiseDelay) return;
        flightPlayerReferenceY = y;
        flightPlayerRiseElapsed = 0f;
        flightPlayerRiseUnlocked = true;
    }

    private void AdjustPlayerRelativeFlightHeight(float dt)
    {
        if (!TryGetFlightPatrolPlayer(out Transform player)) return;
        Collider2D collider = ResolveBodyCollider();
        if (collider == null) return;
        float feetY = collider.bounds.min.y;
        // Keep the authored height variation, but leave the spawn plane when the player changes level.
        float minimum = ResolvePlayerFlightFeetY(player, Mathf.Max(.2f, flightHeight - flightHeightVariation));
        float maximum = ResolvePlayerFlightFeetY(player, flightHeight + flightHeightVariation);
        float target = Mathf.Clamp(feetY, minimum, maximum);
        float step = Mathf.MoveTowards(feetY, target, flightPatrolSpeed * dt) - feetY;
        body.position += ClipFlightTravel(Vector2.up * step);
        flightWorldY = body.position.y;
    }

    private Vector2 ClipFlightTravel(Vector2 delta)
    {
        float distance = delta.magnitude;
        Collider2D collider = ResolveBodyCollider();
        if (distance < .0001f || collider == null) return delta;
        Vector2 direction = delta / distance;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(collider.gameObject.layer));
        filter.useTriggers = false;
        int count = collider.Cast(direction, filter, flightWallHits, distance + .02f);
        if (count == flightWallHits.Length) return Vector2.zero;
        float allowed = distance;
        for (int i = 0; i < count; i++)
        {
            var hit = flightWallHits[i];
            if (IsActorCollider(hit.collider, collider) || Physics2D.GetIgnoreCollision(collider, hit.collider)
                || Vector2.Dot(hit.normal, direction) > -.1f) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - .02f));
        }
        return direction * allowed;
    }

    private bool TrySampleFlightPatrol(out float velocityX)
    {
        velocityX = 0f;
        if (enemy == null || !enemy.IsGameplayActive || enemy.CurrentReactionState != EnemyReactionState.None)
            return false;

        if (enemy.IsRangedAiming)
            return true;

        float center = flightPatrolAnchorX;
        if (TryGetFlightPatrolPlayer(out Transform player))
        {
            float range = enemy.ResolveAttackRange(enemy.Kit != null ? enemy.Kit.AttackRange : 12f);
            if (EnemyFacingUtil.HorizontalDistance(body.position, player.position) > range)
                return false;

            center = player.position.x;
        }

        float half = flightPatrolSpan * 0.5f;
        float x = body.position.x;
        if (x >= center + half)
            flightPatrolSign = -1f;
        else if (x <= center - half)
            flightPatrolSign = 1f;

        velocityX = flightPatrolSign * flightPatrolSpeed;
        FaceWalkDirection(velocityX);
        return true;
    }

    private bool TryGetFlightPatrolPlayer(out Transform player)
    {
        player = null;
        if (flightPatrolPlayer == null || !flightPatrolPlayer.isActiveAndEnabled)
            flightPatrolPlayer = FindFirstObjectByType<PlayerCharacter>();

        if (flightPatrolPlayer == null)
            return false;

        player = flightPatrolPlayer.transform;
        return player != null;
    }

    public bool HasFlightSupport(float directionX)
    {
        if (!UsesFixedHeightFlight || !flightInitialized)
            return false;
        Collider2D collider = ResolveBodyCollider();
        if (collider == null)
            return false;
        Bounds bounds = collider.bounds;
        float lookahead = Mathf.Abs(directionX) > 0.001f
            ? Mathf.Sign(directionX) * (bounds.extents.x + ledgeProbeDistance + Mathf.Abs(directionX) * PhysicsStep())
            : 0f;
        Vector2 origin = new Vector2(bounds.center.x + lookahead, bounds.min.y + 0.05f);
        return TryFindFlightGround(origin, flightHeight + flightGroundTolerance + 0.1f, out float groundY)
            && Mathf.Abs(groundY - flightGroundY) <= flightGroundTolerance;
    }

    private bool TryFindFlightGround(Vector2 origin, float distance, out float groundY)
    {
        groundY = 0f;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;
        int count = Physics2D.Raycast(origin, Vector2.down, filter, groundHits, distance);
        if (count == groundHits.Length) return false;
        float nearest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null || hit.distance <= .001f || hit.normal.y < 0.5f
                || IsActorCollider(hit.collider, ResolveBodyCollider()) || hit.collider.OverlapPoint(origin))
                continue;
            if (hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            groundY = hit.point.y;
            found = true;
        }
        return found;
    }

    private bool TryResolveUnstackSign(out float sign)
    {
        sign = 0f;
        Cache();
        if (enemy == null || !enemy.IsAlive)
            return false;

        Vector3 self = BodyPosition();
        RoomEnemyRegistry.FillLiving(livingEnemies);
        EnemyController nearest = null;
        float nearestAbs = overlapRadius;
        for (int i = 0; i < livingEnemies.Count; i++)
        {
            EnemyController other = livingEnemies[i];
            if (other == null || other == enemy || !other.IsAlive)
                continue;
            if (other.GetComponent<EnemyLocomotion>() == null)
                continue;

            var otherLocomotion = other.GetComponent<EnemyLocomotion>();
            if (UsesMeleeFlanking && otherLocomotion.UsesMeleeFlanking
                && (IsCrossingToMeleeFlank() || otherLocomotion.IsCrossingToMeleeFlank()
                    || meleeFlankTarget != null && otherLocomotion.meleeFlankTarget == meleeFlankTarget))
                continue;

            Vector3 otherPos = other.transform.position;
            if (Mathf.Abs(otherPos.y - self.y) > overlapSameY)
                continue;

            float dx = self.x - otherPos.x;
            float abs = Mathf.Abs(dx);
            if (abs >= nearestAbs)
                continue;

            nearest = other;
            nearestAbs = abs;
            sign = abs > 0.04f
                ? Mathf.Sign(dx)
                : (enemy.GetInstanceID() > other.GetInstanceID() ? 1f : -1f);
        }

        return nearest != null;
    }

    /// <summary>
    /// Extra outward distance so a pack queues instead of piling onto one point. Rank comes from how
    /// far each body already stands from the target, so the front slot belongs to whoever actually
    /// got there first and nobody has to walk through a peer to reach its place.
    /// </summary>
    private float ResolveCrowdSlotOffset(Transform target, float side)
    {
        Cache();
        if (enemy == null || target == null)
            return 0f;

        Vector3 self = BodyPosition();
        float targetX = target.position.x;
        float selfGap = Mathf.Abs(self.x - targetX);
        int selfId = enemy.GetInstanceID();

        RoomEnemyRegistry.FillLiving(livingEnemies);
        int ahead = 0;
        for (int i = 0; i < livingEnemies.Count; i++)
        {
            EnemyController other = livingEnemies[i];
            if (other == null || other == enemy || !other.IsAlive)
                continue;
            if (other.GetComponent<EnemyLocomotion>() == null)
                continue;

            Vector3 otherPos = other.transform.position;
            if (Mathf.Abs(otherPos.y - self.y) > samePlatformMaxY)
                continue;
            var otherLocomotion = other.GetComponent<EnemyLocomotion>();
            float otherSide = UsesMeleeFlanking && otherLocomotion.UsesMeleeFlanking
                && otherLocomotion.meleeFlankTarget == target
                ? otherLocomotion.meleeFlankSide : Mathf.Sign(otherPos.x - targetX);
            // Reserve the destination flank even while a peer is crossing through the pack.
            if (!Mathf.Approximately(otherSide, side))
                continue;

            float otherGap = Mathf.Abs(otherPos.x - targetX);
            if (otherGap < selfGap - CrowdRankEpsilon)
            {
                ahead++;
            }
            else if (Mathf.Abs(otherGap - selfGap) <= CrowdRankEpsilon && other.GetInstanceID() < selfId)
            {
                // Two bodies at the same distance would otherwise both claim the front slot and grind.
                ahead++;
            }
        }

        return ahead * crowdSlotSpacing;
    }

    private float ResolveMeleeFlankSide(Transform target, float holdGap)
    {
        float naturalSide = BodyPosition().x >= target.position.x ? 1f : -1f;
        float footY = FootY(ResolveBodyCollider(), BodyPosition().y);
        if (meleeFlankTarget == target && meleeFlankSide != 0f
            && Mathf.Abs(target.position.x - meleeFlankTargetX) < 1f
            && Mathf.Abs(footY - meleeFlankFootY) <= samePlatformMaxY)
            return meleeFlankSide;

        RoomEnemyRegistry.FillLiving(livingEnemies);
        int near = 0, far = 0;
        foreach (var other in livingEnemies)
        {
            if (other == null || other == enemy || !other.IsGameplayActive) continue;
            var movement = other.GetComponent<EnemyLocomotion>();
            if (movement == null || !movement.isActiveAndEnabled || !movement.UsesMeleeFlanking
                || movement.meleeFlankTarget != target) continue;
            if (Mathf.Abs(other.transform.position.y - BodyPosition().y) > samePlatformMaxY) continue;
            if (movement.meleeFlankSide == naturalSide) near++;
            else far++;
        }
        float chosen = near > far ? -naturalSide : naturalSide;
        if (chosen != naturalSide && !HasWalkableMeleeFlank(target.position.x + chosen * holdGap))
            chosen = naturalSide;
        meleeFlankCrossing = chosen != naturalSide
            || (meleeFlankTarget == target && meleeFlankSide == chosen && meleeFlankCrossing);
        meleeFlankArrivalGap = Mathf.Max(.1f, holdGap - arrivalThreshold);
        meleeFlankTarget = target;
        meleeFlankSide = chosen;
        meleeFlankTargetX = target.position.x;
        meleeFlankFootY = footY;
        return chosen;
    }

    private bool IsCrossingToMeleeFlank()
    {
        if (meleeFlankTarget == null || meleeFlankSide == 0f) return false;
        float signedGap = (BodyPosition().x - meleeFlankTarget.position.x) * meleeFlankSide;
        if (meleeFlankCrossing && signedGap >= meleeFlankArrivalGap) meleeFlankCrossing = false;
        return meleeFlankCrossing || signedGap < 0f;
    }

    private bool HasWalkableMeleeFlank(float targetX)
    {
        Collider2D collider = ResolveBodyCollider();
        if (collider == null) return false;
        float travel = targetX - BodyPosition().x;
        if (Mathf.Abs(ClipFlightTravel(Vector2.right * travel).x - travel) > .05f) return false;
        float feet = collider.bounds.min.y;
        float supportEndX = targetX + Mathf.Sign(travel) * (collider.bounds.extents.x + ledgeProbeDistance);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(supportEndX - BodyPosition().x) / .5f));
        for (int i = 0; i <= steps; i++)
        {
            float x = Mathf.Lerp(BodyPosition().x, supportEndX, (float)i / steps);
            if (!TryFindFlightGround(new Vector2(x, feet + .05f), groundProbeDistance + .05f, out _)) return false;
        }
        return true;
    }

    private void SyncMeleePassThroughBodies()
    {
        Cache();
        if (!UsesMeleeFlanking || !enemy.IsGameplayActive) return;
        Collider2D self = ResolveBodyCollider();
        if (self == null || !self.enabled) return;
        RoomEnemyRegistry.FillLiving(livingEnemies);
        foreach (var other in livingEnemies)
        {
            if (other == null || other == enemy || !other.IsGameplayActive) continue;
            var movement = other.GetComponent<EnemyLocomotion>();
            if (movement == null || !movement.isActiveAndEnabled || !movement.UsesMeleeFlanking) continue;
            Collider2D peer = other.bodyCollider;
            if (peer == null || !peer.enabled || Physics2D.GetIgnoreCollision(self, peer)) continue;
            Physics2D.IgnoreCollision(self, peer, true);
            if (!meleePassThroughBodies.Contains(peer)) meleePassThroughBodies.Add(peer);
        }
    }

    private bool CanDriveMovement()
    {
        Cache();
        if (UsesRoamingFlight) return false;
        return enemy == null
            || (enemy.IsCombatReady && enemy.CurrentReactionState == EnemyReactionState.None);
    }

    private static bool IsActorCollider(Collider2D hitCollider, Collider2D self)
    {
        if (hitCollider == null || hitCollider == self)
            return true;

        Transform owner = hitCollider.transform;
        return owner.GetComponentInParent<EnemyController>() != null
            || owner.GetComponentInParent<PlayerCharacter>() != null;
    }
}
