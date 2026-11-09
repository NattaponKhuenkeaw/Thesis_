#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EncounterBatchSpawnTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void GeneratedFallbackOffersKeepTheirQuotaAndFiveEnemyCap()
    {
        var room = ScriptableObject.CreateInstance<RoomData>();
        var previous = UnityEngine.Random.state;
        try
        {
            using (var data = new SerializedObject(room))
            {
                data.FindProperty("roomKind").enumValueIndex = (int)RoomKind.Combat;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int seed = 0; seed < 12; seed++)
            {
                UnityEngine.Random.InitState(seed);
                var offer = RouteOfferGenerator.BuildOffer(room, spawnSlotCount: 5);
                Assert.That(offer.Encounter, Is.Not.Null);
                Assert.That(offer.Encounter.SpawnMode, Is.EqualTo(EncounterSpawnMode.Pressure));
                Assert.That(offer.Encounter.AliveCap, Is.EqualTo(5));
                Assert.That(RouteOfferGenerator.CountEncounterEnemies(offer.Encounter), Is.EqualTo(4 + 2 * (int)offer.Difficulty));
            }
        }
        finally { UnityEngine.Random.state = previous; Object.DestroyImmediate(room); }
    }

    [UnityTest]
    public IEnumerator QuotasSpawnInBalancedGroupsAndNeverRefillWhileOneSurvives()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var plans = new Dictionary<int, int[]>
        {
            { 2, new[] { 2 } },
            { 3, new[] { 3 } },
            { 4, new[] { 4 } },
            { 5, new[] { 5 } },
            { 6, new[] { 3, 3 } },
            { 7, new[] { 4, 3 } },
            { 8, new[] { 4, 4 } },
            { 12, new[] { 4, 4, 4 } }
        };
        foreach (var plan in plans)
        {
            int quota = plan.Key;
            using (var f = new Fixture(5))
            {
                if (quota == 5) f.AddRoomySlots();
                f.Start(quota, quota == 12 ? 99 : 5);
                var seen = new HashSet<int>();
                int remaining = quota;
                foreach (int expected in plan.Value)
                {
                    Assert.That(expected, Is.InRange(2, 5));
                    yield return Until(() => f.Living.Length == expected);
                    var group = f.Living;
                    foreach (var enemy in group) Assert.That(seen.Add(enemy.GetInstanceID()), Is.True);
                    for (int i = 0; i < group.Length - 1; i++)
                    {
                        group[i].Die();
                        yield return Wait(.06f);
                        Assert.That(f.Living.Length, Is.EqualTo(expected - i - 1), "Partial kills must not refill this group.");
                        Assert.That(f.Pending, Is.EqualTo(remaining - expected));
                        Assert.That(f.Running, Is.True, "Do not clear or advance a chamber with living or queued enemies.");
                    }
                    group[group.Length - 1].Die();
                    remaining -= expected;
                }
                yield return Until(() => !f.Running);
                Assert.That(f.Living, Is.Empty);
                Assert.That(f.Pending, Is.Zero);
                Assert.That(seen.Count, Is.EqualTo(quota));
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PressureLayoutsSpawnTheFullSelectedCardBudgetAtAuthoredMarkers()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var context = new GameObject("Route spawn budget audit");
        context.AddComponent<AudioListener>();
        var progression = context.AddComponent<RunProgressionManager>();
        var player = new GameObject("Chamber entry probe") { tag = "Player" };
        player.transform.SetParent(context.transform);
        player.transform.position = Vector3.one * -1000f;
        var entry = player.AddComponent<BoxCollider2D>();
        var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
        var activeField = typeof(RoomWaveManager).GetField("activeEnemies", Private);
        var originsField = typeof(RoomWaveManager).GetField("spawnOrigins", Private);
        var pendingField = typeof(RoomWaveManager).GetField("pressureDirector", Private);
        var routineField = typeof(RoomWaveManager).GetField("waveRoutine", Private);
        var waitingField = typeof(CombatStageController).GetField("waitingForIndex", Private);
        var layouts = new[] { 5, 2, 5, 3, 1 };
        try
        {
            for (int depth = 0; depth < layouts.Length; depth++)
            {
                var room = map.GetRoomById($"Combat_{layouts[depth]:00}");
                var difficulty = depth == 0 ? RouteDifficulty.Medium : RouteDifficulty.Hard;
                int[] expected = layouts[depth] == 1 ? new[] { 7, 7, 6 }
                    : depth == 0 ? new[] { 4, 4, 4, 4 } : new[] { 5, 5, 5, 5 };
                progression.currentRoomDepth = depth;
                ActiveRouteOffer.Set(new RoomChoiceOffer(room, difficulty, null, null));
                var layout = Object.Instantiate(room.LayoutPrefab);
                try
                {
                    var stage = layout.GetComponentInChildren<CombatStageController>();
                    var waves = layout.GetComponentInChildren<RoomWaveManager>();
                    var active = (List<EnemyController>)activeField.GetValue(waves);
                    var origins = (Dictionary<EnemyController, Vector3>)originsField.GetValue(waves);
                    yield return null;
                    waves.StartEncounter();
                    Assert.That(stage.ChamberCount, Is.EqualTo(expected.Length));
                    int total = 0;
                    for (int i = 0; i < stage.ChamberCount; i++)
                    {
                        var markers = stage.Chambers[i].spawnSlots.Where(slot => slot != null).Select(slot => slot.position);
                        int spawned = 0;
                        while (spawned < expected[i])
                        {
                            yield return Until(() => active.Any(e => e != null && e.IsAlive));
                            var living = active.Where(e => e != null && e.IsAlive).ToArray();
                            Assert.That(living.Length, Is.InRange(1, 5), $"{room.RoomId} {difficulty}, chamber {i + 1}");
                            foreach (var enemy in living)
                            {
                                Assert.That(markers, Does.Contain(origins[enemy]), "Pressure must use the authored markers.");
                                enemy.Die();
                            }
                            spawned += living.Length;
                        }
                        Assert.That(spawned, Is.EqualTo(expected[i]));
                        Assert.That(((EncounterPressureDirector)pendingField.GetValue(waves)).Remaining, Is.Zero);
                        total += spawned;
                        if (i + 1 < stage.ChamberCount)
                        {
                            yield return Until(() => (int)waitingField.GetValue(stage) == i + 1);
                            stage.NotifyPlayerEntered(i + 1, entry);
                        }
                    }
                    yield return Until(() => routineField.GetValue(waves) == null);
                    Assert.That(total, Is.EqualTo(depth == 0 ? 16 : 20), room.RoomId);
                }
                finally { Object.DestroyImmediate(layout); }
            }
        }
        finally
        {
            ActiveRouteOffer.Clear();
            Object.DestroyImmediate(context);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PressureRefillsOneVacancyWhileTheOtherFourAreAlive()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture(5))
        {
            f.Start(7, cap: 99, mode: EncounterSpawnMode.Pressure);
            var opening = f.Living;
            Assert.That(opening.Length, Is.EqualTo(5));
            Assert.That(f.Pending, Is.EqualTo(2));
            opening[0].Die();
            yield return Until(() => f.Living.Length == 5);
            Assert.That(f.Pending, Is.EqualTo(1));
            Assert.That(opening.Skip(1).All(e => f.Living.Contains(e)), Is.True);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CrowdedSlotsWaitForACompleteGroupInsteadOfChangingTheFourPlusFourPlan()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture(3))
        {
            f.Start(8);
            Assert.That(f.Living, Is.Empty);
            Assert.That(f.Pending, Is.EqualTo(8));
            yield return Wait(.2f);
            Assert.That(f.Living, Is.Empty);
            Assert.That(f.Pending, Is.EqualTo(8));
            f.AddRoomySlots();
            yield return Until(() => f.Living.Length == 4);
            Assert.That(f.Pending, Is.EqualTo(4));
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => f.Living.Length == 4);
            Assert.That(f.Pending, Is.Zero);
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => !f.Running);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DisabledSpawnersCancelPendingGroupsAndRestartWithOnlyTheNewQuota()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture(5))
        {
            f.Start(8);
            Assert.That(f.Living.Length, Is.EqualTo(4));
            f.Waves.enabled = false;
            yield return Wait(.2f);
            Assert.That(f.Living, Is.Empty);
            f.Waves.enabled = true;
            f.Start(6);
            Assert.That(f.Living.Length, Is.EqualTo(3));
            Assert.That(f.Pending, Is.EqualTo(3));
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => f.Living.Length == 3);
            yield return Wait(.15f);
            Assert.That(f.Living.Length, Is.EqualTo(3));
            Assert.That(f.Pending, Is.Zero);
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => !f.Running);
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "The expected batch transition did not occur.");
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("Batch spawn fixture");
        private readonly EncounterSO encounter;
        private readonly GameObject prefab;
        public readonly RoomWaveManager Waves;
        public EnemyController[] Living => ((List<EnemyController>)typeof(RoomWaveManager).GetField("activeEnemies", Private)
            .GetValue(Waves)).Where(e => e != null && e.IsAlive).ToArray();
        public int Pending => ((EncounterPressureDirector)typeof(RoomWaveManager).GetField("pressureDirector", Private)
            .GetValue(Waves)).Remaining;
        public bool Running => typeof(RoomWaveManager).GetField("waveRoutine", Private).GetValue(Waves) != null;

        public Fixture(int slotCount)
        {
            root.AddComponent<AudioListener>();
            var context = new GameObject("Progression"); context.transform.SetParent(root.transform, false);
            context.AddComponent<RunProgressionManager>().currentBiomeIndex = 0;
            var floor = new GameObject("Floor"); floor.transform.SetParent(root.transform, false);
            floor.layer = LayerMask.NameToLayer("Ground"); floor.transform.position = new Vector3(1000f, -.5f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            var host = new GameObject("Spawner"); host.transform.SetParent(root.transform, false);
            host.transform.position = Vector3.right * 1000f;
            host.AddComponent<BoxCollider2D>().isTrigger = true;
            var slots = new Transform[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var marker = new GameObject("Spawn_" + i); marker.transform.SetParent(host.transform, false);
                marker.transform.localPosition = Vector3.right * ((i - (slotCount - 1) * .5f) * 4f);
                slots[i] = marker.transform;
            }
            Waves = host.AddComponent<RoomWaveManager>(); Waves.BindSpawnSlots(slots);
            typeof(RoomWaveManager).GetField("roomCleared", Private).SetValue(Waves, true);
            using (var data = new SerializedObject(Waves))
            {
                data.FindProperty("maxSpawnNudge").floatValue = 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            encounter = ScriptableObject.CreateInstance<EncounterSO>();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab");
            Physics2D.SyncTransforms();
        }

        public void Start(int count, int cap = 5, EncounterSpawnMode mode = EncounterSpawnMode.Batched)
        {
            using (var data = new SerializedObject(encounter))
            {
                data.FindProperty("spawnMode").enumValueIndex = (int)mode;
                data.FindProperty("aliveCap").intValue = cap;
                data.FindProperty("delayBetweenWaves").floatValue = .05f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Waves.StartChamber(Enumerable.Repeat(prefab, count).ToArray(), encounter);
        }

        public void AddRoomySlots()
        {
            var slots = new Transform[5];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GameObject("Roomy_" + i).transform;
                slots[i].SetParent(Waves.transform, false);
                slots[i].localPosition = Vector3.right * ((i - 2) * 3f);
            }
            Waves.BindSpawnSlots(slots);
            Physics2D.SyncTransforms();
        }

        public void Dispose() { Object.DestroyImmediate(root); Object.DestroyImmediate(encounter); }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
