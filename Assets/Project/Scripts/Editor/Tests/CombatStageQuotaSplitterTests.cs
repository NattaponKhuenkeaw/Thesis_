#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class CombatStageQuotaSplitterTests
{
    [Test]
    public void AssignCountsUsesAuthoredNumbersOnly()
    {
        int[] counts = CombatStageQuotaSplitter.AssignCounts(new[] { 3, 0, 4 });
        Assert.That(counts, Is.EqualTo(new[] { 3, 0, 4 }));
    }

    [Test]
    public void SplitTakesExactCountPerChamberAndDoesNotDumpLeftovers()
    {
        var a = new GameObject("a");
        var b = new GameObject("b");
        var c = new GameObject("c");
        var d = new GameObject("d");
        try
        {
            List<GameObject>[] slices = CombatStageQuotaSplitter.Split(
                new[] { a, b, c, d },
                new[] { 1, 2, 0 });

            Assert.That(slices.Length, Is.EqualTo(3));
            Assert.That(slices[0], Is.EqualTo(new[] { a }));
            Assert.That(slices[1], Is.EqualTo(new[] { a, b }));
            Assert.That(slices[2], Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
            Object.DestroyImmediate(d);
        }
    }

    [Test]
    public void SplitWrapsPoolWhenRoomAsksForMoreThanPoolSize()
    {
        var a = new GameObject("a");
        var b = new GameObject("b");
        try
        {
            List<GameObject>[] slices = CombatStageQuotaSplitter.Split(
                new[] { a, b },
                new[] { 3 });

            Assert.That(slices[0], Is.EqualTo(new[] { a, b, a }));
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    [Test]
    public void PalettePrefersEnemyTypesOverWaves()
    {
        var fromTypes = new GameObject("types");
        var fromWaves = new GameObject("waves");
        try
        {
            var wave = new EncounterSO.Wave
            {
                enemies = new List<EncounterSO.SpawnEntry>
                {
                    new EncounterSO.SpawnEntry { enemyPrefab = fromWaves }
                }
            };

            List<GameObject> palette = EncounterSO.CollectPalettePrefabs(
                new[] { fromTypes },
                new[] { wave });

            Assert.That(palette, Is.EqualTo(new[] { fromTypes }));
        }
        finally
        {
            Object.DestroyImmediate(fromTypes);
            Object.DestroyImmediate(fromWaves);
        }
    }

    [Test]
    public void PaletteFallsBackToWavesWhenTypesEmpty()
    {
        var fromWaves = new GameObject("waves");
        try
        {
            var wave = new EncounterSO.Wave
            {
                enemies = new List<EncounterSO.SpawnEntry>
                {
                    new EncounterSO.SpawnEntry { enemyPrefab = fromWaves }
                }
            };

            List<GameObject> palette = EncounterSO.CollectPalettePrefabs(
                System.Array.Empty<GameObject>(),
                new[] { wave });

            Assert.That(palette, Is.EqualTo(new[] { fromWaves }));
        }
        finally
        {
            Object.DestroyImmediate(fromWaves);
        }
    }
}
#endif
