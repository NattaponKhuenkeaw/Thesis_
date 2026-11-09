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
    private const string ObserverPath = "Assets/Project/Character/enemy/Boss/Observer112/Boss112_Run.prefab";
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
    public void BossAttackAndDefenseStillAddTwentyPercentOfBasePerLevel()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(1), Is.EqualTo(1.20f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(2), Is.EqualTo(1.40f).Within(0.0001f));
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(3), Is.EqualTo(1.60f).Within(0.0001f));
    }

    [Test]
    public void NegativeBiomeClampsToBaseline()
    {
        Assert.That(RunProgressionManager.ResolveEnemyBasicStatMultiplier(-2), Is.EqualTo(1f));
        Assert.That(RunProgressionManager.ResolveBossHealthMultiplier(-2), Is.EqualTo(2f));
    }

    [TestCase(0, 2f)] [TestCase(1, 4f)] [TestCase(2, 6f)]
    [TestCase(3, 6.625f)] [TestCase(4, 7.25f)] [TestCase(5, 7.875f)]
    [TestCase(6, 8.5f)] [TestCase(7, 9.125f)] [TestCase(8, 9.75f)]
    [TestCase(9, 10.5f)] [TestCase(10, 10.5f)] [TestCase(99, 10.5f)]
    public void BossHealthUsesMilestonesAndCapsAfterLevelTen(int biome, float health)
    {
        progression.SetCurrentBiomeIndex(biome);
        Assert.That(progression.BossHealthMultiplier, Is.EqualTo(health).Within(.00001f));
    }

    [TestCase(-2, 1f, 1f)]
    [TestCase(0, 1f, 1f)]
    [TestCase(1, 1.18f, 1.15f)]
    [TestCase(2, 1.3924f, 1.3225f)]
    [TestCase(4, 1.93877776f, 1.74900625f)]
    [TestCase(9, 4.43545386f, 3.51787629f)]
    [TestCase(19, 4.43545386f, 14.23177165f)]
    public void NormalHealthCapsAfterLevelTenButAttackKeepsCompounding(int biome, float health, float attack)
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
            Assert.That(progression.NormalEnemyHealthMultiplier, Is.EqualTo(Mathf.Pow(1.18f, biome)).Within(.00001f));
            Assert.That(progression.NormalEnemyAttackMultiplier, Is.EqualTo(Mathf.Pow(1.15f, biome)).Within(.00001f));
            Assert.That(progression.EnemyBasicStatMultiplier, Is.EqualTo(1f + biome * .2f).Within(.00001f));
            Assert.That(progression.BossHealthMultiplier, Is.EqualTo(RunProgressionManager.ResolveBossHealthMultiplier(biome)));
            progression.NotifyRoomCleared(bossRoom);
        }
        progression.ResetRun();
        Assert.That(progression.CurrentBiomeIndex, Is.Zero);
        Assert.That(progression.NormalEnemyHealthMultiplier, Is.EqualTo(1f));
        Assert.That(progression.NormalEnemyAttackMultiplier, Is.EqualTo(1f));
        Assert.That(progression.EnemyBasicStatMultiplier, Is.EqualTo(1f));
        Assert.That(progression.BossHealthMultiplier, Is.EqualTo(2f));
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

    [TestCase(BossPath, 0)] [TestCase(BossPath, 1)] [TestCase(BossPath, 2)] [TestCase(BossPath, 4)] [TestCase(BossPath, 5)]
    [TestCase(ObserverPath, 0)] [TestCase(ObserverPath, 1)] [TestCase(ObserverPath, 2)] [TestCase(ObserverPath, 4)] [TestCase(ObserverPath, 5)]
    public void BothBossesUseHealthMilestonesAndLinearAttackAndDefense(string path, int biome)
    {
        progression.SetCurrentBiomeIndex(biome);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyController>();
        Assert.That(source.maxHP, Is.EqualTo(4000f));
        string authored = EditorJsonUtility.ToJson(source);
        var enemy = Object.Instantiate(source.gameObject, holder.transform).GetComponent<EnemyController>();
        RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
        float scale = 1f + biome * .2f;
        Assert.That(enemy.HasBossDriver, Is.True);
        Assert.That(enemy.maxHP, Is.EqualTo(4000f * RunProgressionManager.ResolveBossHealthMultiplier(biome)).Within(.01f));
        Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP));
        Assert.That(enemy.attackDamage, Is.EqualTo(source.attackDamage * scale).Within(.001f));
        Assert.That(enemy.defense, Is.EqualTo(source.defense * scale).Within(.001f));
        Assert.That(enemy.damageRes, Is.EqualTo(source.damageRes));
        Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(authored), "Runtime scaling must not write back into the prefab.");
    }

    [TestCase(BossPath, 0)] [TestCase(BossPath, 1)] [TestCase(BossPath, 4)]
    [TestCase(ObserverPath, 0)] [TestCase(ObserverPath, 1)] [TestCase(ObserverPath, 4)]
    public void ScalingRefreshesTheBoundFiveLayerHudUsingTheNewMaximum(string path, int biome)
    {
        progression.SetCurrentBiomeIndex(biome);
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder.transform).GetComponent<EnemyController>();
        var root = Object.Instantiate(Resources.Load<GameObject>("UI/BossHealthHUD"));
        var hud = root.GetComponent<Project.UI.BossHealthHUD>();
        var instance = typeof(Project.UI.BossHealthHUD).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var previousHud = instance.GetValue(null);
        instance.SetValue(null, hud);
        try
        {
            hud.Bind(enemy, "Scaled boss", 5);
            RunProgressionManager.ApplyBiomeDifficultyToEnemy(enemy);
            var slider = (UnityEngine.UI.Slider)typeof(Project.UI.BossHealthHUD).GetField("healthSlider", Private).GetValue(hud);
            var bars = (TMPro.TMP_Text)typeof(Project.UI.BossHealthHUD).GetField("remainingBarsText", Private).GetValue(hud);
            Assert.That(slider.value, Is.EqualTo(1f));
            Assert.That(bars.text, Is.EqualTo("\u00d75"));
            enemy.currentHP = enemy.maxHP * .7f;
            hud.RefreshIfBound(enemy);
            Assert.That(slider.value, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(bars.text, Is.EqualTo("\u00d74"));
        }
        finally
        {
            Object.DestroyImmediate(root);
            instance.SetValue(null, previousHud);
        }
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
    [TestCase(ObserverPath)]
    public void WaveManagerAppliesTheCorrectCurveDuringSpawn(string path)
    {
        const int biome = 2;
        progression.SetCurrentBiomeIndex(biome);
        bool boss = path == BossPath || path == ObserverPath;
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
                Assert.That(enemy.maxHP, Is.EqualTo(source.maxHP * 6f).Within(.01f));
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
        Assert.That(enemy.maxHP, Is.EqualTo(source.maxHP * Mathf.Pow(1.18f, Mathf.Clamp(biome, 0, 9))).Within(.001f));
        Assert.That(enemy.currentHP, Is.EqualTo(enemy.maxHP));
        Assert.That(enemy.attackDamage, Is.EqualTo(source.attackDamage * Mathf.Pow(1.15f, biome)).Within(.001f));
        Assert.That(enemy.defense, Is.EqualTo(source.defense));
        Assert.That(enemy.damageRes, Is.EqualTo(source.damageRes));
        Assert.That(enemy.Kit, Is.SameAs(source.Kit));
    }
}
#endif
