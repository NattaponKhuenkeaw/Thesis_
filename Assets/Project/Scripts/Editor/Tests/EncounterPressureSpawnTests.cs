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
    public void ClearancePickerSkipsSlotsCrowdedByLivingEnemies()
    {
        var near = new GameObject("near");
        var far = new GameObject("far");
        try
        {
            near.transform.position = new Vector3(1f, 0f, 0f);
            far.transform.position = new Vector3(10f, 0f, 0f);
            Transform[] slots = { near.transform, far.transform };
            Vector3 player = Vector3.zero;
            var occupiedByBody = new List<Vector3> { new Vector3(1.2f, 0f, 0f) };

            Transform melee = EncounterSpawnSlotPicker.PickWithClearance(
                slots,
                player,
                preferFar: false,
                blockedPositions: occupiedByBody,
                minSeparation: 1.8f,
                out bool isClear);

            Assert.That(melee, Is.SameAs(far.transform), "melee must avoid the slot a body is standing on");
            Assert.That(isClear, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(near);
            Object.DestroyImmediate(far);
        }
    }

    [Test]
    public void ClearancePickerReportsRoomiestSlotWhenEverySlotIsCrowded()
    {
        var near = new GameObject("near");
        var far = new GameObject("far");
        try
        {
            near.transform.position = new Vector3(1f, 0f, 0f);
            far.transform.position = new Vector3(10f, 0f, 0f);
            Transform[] slots = { near.transform, far.transform };
            var bodies = new List<Vector3>
            {
                new Vector3(1.1f, 0f, 0f),
                new Vector3(9.5f, 0f, 0f)
            };

            Transform picked = EncounterSpawnSlotPicker.PickWithClearance(
                slots,
                Vector3.zero,
                preferFar: false,
                blockedPositions: bodies,
                minSeparation: 1.8f,
                out bool isClear);

            Assert.That(isClear, Is.False, "caller must know it has to nudge the spawn");
            Assert.That(picked, Is.SameAs(far.transform), "roomiest slot wins when nothing is clear");
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
    [TestCase(RouteDifficulty.Easy, 8)]
    [TestCase(RouteDifficulty.Medium, 12)]
    [TestCase(RouteDifficulty.Hard, 16)]
    public void ProductionDifficultyRefillsFiveLivingEnemiesUntilTheDoubledQuotaIsExhausted(RouteDifficulty difficulty, int quota)
    {
        EncounterSO encounter = CombatRouteContent.LoadPalette(difficulty);
        Assert.That(encounter.ChamberQuota, Is.EqualTo(quota));
        Assert.That(encounter.AliveCap, Is.EqualTo(5));
        Assert.That(encounter.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
        var enemies = encounter.RollQuota(encounter.ChamberQuota);
        Assert.That(enemies.Count, Is.EqualTo(quota));
        var director = new EncounterPressureDirector(encounter.AliveCap, enemies);
        int living = 0;
        int spawned = 0;
        while (director.CanSpawn(living))
        {
            Assert.That(director.DequeueNext(), Is.Not.Null);
            living++;
            spawned++;
        }
        Assert.That(living, Is.EqualTo(5));
        Assert.That(director.CanSpawn(living), Is.False);
        while (director.Remaining > 0)
        {
            living--;
            Assert.That(director.CanSpawn(living), Is.True);
            Assert.That(director.DequeueNext(), Is.Not.Null);
            living++;
            spawned++;
            Assert.That(living, Is.EqualTo(5));
            Assert.That(director.CanSpawn(living), Is.False);
        }
        Assert.That(spawned, Is.EqualTo(quota));
        Assert.That(director.IsCleared(living), Is.False);
        Assert.That(director.IsCleared(0), Is.True);
    }

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
    public void RouteOfferTablesUseAuthoredEnemiesNotEnemyCore()
    {
        RouteOfferTables tables = UnityEditor.AssetDatabase.LoadAssetAtPath<RouteOfferTables>(
            "Assets/Project/Resources/MapSO/RouteOfferTables.asset");
        Assert.That(tables, Is.Not.Null);
        Assert.That(tables.AliveCap, Is.EqualTo(3));

        GameObject enemy1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab");
        GameObject enemy102 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab");
        GameObject enemy103 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab");
        GameObject coreEnemy2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/enemy/EnemyCore/Enemy2.prefab");
        GameObject coreEnemy3 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/enemy/EnemyCore/Enemy3.prefab");
        Assert.That(enemy1, Is.Not.Null);
        Assert.That(enemy102, Is.Not.Null);
        Assert.That(enemy103, Is.Not.Null);

        IReadOnlyList<RouteOfferTables.DifficultyProfile> profiles = tables.Profiles;
        Assert.That(profiles.Count, Is.GreaterThan(0));
        for (int p = 0; p < profiles.Count; p++)
        {
            List<RouteOfferTables.EnemyEntry> pool = profiles[p].enemyPool;
            Assert.That(pool, Is.Not.Null);
            Assert.That(PoolContains(pool, enemy1), Is.True, profiles[p].difficulty + " missing Enemy1.");
            Assert.That(PoolContains(pool, enemy102), Is.True, profiles[p].difficulty + " missing Enemy102.");
            Assert.That(PoolContains(pool, enemy103), Is.True, profiles[p].difficulty + " missing Enemy103.");
            Assert.That(PoolContains(pool, coreEnemy2), Is.False, profiles[p].difficulty + " still spawns EnemyCore Enemy2.");
            Assert.That(PoolContains(pool, coreEnemy3), Is.False, profiles[p].difficulty + " still spawns EnemyCore Enemy3.");
        }
    }

    private static bool PoolContains(List<RouteOfferTables.EnemyEntry> pool, GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].prefab == prefab)
            {
                return true;
            }
        }

        return false;
    }
#endif
}
#endif
