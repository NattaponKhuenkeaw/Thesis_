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

    [Tooltip("Leave off for Enemy1-3. Fly/Warp stay in code for later kits.")]
    [SerializeField] private bool allowNonGroundMove = false;

    [Header("Ground Reach")]
    [Tooltip("If the target's feet are farther than this in Y, treat as another platform.")]
    [SerializeField, Min(0.05f)] private float samePlatformMaxY = 0.85f;
    [SerializeField, Min(0.05f)] private float ledgeProbeDistance = 0.2f;
    [SerializeField, Min(0.05f)] private float groundProbeDistance = 0.22f;
    [SerializeField] private LayerMask groundLayers;

    [Header("Unstack")]
    [Tooltip("If another enemy is closer than this on X, walk apart.")]
    [SerializeField, Min(0.1f)] private float overlapRadius = 0.7f;
    [SerializeField, Min(0.1f)] private float unstackSpeed = 2.2f;
    [SerializeField, Min(0.05f)] private float overlapSameY = 0.55f;
    [SerializeField, Min(0.1f)] private float crowdSlotSpacing = 0.55f;
    [Tooltip("Seconds overlap must last before they walk apart.")]
    [SerializeField, Min(0f)] private float overlapConfirmTime = 0.4f;
    [Tooltip("Once they start splitting, keep that direction this long so they do not flip every frame.")]
    [SerializeField, Min(0f)] private float unstackCommitTime = 0.45f;

    private static readonly List<EnemyController> livingEnemies = new List<EnemyController>(16);

    private EnemyController enemy;
    private Rigidbody2D body;
    private Transform cachedTransform;
    private float cachedGravityScale = 1f;
    private float platformPatrolSign = 1f;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
    private float overlapElapsed;
    private float unstackUntil;
    private float unstackSign;

    public EnemyMoveMode MoveMode => moveMode;
    public Vector2 AttackSlotOffset => attackSlotOffset;

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

        float x = targetPos.x + side * Mathf.Abs(attackSlotOffset.x) + ResolveCrowdSlotOffset();
        float y = moveMode == EnemyMoveMode.Ground ? self.y : targetPos.y + attackSlotOffset.y;
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
    /// Walk or warp closer until within attack range, then hold. Never moves away from the target.
    /// </summary>
    public bool MoveTowardsTargetRange(Transform target, float attackRange, float speed, float deltaTime)
    {
        if (target == null)
            return false;

        Cache();
        Vector3 current = BodyPosition();
        Vector3 targetPos = target.position;

        float range = Mathf.Max(0.1f, attackRange);
        float distanceX = EnemyFacingUtil.HorizontalDistance(current, targetPos);
        // Stay beside the player even while they jump. Same-platform is only required to actually strike.
        if (distanceX <= range)
        {
            if (!IsUnstacking)
                StopHorizontal();
            return IsInAttackRangeOfTarget(target, attackRange);
        }

        bool rangedHold = enemy != null && enemy.UsesRangedAttack;
        // Ranged never kites. Close in on the player; melee still uses a hold point in front.
        float holdX = rangedHold
            ? targetPos.x
            : targetPos.x + (Mathf.Approximately(current.x - targetPos.x, 0f)
                ? -EnemyFacingUtil.GetFacingSign(target)
                : Mathf.Sign(current.x - targetPos.x)) * range * 0.85f;
        holdX += ResolveCrowdSlotOffset();
        Vector3 holdPoint = moveMode == EnemyMoveMode.Ground
            ? new Vector3(holdX, current.y, current.z)
            : new Vector3(holdX, targetPos.y + attackSlotOffset.y, current.z);

        if (moveMode == EnemyMoveMode.Warp)
        {
            Teleport(holdPoint);
            return true;
        }

        if (moveMode == EnemyMoveMode.Ground)
            DriveGroundX(holdPoint.x, Mathf.Max(0f, speed));
        else
            DriveFly(holdPoint, Mathf.Max(0f, speed));

        return IsInAttackRangeOfTarget(target, attackRange);
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
        if (!CanDriveMovement())
            return;

        FaceWalkDirection(signedSpeed);
        if (body != null)
        {
            body.linearVelocity = new Vector2(signedSpeed, body.linearVelocity.y);
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

    public bool IsUnstacking => Time.time < unstackUntil;

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

    private float ResolveCrowdSlotOffset()
    {
        Cache();
        if (enemy == null)
            return 0f;

        Vector3 self = BodyPosition();
        RoomEnemyRegistry.FillLiving(livingEnemies);
        int selfId = enemy.GetInstanceID();
        int index = 0;
        int count = 0;
        for (int i = 0; i < livingEnemies.Count; i++)
        {
            EnemyController other = livingEnemies[i];
            if (other == null || !other.IsAlive || other.GetComponent<EnemyLocomotion>() == null)
                continue;
            if (Mathf.Abs(other.transform.position.y - self.y) > overlapSameY + 0.4f)
                continue;

            count++;
            if (other.GetInstanceID() < selfId)
                index++;
        }

        if (count <= 1)
            return 0f;

        float center = (count - 1) * 0.5f;
        return (index - center) * crowdSlotSpacing;
    }

    private bool CanDriveMovement()
    {
        Cache();
        return enemy == null || enemy.CurrentReactionState == EnemyReactionState.None;
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
