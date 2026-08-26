using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Horizontal playable edges for a room. Walls are the source of truth;
    /// the exit trigger is only a fallback for the right edge.
    /// </summary>
    public static class RoomBoundaryMath
    {
        public static bool TryResolveLockedX(
            Collider2D entranceWall,
            Collider2D exitWall,
            Collider2D exitTrigger,
            out float left,
            out float right)
        {
            left = 0f;
            right = 0f;

            bool hasLeft = entranceWall != null;
            bool hasRight = exitWall != null || exitTrigger != null;
            if (!hasLeft && !hasRight)
            {
                return false;
            }

            left = hasLeft
                ? entranceWall.bounds.max.x
                : (exitWall != null ? exitWall.bounds.min.x : exitTrigger.bounds.min.x) - 16f;

            if (exitWall != null)
            {
                right = exitWall.bounds.min.x;
            }
            else if (exitTrigger != null)
            {
                right = exitTrigger.bounds.min.x;
            }
            else
            {
                right = left + 16f;
            }

            return right > left + 0.001f;
        }

        /// <summary>
        /// Stop X just before a solid wall so the body does not rest against it.
        /// <paramref name="walkSign"/> of +1 uses the wall's room-facing min X;
        /// -1 uses max X.
        /// </summary>
        public static float ResolveStopBeforeWallX(
            float wallInnerX,
            float walkSign,
            float bodyExtentTowardWall,
            float skin)
        {
            float clearance = Mathf.Max(0.01f, bodyExtentTowardWall + Mathf.Max(0f, skin));
            return walkSign >= 0f
                ? wallInnerX - clearance
                : wallInnerX + clearance;
        }

        public static bool IsStageExitWallName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            return string.Equals(objectName.Trim(), "Wall_Exit", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryEncapsulateVertical(Collider2D[] colliders, out float bottom, out float top)
        {
            bottom = float.PositiveInfinity;
            top = float.NegativeInfinity;
            if (colliders == null)
            {
                return false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                bottom = Mathf.Min(bottom, bounds.min.y);
                top = Mathf.Max(top, bounds.max.y);
            }

            return !float.IsInfinity(bottom) && top > bottom + 0.001f;
        }
    }
}
