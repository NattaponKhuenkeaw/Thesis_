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
    [SerializeField] private bool faceTargetOnMove = true;

    private EnemyController enemy;
    private Rigidbody2D body;
    private Transform cachedTransform;
    private float cachedGravityScale = 1f;

    public EnemyMoveMode MoveMode => moveMode;
    public Vector2 AttackSlotOffset => attackSlotOffset;

    private void Awake()
    {
        Cache();
        if (body != null)
        {
            cachedGravityScale = body.gravityScale;
        }

        ApplyModeGravity();
    }

    public void SetMoveMode(EnemyMoveMode mode)
    {
        moveMode = mode;
        Cache();
        ApplyModeGravity();
    }

    public Vector3 ResolveAttackSlot(Transform target)
    {
        Cache();
        Vector3 self = BodyPosition();
        if (target == null)
        {
            return self;
        }

        Vector3 targetPos = target.position;
        float side = Mathf.Sign(self.x - targetPos.x);
        if (Mathf.Approximately(side, 0f))
        {
            side = -EnemyFacingUtil.GetFacingSign(target);
        }

        float x = targetPos.x + side * Mathf.Abs(attackSlotOffset.x);
        float y = moveMode == EnemyMoveMode.Ground ? self.y : targetPos.y + attackSlotOffset.y;
        return new Vector3(x, y, self.z);
    }

    public bool IsInAttackSlot(Transform target, float rangeOverride = -1f)
    {
        if (target == null)
        {
            return false;
        }

        Cache();
        float threshold = rangeOverride > 0f ? rangeOverride : arrivalThreshold;
        Vector3 slot = ResolveAttackSlot(target);
        if (moveMode == EnemyMoveMode.Ground)
        {
            return EnemyFacingUtil.HorizontalDistance(BodyPosition(), slot) <= threshold;
        }

        return EnemyFacingUtil.PlanarDistance(BodyPosition(), slot) <= threshold;
    }

    public bool IsInAttackRangeOfTarget(Transform target, float attackRange)
    {
        if (target == null)
        {
            return false;
        }

        Cache();
        if (moveMode == EnemyMoveMode.Ground)
        {
            return EnemyFacingUtil.HorizontalDistance(BodyPosition(), target.position) <= attackRange;
        }

        return IsInAttackSlot(target, attackRange);
    }

    public void WarpToAttackSlot(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Cache();
        Vector3 slot = ResolveAttackSlot(target);
        Teleport(slot);
        if (faceTargetOnMove)
        {
            EnemyFacingUtil.FaceHorizontal(cachedTransform, target.position.x - slot.x);
        }
    }

    public bool MoveTowardsAttackSlot(Transform target, float speed, float deltaTime)
    {
        if (target == null || speed <= 0f)
        {
            return false;
        }

        Cache();
        Vector3 slot = ResolveAttackSlot(target);
        Vector3 current = BodyPosition();
        if (faceTargetOnMove)
        {
            EnemyFacingUtil.FaceHorizontal(cachedTransform, target.position.x - current.x);
        }

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
        if (IsInAttackRangeOfTarget(target, attackRange))
        {
            if (faceTargetOnMove)
                EnemyFacingUtil.FaceHorizontal(cachedTransform, target.position.x - BodyPosition().x);
            StopHorizontal();
            return true;
        }

        Vector3 current = BodyPosition();
        Vector3 targetPos = target.position;
        if (faceTargetOnMove)
            EnemyFacingUtil.FaceHorizontal(cachedTransform, targetPos.x - current.x);

        float range = Mathf.Max(0.1f, attackRange);
        float side = Mathf.Sign(current.x - targetPos.x);
        if (Mathf.Approximately(side, 0f))
            side = -EnemyFacingUtil.GetFacingSign(target);

        float holdX = targetPos.x + side * range * 0.85f;
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
        if (body != null)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    public void SetHorizontalSpeed(float signedSpeed)
    {
        Cache();
        if (body != null)
        {
            body.linearVelocity = new Vector2(signedSpeed, body.linearVelocity.y);
            return;
        }

        cachedTransform.position += new Vector3(signedSpeed * Time.deltaTime, 0f, 0f);
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
        Vector3 current = BodyPosition();
        float dx = targetX - current.x;
        if (Mathf.Abs(dx) <= arrivalThreshold)
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
        float vx = Mathf.Abs(dx) <= step ? dx / PhysicsStep() : Mathf.Sign(dx) * speed;
        body.linearVelocity = new Vector2(vx, body.linearVelocity.y);
    }

    private void DriveFly(Vector3 target, float speed)
    {
        ApplyModeGravity();
        Vector3 current = BodyPosition();
        Vector3 delta = target - current;
        delta.z = 0f;
        float dist = delta.magnitude;
        if (dist <= arrivalThreshold)
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            return;
        }

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

    private void Teleport(Vector3 worldPosition)
    {
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
        {
            return;
        }

        body.gravityScale = moveMode == EnemyMoveMode.Fly ? 0f : cachedGravityScale;
    }

    private static float PhysicsStep()
    {
        return Mathf.Max(Time.fixedDeltaTime, 0.0001f);
    }

    private void Cache()
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        if (enemy == null)
        {
            enemy = GetComponent<EnemyController>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }
}
