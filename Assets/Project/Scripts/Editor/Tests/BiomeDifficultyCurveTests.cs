#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class BiomeDifficultyCurveTests
{
    private const string EnemyFolder = "Assets/Project/Character/enemy/enemy/";
    private const string BossPath = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo InstanceField = typeof(RunProgressionManager)
        .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
    private GameObject holder;
    private RunProgressionManager progression;
    private RunProgressionManager previous;

    [SetUp]
    public void SetUp()
    {
        previous = RunProgressionManager.Instance;
        holder = new GameObject("Inactive difficulty fixture");
        holder.SetActive(false);
        progression = holder.AddComponent<RunProgressionManager>();
        InstanceField.SetValue(null, progression);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(holder);
        InstanceField.SetValue(null, previous);
    }

    [Test]
    public void BiomeZeroIsBaseline()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(0), Is.EqualTo(1f));
    }

    [Test]
    public void BossCurveStillAddsTwentyPercentOfBasePerLevel()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(1), Is.EqualTo(1.20f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(2), Is.EqualTo(1.40f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(3), Is.EqualTo(1.60f).Within(0.0001f));
    }

    [Test]
    public void NegativeBiomeClampsToBaseline()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(-2), Is.EqualTo(1f));
    }

    [TestCase(-2, 1f, 1f)]
    [TestCase(0, 1f, 1f)]
    [TestCase(1, 1.4f, 1.15f)]
    [TestCase(2, 1.96f, 1.3225f)]
    [TestCase(3, 2.744f, 1.520875f)]
    [TestCase(4, 3.8416f, 1.74900625f)]
    [TestCase(5, 5.37824f, 2.01135719f)]
    public void NormalEnemyHealthAndAttackCompoundFromLevelOne(int biome, float health, float attack)
    {
        progression.SetCurrentBiomeIndex(biome);
        Assert.That(progression.NormalEnemyHealthMultiplier, Is.EqualTo(health).Within(.00001f));
        Assert.That(progression.NormalEnemyAttackMultiplier, Is.EqualTo(attack).Within(.00001f));
    }

    [TestCase(-.5f)]
    [TestCase(0f)]
    public void NonPositiveGrowthBonusesCannotReduceBaseStats(float bonus)
    {
        Assert.That(RunProgressionManager.ResolveNormalEnemyHealthMultiplier(5, bonus), Is.EqualTo(1f));
        Assert.That(RunProgressionManager.ResolveNormalEnemyAttackMultiplier(5, bonus), Is.EqualTo(1f));
    }

    [Test]
    public void BossClearsAdvanceBothCurvesAndNewRunsRestoreTheirBaselines()
    {
        var bossRoom = Resources.Load<RoomData>("MapSO/RoomData_Boss_01");
        Assert.That(bossRoom, Is.Not.Null);
        for (int biome = 0; biome < 6; biome++)
        {
            Assert.That(progression.CurrentBiomeIndex, Is.EqualTo(biome));
            Assert.That(progression.NormalEnemyHealthMultiplier, Is.EqualTo(Mathf.Pow(1.4f, biome)).Within(.00001f));
            Assert.That(progression.NormalEnemyAttackMultiplier, Is.EqualTo(Mathf.Pow(1.15f, biome)).Within(.00001f));
            Assert.That(progression.EnemyBasicStatMultiplier, Is.EqualTo(1f + biome * .2f).Within(.00001f));
            progression.NotifyRoomCleared(bossRoom);
        }
        progression.ResetRun();
        Assert.That(progression.CurrentBiomeIndex, Is.Zero);
        Assert.That(progression.NormalEnemyHealthMultiplier, Is.EqualTo(1f));
        Assert.That(progression.NormalEnemyAttackMultiplier, Is.EqualTo(1f));
        Assert.That(progression.EnemyBasicStatMultiplier, Is.EqualTo(1f));
    }

    [TestCase("Enm101/Enemy1.prefab", 0)]
    [TestCase("Enm101/Enemy1.prefab", 1)]
    [TestCase("Enm101/Enemy1.prefab", 2)]
    [TestCase("Enm101/Enemy1.prefab", 4)]
    [TestCase("Enm101/Enemy1.prefab", 5)]
    [TestCase("Enm102/Enemy102.prefab", 0)]
    [TestCase("Enm102/Enemy102.prefab", 1)]
    [TestCase("Enm102/Enemy102.prefab", 2)]
    [TestCase("Enm102/Enemy102.prefab", 4)]
    [TestCase("Enm102/Enemy102.prefab", 5)]
    [TestCase("Enm103/Enemy103.prefab", 0)]
    [TestCase("Enm103/Enemy103.prefab", 1)]
    [TestCase("Enm103/Enemy103.prefab", 2)]
    [TestCase("Enm103/Enemy103.prefab", 4)]
    [TestCase("Enm103/Enemy103.prefab", 5)]
    public void ProductionEnemyPrefabsReceiveSeparateScalingWithoutChangingTheirAssets(string path, int biome)
    {
        progression.SetCurrentBiomeIndex(biome);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + path).GetComponent<EnemyController>();
        var enemy = Object.Instantiate(source.gameObject, holder.transform).GetComponent<EnemyController>();
        RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
        AssertNormalStats(enemy, source, biome);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    public void ActualBossStillReceivesItsOriginalLinearScaling(int biome)
    {
        progression.SetCurrentBiomeIndex(biome);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).GetComponent<EnemyController>();
        var enemy = Object.Instantiate(source.gameObject, holder.transform).GetComponent<EnemyController>();
        RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
        float scale = 1f + biome * .2f;
        Assert.That(enemy.maxHP, Is.EqualTo(source.maxHP * scale).Within(.001f));
        Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP));
        Assert.That(enemy.attackDamage, Is.EqualTo(source.attackDamage * scale).Within(.001f));
        Assert.That(enemy.defense, Is.EqualTo(source.defense * scale).Within(.001f));
        Assert.That(enemy.damageRes, Is.EqualTo(source.damageRes));
    }

    [Test]
    public void AnEliteFlagAloneDoesNotMisclassifyNormalEnemiesAsBosses()
    {
        progression.SetCurrentBiomeIndex(2);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + "Enm101/Enemy1.prefab")
            .GetComponent<EnemyController>();
        var enemy = Object.Instantiate(source.gameObject, holder.transform).GetComponent<EnemyController>();
        enemy.isEliteOrBoss = true;
        RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
        AssertNormalStats(enemy, source, 2);
    }

    [TestCase("Enm101/Enemy1.prefab")]
    [TestCase("Enm102/Enemy102.prefab")]
    [TestCase("Enm103/Enemy103.prefab")]
    [TestCase(BossPath)]
    public void WaveManagerAppliesTheCorrectCurveDuringSpawn(string path)
    {
        const int biome = 2;
        progression.SetCurrentBiomeIndex(biome);
        bool boss = path == BossPath;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(boss ? path : EnemyFolder + path)
            .GetComponent<EnemyController>();
        var template = Object.Instantiate(source.gameObject, holder.transform);
        template.SetActive(false);
        holder.AddComponent<BoxCollider2D>().isTrigger = true;
        var waves = holder.AddComponent<RoomWaveManager>();
        var enemies = (List<EnemyController>)typeof(RoomWaveManager).GetField("activeEnemies", Private).GetValue(waves);
        try
        {
            object result = typeof(RoomWaveManager).GetMethod("TrySpawnPrefab", Private)
                .Invoke(waves, new object[] { template, false, holder.transform });
            Assert.That(result.ToString(), Is.EqualTo("Spawned"));
            Assert.That(enemies.Count, Is.EqualTo(1));
            var enemy = enemies[0];
            if (boss)
            {
                Assert.That(enemy.maxHP, Is.EqualTo(source.maxHP * 1.4f).Within(.001f));
                Assert.That(enemy.attackDamage, Is.EqualTo(source.attackDamage * 1.4f).Within(.001f));
                Assert.That(enemy.defense, Is.EqualTo(source.defense * 1.4f).Within(.001f));
            }
            else AssertNormalStats(enemy, source, biome);
            Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP));
        }
        finally
        {
            foreach (var enemy in enemies)
                if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            enemies.Clear();
        }
    }

    private static void AssertNormalStats(EnemyController enemy, EnemyController source, int biome)
    {
        Assert.That(enemy.maxHP, Is.EqualTo(source.maxHP * Mathf.Pow(1.4f, biome)).Within(.001f));
        Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP));
        Assert.That(enemy.attackDamage, Is.EqualTo(source.attackDamage * Mathf.Pow(1.15f, biome)).Within(.001f));
        Assert.That(enemy.defense, Is.EqualTo(source.defense));
        Assert.That(enemy.damageRes, Is.EqualTo(source.damageRes));
        Assert.That(enemy.Kit, Is.SameAs(source.Kit));
    }
}
#endif
