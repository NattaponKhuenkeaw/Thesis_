using UnityEngine;

namespace Project.Systems
{
    public static class EntitySortingManager
    {
        public const int PRIORITY_SORTING_OFFSET = 100;
        public const int DEFAULT_SORTING_ORDER = 0;

        /// <summary>
        /// Applies an offset to all sprite renderers on a character.
        /// </summary>
        public static void SetCharacterPriority(SpriteRenderer[] renderers, int[] originalOrders, bool isPriority)
        {
            if (renderers == null || originalOrders == null || renderers.Length != originalOrders.Length)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = originalOrders[i] + (isPriority ? PRIORITY_SORTING_OFFSET : 0);
                }
            }
        }
    }
}
