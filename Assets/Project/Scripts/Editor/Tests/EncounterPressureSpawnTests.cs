#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEngine;

public class EncounterPressureSpawnTests
{
    [Test]
    public void PressureDirectorStopsAtAliveCapThenRefillsOnDeath()
    {
        GameObject prefab = new GameObject("quota");
        try
        {
            var quota = new GameObject[5];
            for (int i = 0; i < quota.Length; i++)
            {
                quota[i] = prefab;
            }

            var director = new EncounterPressureDirector(3, quota);
            int living = 0;

            while (director.CanSpawn(living))
            {
                Assert.That(director.DequeueNext(), Is.Not.Null);
                living++;
            }

            Assert.That(living, Is.EqualTo(3));
            Assert.That(director.Remaining, Is.EqualTo(2));
            Assert.That(director.CanSpawn(living), Is.False);

            living--;
            Assert.That(director.CanSpawn(living), Is.True);
            Assert.That(director.DequeueNext(), Is.Not.Null);
            living++;
            Assert.That(living, Is.EqualTo(3));
            Assert.That(director.Remaining, Is.EqualTo(1));

            living = 0;
            Assert.That(director.IsCleared(living), Is.False);
            Assert.That(director.DequeueNext(), Is.Not.Null);
            Assert.That(director.Remaining, Is.EqualTo(0));
            Assert.That(director.IsCleared(0), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void SlotPickerChoosesNearForMeleeAndFarForRanged()
    {
        var near = new GameObject("near");
        var far = new GameObject("far");
        try
        {
            near.transform.position = new Vector3(1f, 0f, 0f);
            far.transform.position = new Vector3(10f, 0f, 0f);
            Transform[] slots = { near.transform, far.transform };
            Vector3 player = Vector3.zero;

            Transform meleeSlot = EncounterSpawnSlotPicker.Pick(slots, player, preferFar: false);
            Transform rangedSlot = EncounterSpawnSlotPicker.Pick(slots, player, preferFar: true);

            Transform stacked = EncounterSpawnSlotPicker.Pick(slots, player, preferFar: false);
            Transform spread = EncounterSpawnSlotPicker.Pick(
                slots,
                player,
                preferFar: false,
                occupied: new Transform[] { near.transform });

            Assert.That(meleeSlot, Is.SameAs(near.transform));
            Assert.That(rangedSlot, Is.SameAs(far.transform));
            Assert.That(stacked, Is.SameAs(near.transform));
            Assert.That(spread, Is.SameAs(far.transform));
        }
        finally
        {
            Object.DestroyImmediate(near);
            Object.DestroyImmediate(far);
        }
    }

    [Test]
    public void PrefersFarSlotFollowsRangedHoldRole()
    {
        var melee = new GameObject("melee");
        var ranged = new GameObject("ranged");
        melee.AddComponent<Rigidbody2D>();
        ranged.AddComponent<Rigidbody2D>();
        try
        {
            melee.AddComponent<EnemyController>().CombatRole = EnemyCombatRole.InterruptibleMelee;
            ranged.AddComponent<EnemyController>().CombatRole = EnemyCombatRole.RangedHold;

            Assert.That(EncounterSpawnSlotPicker.PrefersFarSlot(melee), Is.False);
            Assert.That(EncounterSpawnSlotPicker.PrefersFarSlot(ranged), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    [Test]
    public void GeneratedHardEncounterCanExceedSpawnSlotCount()
    {
        var dummy = new GameObject("dummy-enemy");
        try
        {
            var profile = new RouteOfferTables.DifficultyProfile
            {
                minWaves = 3,
                maxWaves = 3,
                minEnemiesPerWave = 3,
                maxEnemiesPerWave = 3,
                enemyPool = new List<RouteOfferTables.EnemyEntry>
                {
                    new RouteOfferTables.EnemyEntry { prefab = dummy, weight = 1f }
                }
            };

            GeneratedEncounter encounter = RouteOfferGenerator.BuildEncounter(
                profile,
                0.35f,
                spawnSlotCount: 3,
                EncounterSpawnMode.Pressure,
                aliveCap: 3,
                spawnInterval: 0.35f);

            Assert.That(encounter.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
            Assert.That(encounter.AliveCap, Is.EqualTo(3));
            Assert.That(RouteOfferGenerator.CountEncounterEnemies(encounter), Is.EqualTo(9));
        }
        finally
        {
            Object.DestroyImmediate(dummy);
        }
    }

#if UNITY_EDITOR
    [Test]
    public void CombatEncounterAssetsUsePressureAndBossUsesBurst()
    {
        EncounterSO easy = UnityEditor.AssetDatabase.LoadAssetAtPath<EncounterSO>(
            "Assets/Project/Resources/EncounterSO/Encounter_Easy.asset");
        EncounterSO medium = UnityEditor.AssetDatabase.LoadAssetAtPath<EncounterSO>(
            "Assets/Project/Resources/EncounterSO/Encounter_Medium.asset");
        EncounterSO hard = UnityEditor.AssetDatabase.LoadAssetAtPath<EncounterSO>(
            "Assets/Project/Resources/EncounterSO/Encounter_Hard.asset");
        EncounterSO boss = UnityEditor.AssetDatabase.LoadAssetAtPath<EncounterSO>(
            "Assets/Project/Resources/EncounterSO/Encounter_Boss_Executioner.asset");

        Assert.That(easy, Is.Not.Null);
        Assert.That(medium, Is.Not.Null);
        Assert.That(hard, Is.Not.Null);
        Assert.That(boss, Is.Not.Null);
        Assert.That(easy.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
        Assert.That(medium.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
        Assert.That(hard.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
        Assert.That(easy.AliveCap, Is.GreaterThanOrEqualTo(1));
        Assert.That(easy.TypePickMode, Is.EqualTo(EncounterTypePickMode.ShuffleBag));
        Assert.That(easy.CollectPalettePrefabs().Count, Is.GreaterThan(0));
        Assert.That(medium.CollectPalettePrefabs().Count, Is.GreaterThan(0));
        Assert.That(hard.CollectPalettePrefabs().Count, Is.GreaterThan(0));
        Assert.That(boss.SpawnMode, Is.EqualTo(EncounterSpawnMode.Burst));
    }

    [Test]
    public void RouteOfferTablesIncludeEnemy3AndPressureCap()
    {
        RouteOfferTables tables = UnityEditor.AssetDatabase.LoadAssetAtPath<RouteOfferTables>(
            "Assets/Project/Resources/MapSO/RouteOfferTables.asset");
        Assert.That(tables, Is.Not.Null);
        Assert.That(tables.AliveCap, Is.EqualTo(3));

        GameObject enemy3 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/TestChar/Prefab/Enemy3.prefab");
        Assert.That(enemy3, Is.Not.Null);

        bool found = false;
        IReadOnlyList<RouteOfferTables.DifficultyProfile> profiles = tables.Profiles;
        for (int p = 0; p < profiles.Count; p++)
        {
            List<RouteOfferTables.EnemyEntry> pool = profiles[p].enemyPool;
            if (pool == null)
            {
                continue;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].prefab == enemy3)
                {
                    found = true;
                    break;
                }
            }
        }

        Assert.That(found, Is.True, "Enemy3 missing from RouteOfferTables pools.");
    }
#endif
}
#endif
