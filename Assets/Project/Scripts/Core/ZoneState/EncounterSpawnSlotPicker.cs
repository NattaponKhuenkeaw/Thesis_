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
            System.Collections.Generic.ICollection<Transform> occupied = null)
        {
            if (slots == null || slots.Length == 0)
            {
                return null;
            }

            Transform best = PickFrom(slots, referencePosition, preferFar, occupied);
            if (best != null)
            {
                return best;
            }

            return occupied != null && occupied.Count > 0
                ? PickFrom(slots, referencePosition, preferFar, null)
                : null;
        }

        private static Transform PickFrom(
            Transform[] slots,
            Vector3 referencePosition,
            bool preferFar,
            System.Collections.Generic.ICollection<Transform> occupied)
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
