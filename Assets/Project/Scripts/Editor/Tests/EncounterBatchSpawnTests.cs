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
    public void GeneratedFallbackOffersAlsoUseFiveEnemyBatchesWithoutChangingTheQuota()
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
                Assert.That(offer.Encounter.SpawnMode, Is.EqualTo(EncounterSpawnMode.Batched));
                Assert.That(offer.Encounter.AliveCap, Is.EqualTo(5));
                Assert.That(RouteOfferGenerator.CountEncounterEnemies(offer.Encounter), Is.EqualTo(4 + 2 * (int)offer.Difficulty));
            }
        }
        finally { UnityEngine.Random.state = previous; Object.DestroyImmediate(room); }
    }

    [UnityTest]
    public IEnumerator QuotasSpawnInGroupsOfFiveAndNeverRefillWhileOneSurvives()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (int quota in new[] { 4, 6, 8, 12 })
        {
            using (var f = new Fixture(5))
            {
                f.Start(quota, quota == 12 ? 99 : 5);
                var seen = new HashSet<int>();
                int remaining = quota;
                while (remaining > 0)
                {
                    int expected = Mathf.Min(5, remaining);
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
    public IEnumerator CrowdedSlotsCloseASmallerGroupInsteadOfAddingEnemiesWhenOneDies()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture(3))
        {
            f.Start(8);
            Assert.That(f.Living.Length, Is.EqualTo(3));
            Assert.That(f.Pending, Is.EqualTo(5));
            var first = f.Living;
            first[0].Die();
            yield return Wait(.2f);
            Assert.That(f.Living.Length, Is.EqualTo(2));
            Assert.That(f.Pending, Is.EqualTo(5), "A free marker must wait until the entire group is defeated.");
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => f.Living.Length == 3);
            Assert.That(f.Pending, Is.EqualTo(2));
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => f.Living.Length == 2);
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
            Assert.That(f.Living.Length, Is.EqualTo(5));
            f.Waves.enabled = false;
            yield return Wait(.2f);
            Assert.That(f.Living, Is.Empty);
            f.Waves.enabled = true;
            f.Start(6);
            Assert.That(f.Living.Length, Is.EqualTo(5));
            Assert.That(f.Pending, Is.EqualTo(1));
            foreach (var enemy in f.Living) enemy.Die();
            yield return Until(() => f.Living.Length == 1);
            yield return Wait(.15f);
            Assert.That(f.Living.Length, Is.EqualTo(1));
            Assert.That(f.Pending, Is.Zero);
            f.Living[0].Die();
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

        public void Start(int count, int cap = 5)
        {
            using (var data = new SerializedObject(encounter))
            {
                data.FindProperty("spawnMode").enumValueIndex = (int)EncounterSpawnMode.Batched;
                data.FindProperty("aliveCap").intValue = cap;
                data.FindProperty("delayBetweenWaves").floatValue = .05f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Waves.StartChamber(Enumerable.Repeat(prefab, count).ToArray(), encounter);
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
