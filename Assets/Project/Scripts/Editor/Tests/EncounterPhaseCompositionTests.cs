#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.ShopSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class EncounterPhaseCompositionTests
{
    private Random.State previousRandom;
    [SetUp] public void SetUp() => previousRandom = Random.state;
    [TearDown] public void TearDown() => Random.state = previousRandom;

    private static IEnumerable<TestCaseData> PhaseCases()
    {
        foreach (var difficulty in new[] { RouteDifficulty.Easy, RouteDifficulty.Medium, RouteDifficulty.Hard })
        {
            yield return new TestCaseData(difficulty, 0, 0, false, 0, 0);
            yield return new TestCaseData(difficulty, 0, 1, false, 1, 0);
            yield return new TestCaseData(difficulty, 0, 1, true, 1, 0);
            yield return new TestCaseData(difficulty, 0, 2, false, 1, 1);
            yield return new TestCaseData(difficulty, 0, 3, true, 1, 1);
            yield return new TestCaseData(difficulty, 1, 0, false, 0, 1);
            yield return new TestCaseData(difficulty, 1, 1, false, 1, 1);
            yield return new TestCaseData(difficulty, 1, 2, true, 1, 1);
            yield return new TestCaseData(difficulty, 2, 0, false, 1, 1);
            foreach (int biome in new[] { 2, 4 })
                yield return new TestCaseData(difficulty, biome, 0, true,
                    difficulty == RouteDifficulty.Hard ? 2 : 1,
                    difficulty == RouteDifficulty.Easy ? 1 : 2);
        }
    }

    [TestCaseSource(nameof(PhaseCases))]
    public void AuthoredDifficultyKeepsQuotaAndCapWhileIntroducingRolesByPhase(
        RouteDifficulty difficulty, int biome, int depth, bool rotationTeam, int ranged, int guards)
    {
        EncounterSO source = CombatRouteContent.LoadPalette(difficulty);
        string before = EditorJsonUtility.ToJson(source);
        int count = 4 + 2 * (int)difficulty;
        Assert.That(source.ChamberQuota, Is.EqualTo(count));
        Assert.That(source.AliveCap, Is.EqualTo(5));
        for (int seed = 0; seed < 20; seed++)
        {
            Random.InitState(seed);
            var quota = EncounterPhaseComposition.Roll(source, count, biome, depth, rotationTeam);
            AssertMix(quota, count, ranged, guards);
            Assert.That(quota.All(prefab => source.EnemyTypes.Any(entry => entry.prefab == prefab)), Is.True);
            var pressure = new EncounterPressureDirector(source.AliveCap, quota);
            int living = 0;
            int spawned = 0;
            while (pressure.Remaining > 0)
            {
                if (!pressure.CanSpawn(living)) living--;
                Assert.That(pressure.CanSpawn(living), Is.True);
                Assert.That(pressure.DequeueNext(), Is.Not.Null);
                spawned++;
                living++;
                Assert.That(living, Is.LessThanOrEqualTo(5));
            }
            Assert.That(spawned, Is.EqualTo(count));
            Assert.That(pressure.IsCleared(living), Is.False);
            Assert.That(pressure.IsCleared(0), Is.True);
        }
        Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before), "Do not mutate shared encounter assets");
    }

    [Test]
    public void RotationReadinessRequiresTwoLivingDeployedCharactersNotJustOwnership()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            var main = Resources.Load<Project.InventorySystem.Items.CharacterItem>("ItemSO/char_0_Main");
            var camera = Resources.Load<Project.InventorySystem.Items.CharacterItem>("ItemSO/char_1_CameraGIrl");
            inventory.TryAddItem(main);
            inventory.TryAddItem(camera);
            inventory.DeployTeam(new[] { main.characterObjectData }, 0);
            Assert.That(EncounterPhaseComposition.HasRotationTeam(inventory), Is.False);
            inventory.DeployTeam(new[] { main.characterObjectData, camera.characterObjectData }, 0);
            Assert.That(EncounterPhaseComposition.HasRotationTeam(inventory), Is.True);
            inventory.SetRunCharacterCurrentHP(camera.characterObjectData, 0f);
            Assert.That(EncounterPhaseComposition.HasRotationTeam(inventory), Is.False);
            inventory.SetRunCharacterCurrentHP(camera.characterObjectData, 100f);
            Assert.That(EncounterPhaseComposition.HasRotationTeam(inventory), Is.True);
            Assert.That(EncounterPhaseComposition.HasRotationTeam(null), Is.False);
        }
        finally { Object.DestroyImmediate(inventory); }
    }

    [TestCase(0, 0, 0, 0)]
    [TestCase(0, 1, 1, 0)]
    [TestCase(0, 2, 1, 1)]
    [TestCase(0, 3, 1, 1)]
    [TestCase(1, 0, 0, 1)]
    [TestCase(1, 2, 1, 1)]
    [TestCase(2, 0, 2, 2)]
    public void StageSelectionReadsTheCurrentRunAndDeployedTeam(int biome, int depth, int ranged, int guards)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var progressionInstance = typeof(RunProgressionManager).GetField("<Instance>k__BackingField", flags);
        var loaderInstance = typeof(TeamLoader).GetField("<Instance>k__BackingField", flags);
        var previousProgression = RunProgressionManager.Instance;
        var previousLoader = TeamLoader.Instance;
        var previousOffer = ActiveRouteOffer.Current;
        bool hadOffer = ActiveRouteOffer.HasSelection;
        var host = new GameObject("Phase selection context");
        host.SetActive(false);
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            ActiveRouteOffer.Clear();
            var progression = host.AddComponent<RunProgressionManager>();
            progression.currentBiomeIndex = biome;
            progression.currentRoomDepth = depth;
            progressionInstance.SetValue(null, progression);
            var loader = host.AddComponent<TeamLoader>();
            typeof(TeamLoader).GetField("runInventory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, inventory);
            loaderInstance.SetValue(null, loader);
            var main = Resources.Load<Project.InventorySystem.Items.CharacterItem>("ItemSO/char_0_Main");
            var camera = Resources.Load<Project.InventorySystem.Items.CharacterItem>("ItemSO/char_1_CameraGIrl");
            inventory.TryAddItem(main);
            inventory.TryAddItem(camera);
            inventory.DeployTeam(new[] { main.characterObjectData, camera.characterObjectData }, 0);
            var chamber = new CombatStageController.Chamber { encounter = CombatRouteContent.LoadPalette(RouteDifficulty.Hard) };
            var roll = typeof(CombatStageController).GetMethod("RollChamberQuota", flags);
            var quota = (List<GameObject>)roll.Invoke(null, new object[] { chamber, null, 8 });
            AssertMix(quota, 8, ranged, guards);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(inventory);
            progressionInstance.SetValue(null, previousProgression);
            loaderInstance.SetValue(null, previousLoader);
            if (hadOffer) ActiveRouteOffer.Set(previousOffer); else ActiveRouteOffer.Clear();
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(8)]
    public void BossWaveIsNotFilteredByCombatIntroductionRules(int biome)
    {
        var boss = Resources.Load<EncounterSO>("EncounterSO/Encounter_Boss_Executioner");
        var quota = EncounterPhaseComposition.Roll(boss, 1, biome, 0, false);
        Assert.That(quota.Count, Is.EqualTo(1));
        Assert.That(quota[0].GetComponent<EnemyController>().isEliteOrBoss, Is.True);
    }

    private static void AssertMix(IReadOnlyList<GameObject> quota, int count, int ranged, int guards)
    {
        Assert.That(quota.Count, Is.EqualTo(count));
        Assert.That(quota.Count(prefab => prefab.GetComponent<EnemyController>().UsesRangedAttack), Is.EqualTo(ranged));
        Assert.That(quota.Count(prefab => prefab.GetComponent<EnemyController>().CombatRole == EnemyCombatRole.ShieldGuard), Is.EqualTo(guards));
        Assert.That(count - ranged - guards, Is.GreaterThan(0), "Keep normal enemies as combo targets");
    }
}

public class EncounterPhaseRuntimeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator RealChamberSpawnsEveryPhaseQuotaAndNeverExceedsFiveLivingEnemies()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var context = new GameObject("Phase runtime context");
        var progression = context.AddComponent<RunProgressionManager>();
        var phases = new[] {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3),
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 0)
        };
        try
        {
            foreach (var phase in phases)
            foreach (var difficulty in new[] { RouteDifficulty.Easy, RouteDifficulty.Medium, RouteDifficulty.Hard })
            {
                progression.currentBiomeIndex = phase.x;
                progression.currentRoomDepth = phase.y;
                var root = Object.Instantiate(Resources.Load<GameObject>("RoomLayouts/LV/RoomLayout_LV_01"));
                var player = new GameObject("Phase trigger player");
                try
                {
                    var room = AssetDatabase.LoadAssetAtPath<RoomData>("Assets/Project/Resources/MapSO/LV/RoomData_LV_01.asset");
                    ActiveRouteOffer.Set(new RoomChoiceOffer(room, difficulty, null, null));
                    player.tag = "Player";
                    player.layer = LayerMask.NameToLayer("Player");
                    player.AddComponent<AudioListener>();
                    var body = player.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Kinematic;
                    var box = player.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(1.55f, 2.6f);
                    var stage = root.GetComponentInChildren<CombatStageController>();
                    var waves = root.GetComponentInChildren<RoomWaveManager>();
                    var trigger = waves.GetComponent<Collider2D>();
                    body.position = new Vector2(trigger.bounds.center.x, trigger.bounds.min.y + 2f);
                    Physics2D.SyncTransforms();
                    var seen = new HashSet<int>();
                    int ranged = 0, guards = 0;
                    int expected = 4 + 2 * (int)difficulty;
                    var active = typeof(RoomWaveManager).GetField("activeEnemies", Private);
                    var waiting = typeof(CombatStageController).GetField("waitingForIndex", Private);
                    float deadline = Time.realtimeSinceStartup + 12f;
                    while ((int)waiting.GetValue(stage) != 1 && Time.realtimeSinceStartup < deadline)
                    {
                        var enemies = ((List<EnemyController>)active.GetValue(waves)).Where(enemy => enemy != null && enemy.IsAlive).ToArray();
                        Assert.That(enemies.Length, Is.LessThanOrEqualTo(5));
                        foreach (var enemy in enemies)
                        {
                            if (seen.Add(enemy.GetInstanceID()))
                            {
                                if (enemy.UsesRangedAttack) ranged++;
                                if (enemy.CombatRole == EnemyCombatRole.ShieldGuard) guards++;
                            }
                            enemy.Die();
                        }
                        yield return null;
                    }
                    string label = $"Biome {phase.x} depth {phase.y} {difficulty}";
                    Assert.That(waiting.GetValue(stage), Is.EqualTo(1), label);
                    Assert.That(seen.Count, Is.EqualTo(expected), label);
                    int expectedRanged = phase.x == 0 ? (phase.y >= 1 ? 1 : 0)
                        : phase.x == 1 && phase.y == 0 ? 0 : 1;
                    int expectedGuards = phase.x == 0 ? (phase.y >= 2 ? 1 : 0) : 1;
                    Assert.That(ranged, Is.EqualTo(expectedRanged), label);
                    Assert.That(guards, Is.EqualTo(expectedGuards), label);
                }
                finally { ActiveRouteOffer.Clear(); Object.DestroyImmediate(root); Object.DestroyImmediate(player); }
                yield return null;
            }
        }
        finally { Object.DestroyImmediate(context); }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
