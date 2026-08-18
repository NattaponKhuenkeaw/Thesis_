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
    }
}
