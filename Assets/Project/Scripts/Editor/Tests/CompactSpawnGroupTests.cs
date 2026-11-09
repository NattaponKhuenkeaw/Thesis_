#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CompactSpawnGroupTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string EnemyRoot = "Assets/Project/Character/enemy/enemy/";
    private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoot + path);

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void EveryChamberCanPlanFullCompactGroupsWithoutChangingItsLayout(int level)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)));
        try
        {
            foreach (var composite in root.GetComponentsInChildren<CompositeCollider2D>()) composite.GenerateGeometry();
            var stage = root.GetComponentInChildren<CombatStageController>();
            var boundary = root.GetComponentInChildren<SceneBoundaryController>();
            var planner = new EncounterSpawnGroupPlanner();
            var result = new List<EncounterSpawnGroupPlanner.Placement>();
            var basic = Load("Enm101/Enemy1.prefab");
            var flyer = Load("Enm102/Enemy102.prefab");
            var shield = Load("Enm103/Enemy103.prefab");
            var elite = Load("Enm101/Enemy101_Elite.prefab");
            for (int room = 0; room < stage.ChamberCount; room++)
            {
                var chamber = stage.Chambers[room];
                if (room > 0) boundary.OpenDivider(chamber.entranceWall);
                chamber.exitWall.enabled = true; chamber.exitWall.isTrigger = false;
                foreach (var wall in chamber.additionalExitWalls ?? new Collider2D[0]) { wall.enabled = true; wall.isTrigger = false; }
                Physics2D.SyncTransforms();
                string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                var references = new[] { chamber.spawnSlots[0].position, chamber.spawnSlots[chamber.spawnSlots.Length / 2].position,
                    chamber.spawnSlots[chamber.spawnSlots.Length - 1].position };
                foreach (var reference in references)
                foreach (var group in new[] { new[] { basic, basic, basic, basic }, new[] { basic, flyer, shield },
                    new[] { basic, flyer, shield, room == stage.ChamberCount - 1 ? elite : basic } })
                {
                    bool planned = planner.TryPlan(group, chamber.spawnSlots, reference, true, 2.4f, 16f, 3.5f, true, result);
                    Assert.That(planned, Is.True, $"LV{level} {chamber.name} player={reference} group={string.Join(",", group.Select(p => p.name))}");
                    Assert.That(result.Count, Is.EqualTo(group.Length));
                    Assert.That(result.Max(p => p.Body.max.x) - result.Min(p => p.Body.min.x), Is.LessThanOrEqualTo(16.001f));
                    for (int i = 0; i < result.Count; i++)
                    {
                        Assert.That(Mathf.Abs(result[i].Body.center.x - reference.x), Is.GreaterThanOrEqualTo(3.49f));
                        for (int j = i + 1; j < result.Count; j++)
                            Assert.That(Mathf.Abs(result[i].Body.center.x - result[j].Body.center.x), Is.GreaterThanOrEqualTo(2.39f));
                    }
                    Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physics));
                }
                boundary.OpenDivider(chamber.exitWall);
                foreach (var wall in chamber.additionalExitWalls ?? new Collider2D[0]) boundary.OpenDivider(wall);
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissingGroundRejectsTheEntirePlanWithoutConsumingQuota()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Unsupported cluster");
        try
        {
            var slots = new Transform[5];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GameObject("Spawn_" + i).transform;
                slots[i].SetParent(root.transform); slots[i].position = Vector3.right * i * 4f;
            }
            var basic = Load("Enm101/Enemy1.prefab");
            var quota = new EncounterPressureDirector(5, Enumerable.Repeat(basic, 8));
            var group = new List<GameObject>(); quota.CopyNext(4, group);
            var result = new List<EncounterSpawnGroupPlanner.Placement>();
            Assert.That(new EncounterSpawnGroupPlanner().TryPlan(group, slots, Vector3.zero, true, 2.4f, 16f, 3.5f, true, result), Is.False);
            Assert.That(result, Is.Empty);
            Assert.That(quota.Remaining, Is.EqualTo(8));
            Assert.That(quota.DequeueNext(), Is.SameAs(basic));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(false)] [TestCase(true)]
    public void AGroupCannotBorrowTheOtherSideOfAClosedWallOrAGap(bool gap)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Disconnected group fixture");
        try
        {
            System.Action<string, Vector2, Vector2> solid = (name, position, size) =>
            {
                var host = new GameObject(name); host.transform.SetParent(root.transform);
                host.layer = LayerMask.NameToLayer("Ground"); host.transform.position = position;
                host.AddComponent<BoxCollider2D>().size = size;
            };
            if (gap)
            {
                solid("Left floor", new Vector2(-5.5f, -.5f), new Vector2(7f, 1f));
                solid("Right floor", new Vector2(5.5f, -.5f), new Vector2(7f, 1f));
            }
            else
            {
                solid("Floor", new Vector2(0f, -.5f), new Vector2(20f, 1f));
                solid("Closed gate", new Vector2(0f, 3f), new Vector2(1f, 6f));
            }
            var xs = new[] { -8f, -6f, 6f, 8f };
            var slots = xs.Select(x =>
            {
                var slot = new GameObject("Spawn").transform; slot.SetParent(root.transform); slot.position = Vector3.right * x;
                return slot;
            }).ToArray();
            var group = Enumerable.Repeat(Load("Enm101/Enemy1.prefab"), 4).ToArray();
            var result = new List<EncounterSpawnGroupPlanner.Placement>();
            Physics2D.SyncTransforms();
            Assert.That(new EncounterSpawnGroupPlanner().TryPlan(group, slots, Vector3.zero, false, 2.4f, 16f, 3.5f, true, result), Is.False);
            Assert.That(result, Is.Empty);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator RealPrefabsSpawnAsFullGroupsInEveryLayoutAndRemainSeparatedAfterPlacement()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture = new GameObject("Compact runtime spawn audit");
        fixture.AddComponent<AudioListener>();
        fixture.AddComponent<RunProgressionManager>();
        var encounter = ScriptableObject.CreateInstance<EncounterSO>();
        using (var data = new SerializedObject(encounter))
        {
            data.FindProperty("spawnMode").enumValueIndex = (int)EncounterSpawnMode.Batched;
            data.FindProperty("aliveCap").intValue = 5;
            data.FindProperty("delayBetweenWaves").floatValue = .05f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject root = null;
        try
        {
            for (int level = 1; level <= 10; level++)
            {
                root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)), fixture.transform);
                foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(script is RoomWaveManager)) script.enabled = false;
                var stage = root.GetComponentInChildren<CombatStageController>(true);
                var boundary = root.GetComponentInChildren<SceneBoundaryController>(true);
                var waves = root.GetComponentInChildren<RoomWaveManager>();
                typeof(RoomWaveManager).GetField("roomCleared", Private).SetValue(waves, true);
                var active = (List<EnemyController>)typeof(RoomWaveManager).GetField("activeEnemies", Private).GetValue(waves);
                for (int room = 0; room < stage.ChamberCount; room++)
                {
                    var chamber = stage.Chambers[room];
                    if (room > 0) boundary.OpenDivider(chamber.entranceWall);
                    foreach (var composite in root.GetComponentsInChildren<CompositeCollider2D>()) composite.GenerateGeometry();
                    Physics2D.SyncTransforms();
                    waves.BindSpawnSlots(chamber.spawnSlots);
                    var group = new[] { Load("Enm101/Enemy1.prefab"), Load("Enm102/Enemy102.prefab"),
                        Load("Enm103/Enemy103.prefab"), Load("Enm101/Enemy101_Elite.prefab") };
                    waves.StartChamber(group, encounter);
                    Assert.That(active.Count(e => e != null && e.IsAlive), Is.EqualTo(4), $"LV{level} {chamber.name}: batch must open in full.");
                    int frame = Time.frameCount;
                    while (Time.frameCount < frame + 2) yield return null;
                    var living = active.Where(e => e != null && e.IsAlive).ToArray();
                    Assert.That(living.Length, Is.EqualTo(4));
                    Assert.That(living.Max(e => e.bodyCollider.bounds.max.x) - living.Min(e => e.bodyCollider.bounds.min.x), Is.LessThanOrEqualTo(16.05f));
                    for (int i = 0; i < living.Length; i++)
                    for (int j = i + 1; j < living.Length; j++)
                        Assert.That(Physics2D.Distance(living[i].bodyCollider, living[j].bodyCollider).isOverlapped, Is.False,
                            $"LV{level} {chamber.name}: {living[i].name}/{living[j].name}");
                    foreach (var enemy in living) enemy.Die();
                    frame = Time.frameCount;
                    while (Time.frameCount < frame + 2) yield return null;
                    boundary.OpenDivider(chamber.exitWall);
                    foreach (var wall in chamber.additionalExitWalls ?? new Collider2D[0]) boundary.OpenDivider(wall);
                }
                Object.DestroyImmediate(root); root = null;
            }
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(fixture); Object.DestroyImmediate(encounter);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
