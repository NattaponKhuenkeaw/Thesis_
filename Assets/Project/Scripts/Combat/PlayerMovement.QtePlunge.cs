using System;
using UnityEngine;

public partial class PlayerMovement
{
    private bool _hasCapturedWorldMotion;
    private QteEntryPlacement _plungePlacement;
    private MotionKeyframe _independentCapturedMotion;
    private float _independentCapturedElapsed;
    private float _capturedLastFixedTime = float.NegativeInfinity;
    private bool _capturedMotionFailed;
    public bool HasCapturedWorldMotion => _hasCapturedWorldMotion;
    public Vector2 PlungeSurfacePoint => _plungePlacement.support != null
        ? (Vector2)_plungePlacement.support.transform.TransformPoint(_plungePlacement.supportLocalPoint)
        : _plungePlacement.surfacePoint;

    public bool TryFindQtePlungePlacement(Vector2 targetPosition, Vector2 startOffset,
        int approachFacing, out QteEntryPlacement placement)
    {
        placement = default;
        if (rb == null || groundCheck == null) return false;
        Vector2 start = targetPosition + new Vector2(startOffset.x * approachFacing, startOffset.y);
        Vector2 footOffset = (Vector2)groundCheck.position - rb.position;
        // The entry arrives above the target; only its vertical descent crosses terrain.
        Vector2 startFoot = start + footOffset;
        float probeDistance = Mathf.Max(0.3f, startFoot.y - Mathf.Min(targetPosition.y, groundCheck.position.y) + 0.3f);
        var filter = new ContactFilter2D();
        filter.SetLayerMask(GetGroundCheckMask());
        filter.useTriggers = false;
        int hits = Physics2D.Raycast(startFoot, Vector2.down, filter, movementWallHits, probeDistance);
        if (hits == 0 || hits == movementWallHits.Length) return false;
        RaycastHit2D floor = movementWallHits[0];
        if (floor.collider == null || floor.collider.isTrigger || floor.normal.y < MinimumGroundNormalY) return false;
        Vector2 landing = new Vector2(start.x, floor.point.y - footOffset.y);
        if (start.y <= landing.y + 0.1f) return false;
        EnsureQteBodyShapes();
        if (!HasQteBodyClearance(start) || !HasQteBodyClearance(landing)
            || !HasQtePathClearance(start, landing, floor.collider)) return false;
        int facing = targetPosition.x >= landing.x ? 1 : -1;
        placement = new QteEntryPlacement(start, landing, floor.point, floor.normal,
            targetPosition, floor.collider, approachFacing, facing);
        return true;
    }

    public void ApplyQteEntryPosition(in QteEntryPlacement placement)
    {
        StopAttackMotion();
        ClearCapturedWorldMotion();
        SetQteBodyPosition(placement.startBodyPosition);
        GroundState.SynchronizeWithoutTransition(false, 0f);
        isGrounded = false;
        actionOutputSnapFixedTime = float.NegativeInfinity;
        airborneTransitionCause = AirborneTransitionCause.AuthoredActionMotion;
        landingFramesRemaining = 0;
        CacheGroundCheckPosition();
    }

    public void PrepareCapturedWorldMotion(in QteEntryPlacement placement)
    {
        StopAttackMotion();
        _plungePlacement = placement;
        _hasCapturedWorldMotion = true;
        _independentCapturedMotion = null;
        _independentCapturedElapsed = 0f;
        _capturedMotionFailed = false;
    }

    public bool SampleCapturedWorldMotion(MotionKeyframe motion, int logicFrame, bool followSlowMotion = true)
    {
        if (followSlowMotion)
        {
            _independentCapturedMotion = null;
            return SampleCapturedWorldMotionAtFrame(motion, logicFrame);
        }
        if (!_hasCapturedWorldMotion || motion == null) return false;
        if (logicFrame < motion.startFrame) return SampleCapturedWorldMotionAtFrame(motion, logicFrame);
        if (_independentCapturedMotion != motion)
        {
            _independentCapturedMotion = motion;
            _independentCapturedElapsed = 0f;
            _capturedLastFixedTime = Time.fixedTime;
            _capturedMotionFailed = false;
        }
        if (Time.inFixedTimeStep) TickIndependentCapturedMotion();
        if (_capturedMotionFailed) return false;
        // The fixed motor owns independent progression; repeated logic samples must not advance its clock twice.
        return !Time.inFixedTimeStep || SampleCapturedWorldMotionAtFrame(motion,
            motion.startFrame + _independentCapturedElapsed * CombatCore.FrameDataConstants.LOGIC_FPS);
    }

    private void TickIndependentCapturedMotion()
    {
        if (!_hasCapturedWorldMotion || _independentCapturedMotion == null || _capturedMotionFailed
            || _capturedLastFixedTime == Time.fixedTime) return;
        _capturedLastFixedTime = Time.fixedTime;
        _independentCapturedElapsed += CombatCore.AnimationTimePolicy.Delta(false, Time.fixedDeltaTime, Time.fixedUnscaledDeltaTime,
            Time.timeScale <= 0f || (GameFreezeManager.Instance != null && GameFreezeManager.Instance.IsPauseHeld));
        _capturedMotionFailed = !SampleCapturedWorldMotionAtFrame(_independentCapturedMotion,
            _independentCapturedMotion.startFrame + _independentCapturedElapsed * CombatCore.FrameDataConstants.LOGIC_FPS);
    }

    private bool SampleCapturedWorldMotionAtFrame(MotionKeyframe motion, float logicFrame)
    {
        if (!_hasCapturedWorldMotion || motion == null || _plungePlacement.support == null
            || !_plungePlacement.support.enabled || !_plungePlacement.support.gameObject.activeInHierarchy) return false;
        Vector2 shift = PlungeSurfacePoint - _plungePlacement.surfacePoint;
        Vector2 landing = _plungePlacement.landingBodyPosition + shift;
        float progress = motion.endFrame <= motion.startFrame ? (logicFrame >= motion.startFrame ? 1f : 0f)
            : Mathf.Clamp01((logicFrame - motion.startFrame) / (motion.endFrame - motion.startFrame));
        float eased = TweenEase(0f, 1f, progress, motion.easing);
        Vector2 desired = Vector2.LerpUnclamped(_plungePlacement.startBodyPosition, landing, eased);
        if (!HasQteBodyClearance(desired) || !HasQtePathClearance(rb.position, desired, _plungePlacement.support)) return false;
        SetQteBodyPosition(desired);
        GroundState.SynchronizeWithoutTransition(false, 0f);
        isGrounded = false;
        return true;
    }

    public bool TryCommitQteGroundContact(out Vector2 surfacePoint)
    {
        surfacePoint = PlungeSurfacePoint;
        if (!_hasCapturedWorldMotion || _plungePlacement.support == null) return false;
        Vector2 landing = _plungePlacement.landingBodyPosition + surfacePoint - _plungePlacement.surfacePoint;
        if ((rb.position - landing).sqrMagnitude > 0.0225f) return false;
        RaycastHit2D floor = Physics2D.Raycast((Vector2)groundCheck.position + Vector2.up * 0.15f,
            Vector2.down, 0.35f, GetGroundCheckMask());
        if (floor.collider != _plungePlacement.support || floor.normal.y < MinimumGroundNormalY) return false;
        landing.y += floor.point.y - groundCheck.position.y;
        ApplyQteGroundPosition(landing);
        ResetLandingResources();
        landingFramesRemaining = 0;
        actionOutputSnapFixedTime = Time.inFixedTimeStep ? Time.fixedTime : Time.fixedTime + Time.fixedDeltaTime;
        surfacePoint = floor.point;
        return true;
    }

    public void ClearCapturedWorldMotion()
    {
        if (!_hasCapturedWorldMotion) return;
        _hasCapturedWorldMotion = false;
        _independentCapturedMotion = null;
        _capturedMotionFailed = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        lastAppliedMotionVelocityY = 0f;
        _plungePlacement = default;
    }

    private void SetQteBodyPosition(Vector2 position)
    {
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        lastAppliedMotionVelocityY = 0f;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        Physics2D.SyncTransforms();
    }

    private void EnsureQteBodyShapes()
    {
        if (solidColliders == null)
            solidColliders = Array.FindAll(GetComponentsInChildren<Collider2D>(),
                c => c != null && !c.isTrigger && c.attachedRigidbody == rb);
    }

    private bool IgnoreQteObstacle(Collider2D body, Collider2D obstacle)
    {
        // Match grounded QTE placement: finishing swap bodies can overlap the incoming actor.
        return obstacle == null || obstacle.attachedRigidbody == rb
            || obstacle.GetComponentInParent<EnemyController>() != null
            || obstacle.GetComponentInParent<PlayerTeamSwitchState>() != null
            || Physics2D.GetIgnoreCollision(body, obstacle);
    }

    private bool HasQteBodyClearance(Vector2 position)
    {
        foreach (Collider2D body in solidColliders)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
            Bounds bounds = body.bounds;
            Vector2 center = (Vector2)bounds.center + position - rb.position;
            float inset = Mathf.Max(MovementWallSkin, Physics2D.defaultContactOffset * 2f) + 0.03f;
            Vector2 size = Vector2.Max((Vector2)bounds.size - Vector2.one * inset * 2f, Vector2.one * 0.01f);
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer));
            filter.useTriggers = false;
            int count = Physics2D.OverlapBox(center, size, 0f, filter, qteClearanceHits);
            if (count == qteClearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D obstacle = qteClearanceHits[i];
                if (IgnoreQteObstacle(body, obstacle)
                    || (obstacle.usedByEffector && obstacle.GetComponent<PlatformEffector2D>() != null)) continue;
                return false;
            }
        }
        return true;
    }

    private bool HasQtePathClearance(Vector2 start, Vector2 end, Collider2D support)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance < 0.0001f) return true;
        foreach (Collider2D body in solidColliders)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
            Bounds bounds = body.bounds;
            Vector2 center = (Vector2)bounds.center + start - rb.position;
            float inset = Mathf.Max(MovementWallSkin, Physics2D.defaultContactOffset * 2f) + 0.03f;
            Vector2 size = Vector2.Max((Vector2)bounds.size - Vector2.one * inset * 2f, Vector2.one * 0.01f);
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer));
            filter.useTriggers = false;
            int count = Physics2D.BoxCast(center, size, 0f, delta / distance, filter, movementWallHits, distance);
            if (count == movementWallHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = movementWallHits[i];
                if (IgnoreQteObstacle(body, hit.collider)) continue;
                if (hit.collider == support && hit.normal.y >= MinimumGroundNormalY && hit.distance >= distance - 0.1f) continue;
                if (hit.collider.usedByEffector && hit.collider.GetComponent<PlatformEffector2D>() != null && delta.y >= 0f) continue;
                if (Vector2.Dot(hit.normal, delta) < -0.001f) return false;
            }
        }
        return true;
    }
}
