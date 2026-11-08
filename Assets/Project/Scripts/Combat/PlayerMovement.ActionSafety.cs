using UnityEngine;

public partial class PlayerMovement
{
    private readonly RaycastHit2D[] _actionSupportHits = new RaycastHit2D[8];
    private const float ActionSupportSampleSpacing = 0.1f;

    private int ResolveActionTargetFacing(EnemyController target)
    {
        if (target == null) return FacingDirection;
        float dx = target.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= AttackFacingCancelInputThreshold) return FacingDirection;

        Collider2D targetBody = target.bodyCollider;
        if (targetBody == null)
        {
            foreach (Collider2D candidate in target.GetCachedColliders())
            {
                if (candidate != null && candidate.enabled && !candidate.isTrigger
                    && candidate.attachedRigidbody == target.rb)
                {
                    targetBody = candidate;
                    break;
                }
            }
        }
        if (targetBody != null && targetBody.enabled && targetBody.gameObject.activeInHierarchy
            && solidColliders != null)
        {
            Bounds targetBounds = targetBody.bounds;
            foreach (Collider2D body in solidColliders)
            {
                if (body != null && body.enabled && !body.isTrigger && body.attachedRigidbody == rb
                    && body.gameObject.activeInHierarchy && body.bounds.Intersects(targetBounds))
                    return FacingDirection;
            }
        }
        return dx > 0f ? 1 : -1;
    }

    private float ClipGroundedActionVelocity(float velocity)
    {
        float step = Mathf.Max(Time.fixedDeltaTime,
            attackMotionIgnoresSlowMotion && !useUnscaledAttackMotion ? 0.000001f : 0.001f);
        return ClipGroundedActionTravel(velocity * step) / step;
    }

    private float ClipGroundedActionTravel(float travel)
    {
        if (rb == null || groundCheck == null || !GroundState.IsGrounded
            || (fsm != null && !fsm.IsGroundedContext) || IsDroppingDown || IsJumpStarting
            || Mathf.Abs(travel) < 0.000001f)
            return travel;

        Vector2 foot = (Vector2)groundCheck.position + rb.position - (Vector2)transform.position;
        float side = Mathf.Sign(travel);
        Vector2 leadingFoot = foot + Vector2.right * side
            * Mathf.Max(0f, GetBodyExtentToward(side) - MovementWallSkin);
        bool leadingSupported = HasActionSupport(leadingFoot);
        if (!HasActionSupport(foot) && !leadingSupported)
        {
            Vector2 trailingFoot = foot - Vector2.right * side
                * Mathf.Max(0f, GetBodyExtentToward(-side) - MovementWallSkin);
            // A body can still be grounded while its center overhangs an edge.
            // Distinguish that contact from a floor that actually disappeared.
            return HasActionSupport(trailingFoot) ? 0f : travel;
        }
        if (!leadingSupported) return 0f;

        float distance = Mathf.Abs(travel);
        float safe = 0f;
        // Checking only the destination would let instant motion skip gaps.
        while (safe < distance)
        {
            float next = Mathf.Min(distance, safe + ActionSupportSampleSpacing);
            if (!HasActionSupport(leadingFoot + Vector2.right * side * next))
            {
                float blocked = next;
                while (blocked - safe > MovementWallSkin * 0.25f)
                {
                    float midpoint = (safe + blocked) * 0.5f;
                    if (HasActionSupport(leadingFoot + Vector2.right * side * midpoint)) safe = midpoint;
                    else blocked = midpoint;
                }
                return side * safe;
            }
            safe = next;
        }
        return travel;
    }

    private bool HasActionSupport(Vector2 foot)
    {
        float lift = Mathf.Max(MovementWallSkin, Physics2D.defaultContactOffset * 2f);
        int count = Physics2D.Raycast(foot + Vector2.up * lift, Vector2.down,
            groundContactFilter, _actionSupportHits, lift * 2f);
        if (count == _actionSupportHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _actionSupportHits[i];
            if (hit.collider != null && hit.collider != ignoredGroundCollider
                && hit.collider.attachedRigidbody != rb && !hit.collider.isTrigger
                && hit.normal.y >= MinimumGroundNormalY)
                return true;
        }
        return false;
    }
}
