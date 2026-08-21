using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.ZoneState
{
    /// <summary>
    /// Rolls a chamber quota from weighted enemy types on an Encounter.
    /// </summary>
    public static class EncounterTypePicker
    {
        public delegate int IntRange(int minInclusive, int maxExclusive);

        public static List<GameObject> Roll(
            IReadOnlyList<EncounterSO.EnemyTypeEntry> entries,
            int count,
            EncounterTypePickMode mode,
            IntRange randomRange = null)
        {
            var result = new List<GameObject>(Mathf.Max(0, count));
            if (count <= 0)
            {
                return result;
            }

            List<EncounterSO.EnemyTypeEntry> valid = CollectValid(entries);
            if (valid.Count == 0)
            {
                return result;
            }

            IntRange rng = randomRange ?? UnityRandomRange;
            if (mode == EncounterTypePickMode.WeightedRandom)
            {
                RollIndependent(valid, count, rng, result);
                return result;
            }

            RollShuffleBag(valid, count, rng, result);
            return result;
        }

        public static List<GameObject> RollFromPrefabs(
            IReadOnlyList<GameObject> prefabs,
            int count,
            EncounterTypePickMode mode,
            IntRange randomRange = null)
        {
            var entries = new List<EncounterSO.EnemyTypeEntry>();
            if (prefabs != null)
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    if (prefabs[i] != null)
                    {
                        entries.Add(new EncounterSO.EnemyTypeEntry
                        {
                            prefab = prefabs[i],
                            weight = 1
                        });
                    }
                }
            }

            return Roll(entries, count, mode, randomRange);
        }

        private static List<EncounterSO.EnemyTypeEntry> CollectValid(
            IReadOnlyList<EncounterSO.EnemyTypeEntry> entries)
        {
            var valid = new List<EncounterSO.EnemyTypeEntry>();
            if (entries == null)
            {
                return valid;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                EncounterSO.EnemyTypeEntry entry = entries[i];
                if (entry != null && entry.prefab != null && entry.weight > 0)
                {
                    valid.Add(entry);
                }
            }

            return valid;
        }

        private static void RollIndependent(
            IReadOnlyList<EncounterSO.EnemyTypeEntry> valid,
            int count,
            IntRange rng,
            List<GameObject> result)
        {
            int total = SumWeights(valid);
            if (total <= 0)
            {
                return;
            }

            for (int n = 0; n < count; n++)
            {
                int roll = rng(0, total);
                result.Add(PickWeighted(valid, roll));
            }
        }

        private static void RollShuffleBag(
            IReadOnlyList<EncounterSO.EnemyTypeEntry> valid,
            int count,
            IntRange rng,
            List<GameObject> result)
        {
            List<GameObject> bag = ExpandBag(valid);
            if (bag.Count == 0)
            {
                return;
            }

            int cursor = bag.Count;
            for (int n = 0; n < count; n++)
            {
                if (cursor >= bag.Count)
                {
                    Shuffle(bag, rng);
                    cursor = 0;
                }

                result.Add(bag[cursor]);
                cursor++;
            }
        }

        private static List<GameObject> ExpandBag(IReadOnlyList<EncounterSO.EnemyTypeEntry> valid)
        {
            int size = SumWeights(valid);
            var bag = new List<GameObject>(Mathf.Max(0, size));
            for (int i = 0; i < valid.Count; i++)
            {
                EncounterSO.EnemyTypeEntry entry = valid[i];
                int copies = Mathf.Max(0, entry.weight);
                for (int c = 0; c < copies; c++)
                {
                    bag.Add(entry.prefab);
                }
            }

            return bag;
        }

        private static int SumWeights(IReadOnlyList<EncounterSO.EnemyTypeEntry> valid)
        {
            int total = 0;
            for (int i = 0; i < valid.Count; i++)
            {
                total += Mathf.Max(0, valid[i].weight);
            }

            return total;
        }

        private static GameObject PickWeighted(IReadOnlyList<EncounterSO.EnemyTypeEntry> valid, int roll)
        {
            int cursor = 0;
            for (int i = 0; i < valid.Count; i++)
            {
                cursor += Mathf.Max(0, valid[i].weight);
                if (roll < cursor)
                {
                    return valid[i].prefab;
                }
            }

            return valid[valid.Count - 1].prefab;
        }

        private static void Shuffle(List<GameObject> bag, IntRange rng)
        {
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng(0, i + 1);
                GameObject swap = bag[i];
                bag[i] = bag[j];
                bag[j] = swap;
            }
        }

        private static int UnityRandomRange(int minInclusive, int maxExclusive)
        {
            return Random.Range(minInclusive, maxExclusive);
        }
    }
}
