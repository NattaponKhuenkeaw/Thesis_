using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Picks a layout spawn slot from the player: melee near, ranged far.
    /// </summary>
    public static class EncounterSpawnSlotPicker
    {
        public static Transform Pick(
            Transform[] slots,
            Vector3 referencePosition,
            bool preferFar,
            ICollection<Transform> occupied = null)
        {
            if (slots == null || slots.Length == 0)
            {
                return null;
            }

            return PickFrom(slots, referencePosition, preferFar, occupied);
        }

        /// <summary>
        /// Picks the role-appropriate slot that still keeps <paramref name="minSeparation"/>
        /// from every living enemy. When nothing is clear, returns the roomiest slot and
        /// reports it through <paramref name="isClear"/> so the caller can slide the spawn
        /// sideways instead of dropping a second body on the same marker.
        /// </summary>
        public static Transform PickWithClearance(
            Transform[] slots,
            Vector3 referencePosition,
            bool preferFar,
            IReadOnlyList<Vector3> blockedPositions,
            float minSeparation,
            out bool isClear)
        {
            isClear = false;
            if (slots == null || slots.Length == 0)
            {
                return null;
            }

            Transform bestClear = null;
            float bestRoleScore = preferFar ? float.MinValue : float.MaxValue;
            Transform roomiest = null;
            float roomiestClearance = float.MinValue;

            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                float clearance = NearestBlockedDistance(slot.position, blockedPositions);
                if (clearance > roomiestClearance)
                {
                    roomiestClearance = clearance;
                    roomiest = slot;
                }

                if (clearance < minSeparation)
                {
                    continue;
                }

                float roleScore = Mathf.Abs(slot.position.x - referencePosition.x);
                bool better = bestClear == null
                    || (preferFar ? roleScore > bestRoleScore : roleScore < bestRoleScore);
                if (better)
                {
                    bestRoleScore = roleScore;
                    bestClear = slot;
                }
            }

            if (bestClear != null)
            {
                isClear = true;
                return bestClear;
            }

            return roomiest;
        }

        public static float NearestBlockedDistance(
            Vector3 position,
            IReadOnlyList<Vector3> blockedPositions)
        {
            if (blockedPositions == null || blockedPositions.Count == 0)
            {
                return float.MaxValue;
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < blockedPositions.Count; i++)
            {
                float distance = Vector2.Distance(position, blockedPositions[i]);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private static Transform PickFrom(
            Transform[] slots,
            Vector3 referencePosition,
            bool preferFar,
            ICollection<Transform> occupied)
        {
            Transform best = null;
            float bestScore = preferFar ? float.MinValue : float.MaxValue;

            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot = slots[i];
                if (slot == null || (occupied != null && occupied.Contains(slot)))
                {
                    continue;
                }

                float distance = Mathf.Abs(slot.position.x - referencePosition.x);
                if (preferFar)
                {
                    if (distance > bestScore)
                    {
                        bestScore = distance;
                        best = slot;
                    }
                }
                else if (distance < bestScore)
                {
                    bestScore = distance;
                    best = slot;
                }
            }

            return best;
        }

        public static bool PrefersFarSlot(GameObject enemyPrefab)
        {
            if (enemyPrefab == null)
            {
                return false;
            }

            EnemyController controller = enemyPrefab.GetComponent<EnemyController>();
            if (controller == null)
            {
                controller = enemyPrefab.GetComponentInChildren<EnemyController>(true);
            }

            return controller != null && controller.UsesRangedAttack;
        }
    }
}
