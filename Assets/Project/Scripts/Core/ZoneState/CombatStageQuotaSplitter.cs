using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Fills a chamber with exactly the authored enemy count from that room's type pool.
    /// </summary>
    public static class CombatStageQuotaSplitter
    {
        public static int[] AssignCounts(IReadOnlyList<int> authoredCounts)
        {
            int chamberCount = authoredCounts != null ? authoredCounts.Count : 0;
            var counts = new int[Mathf.Max(0, chamberCount)];
            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = Mathf.Max(0, authoredCounts[i]);
            }

            return counts;
        }

        public static List<GameObject> Take(IReadOnlyList<GameObject> pool, int count)
        {
            var result = new List<GameObject>(Mathf.Max(0, count));
            if (pool == null || count <= 0)
            {
                return result;
            }

            var valid = new List<GameObject>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    valid.Add(pool[i]);
                }
            }

            if (valid.Count == 0)
            {
                return result;
            }

            int cursor = 0;
            for (int n = 0; n < count; n++)
            {
                result.Add(valid[cursor]);
                cursor++;
                if (cursor >= valid.Count)
                {
                    cursor = 0;
                }
            }

            return result;
        }

        public static List<GameObject>[] Split(IReadOnlyList<GameObject> pool, IReadOnlyList<int> authoredCounts)
        {
            int[] counts = AssignCounts(authoredCounts);
            var slices = new List<GameObject>[counts.Length];
            for (int i = 0; i < slices.Length; i++)
            {
                slices[i] = Take(pool, counts[i]);
            }

            return slices;
        }
    }
}
