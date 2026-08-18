#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class EncounterTypePickerTests
{
    [Test]
    public void WeightedRandomUsesRollAgainstWeights()
    {
        var a = new GameObject("a");
        var b = new GameObject("b");
        try
        {
            var entries = new[]
            {
                new EncounterSO.EnemyTypeEntry { prefab = a, weight = 2 },
                new EncounterSO.EnemyTypeEntry { prefab = b, weight = 1 }
            };
            int[] rolls = { 0, 2, 1 };
            int index = 0;

            List<GameObject> quota = EncounterTypePicker.Roll(
                entries,
                3,
                EncounterTypePickMode.WeightedRandom,
                (min, maxExclusive) => rolls[index++]);

            Assert.That(quota, Is.EqualTo(new[] { a, b, a }));
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    [Test]
    public void ShuffleBagDealsExpandedWeightsThenReshuffles()
    {
        var a = new GameObject("a");
        var b = new GameObject("b");
        try
        {
            var entries = new[]
            {
                new EncounterSO.EnemyTypeEntry { prefab = a, weight = 2 },
                new EncounterSO.EnemyTypeEntry { prefab = b, weight = 1 }
            };

            List<GameObject> quota = EncounterTypePicker.Roll(
                entries,
                4,
                EncounterTypePickMode.ShuffleBag,
                (min, maxExclusive) => maxExclusive - 1);

            Assert.That(quota, Is.EqualTo(new[] { a, a, b, a }));
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    [Test]
    public void SkipsZeroWeightAndNullPrefab()
    {
        var a = new GameObject("a");
        try
        {
            var entries = new[]
            {
                new EncounterSO.EnemyTypeEntry { prefab = null, weight = 9 },
                new EncounterSO.EnemyTypeEntry { prefab = a, weight = 0 },
                new EncounterSO.EnemyTypeEntry { prefab = a, weight = 1 }
            };

            List<GameObject> quota = EncounterTypePicker.Roll(
                entries,
                2,
                EncounterTypePickMode.WeightedRandom,
                (min, maxExclusive) => 0);

            Assert.That(quota, Is.EqualTo(new[] { a, a }));
        }
        finally
        {
            Object.DestroyImmediate(a);
        }
    }
}
#endif
