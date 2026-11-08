#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ProblemLayoutCombatTests
{
    [TestCase(4)] [TestCase(9)]
    public void EachWaveStaysOnOneReachableFloorAndInsideItsExit(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            Physics2D.SyncTransforms();
            foreach (var room in root.GetComponentInChildren<CombatStageController>().Chambers)
            {
                Assert.That(room.spawnSlots.Length, Is.EqualTo(5), room.name);
                Assert.That(room.spawnRoot.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Spawn_")), Is.EqualTo(5), "Extra markers remain in the hierarchy");
                Assert.That(room.spawnSlots.Max(s => s.position.y) - room.spawnSlots.Min(s => s.position.y),
                    Is.LessThan(.01f), room.name + " splits enemies across different floors");
                float span = room.spawnSlots.Max(s => s.position.x) - room.spawnSlots.Min(s => s.position.x);
                Assert.That(span, Is.InRange(10.8f, 30f), room.name + " needs clear, compact spawn space");
                var ordered = room.spawnSlots.OrderBy(s => s.position.x).ToArray();
                for (int n = 1; n < ordered.Length; n++)
                    Assert.That(ordered[n].position.x - ordered[n - 1].position.x, Is.GreaterThanOrEqualTo(2.7f));
                var sensor = room.enterTrigger != null ? room.enterTrigger : root.GetComponentInChildren<RoomWaveManager>().GetComponent<Collider2D>();
                Assert.That(room.spawnSlots.Min(s => s.position.x) - sensor.bounds.min.x, Is.GreaterThanOrEqualTo(2f),
                    room.name + " spawns directly on the entering player");
                foreach (var slot in room.spawnSlots)
                {
                    Assert.That(slot.position.x, Is.GreaterThan(room.entranceWall.bounds.max.x + 1.3f), room.name);
                    Assert.That(slot.position.x, Is.LessThan(room.exitWall.bounds.min.x - 1.3f), room.name);
                    var solids = Solids(root);
                    Assert.That(solids.Any(r => slot.position.x > r.xMin + 1.2f && slot.position.x < r.xMax - 1.2f
                        && Mathf.Abs(slot.position.y - r.yMax - .05f) < .06f), Is.True, room.name + " " + slot.name + " has no floor");
                    var air = new Rect(slot.position.x - 1.2f, slot.position.y + .1f, 2.4f, 7f);
                    Assert.That(solids.Any(r => r.Overlaps(air)), Is.False, room.name + " cannot jump above " + slot.name);
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(4)] [TestCase(9)]
    public void LowerRouteHasJumpHeadroomAndNoShortGateBypass(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            var solids = Solids(root);
            for (float x = 100f; x < 227f; x += .5f)
                Assert.That(solids.Any(r => r.Overlaps(new Rect(x, .1f, .1f, 7f))), Is.False, "Low ceiling at X=" + x);
            var gate = root.transform.Find("Boundaries/ChamberGates/ChamberGate_05").GetComponent<BoxCollider2D>();
            var ceiling = root.transform.Find("Boundaries/Ceiling").GetComponent<BoxCollider2D>();
            Assert.That(LayoutRoadAuthoring.WorldRect(gate).yMax, Is.EqualTo(LayoutRoadAuthoring.WorldRect(ceiling).yMin).Within(.01f));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(4)] [TestCase(9)]
    public void AllDifficultyCardsUseTheSamePalettesAsOtherLayouts(int level)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var roomData = Resources.Load<RoomData>($"MapSO/RoomData_Combat_{level:00}");
        // Biome 2 room IDs use the same numeric layout suffix.
        if (roomData == null)
            roomData = AssetDatabase.LoadAssetAtPath<RoomData>($"Assets/Project/Resources/MapSO/LV/RoomData_LV_{level:00}.asset");
        Assert.That(roomData, Is.Not.Null);
        var resolver = typeof(CombatStageController).GetMethod("ResolveEncounter", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            foreach (var difficulty in new[] { RouteDifficulty.Easy, RouteDifficulty.Medium, RouteDifficulty.Hard })
            {
                ActiveRouteOffer.Set(new RoomChoiceOffer(roomData, difficulty, null, null));
                foreach (var room in source.GetComponentInChildren<CombatStageController>().Chambers)
                {
                    var encounter = (EncounterSO)resolver.Invoke(null, new object[] { room });
                    Assert.That(encounter, Is.Not.Null);
                    Assert.That(encounter, Is.SameAs(CombatRouteContent.LoadPalette(difficulty)));
                }
            }
        }
        finally { ActiveRouteOffer.Clear(); }
    }

    [UnityTest]
    public IEnumerator PlayerCanEscapeBothLowerAreasUsingNormalSingleJumps()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (int level in new[] { 4, 9 })
        {
            var fixture = new GameObject("Saved LV" + level + " traversal");
            var source = Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}");
            var ground = Object.Instantiate(source.transform.Find("Terrain").gameObject, fixture.transform, false);
            ground.GetComponent<CompositeCollider2D>().GenerateGeometry();
            var platforms = Object.Instantiate(source.transform.Find("OneWayPlatforms").gameObject, fixture.transform, false);
            var actor = Actor(new Vector2(191.8f, .05f));
            try
            {
                var motor = actor.GetComponent<PlayerMovement>();
                yield return Settle(motor);
                Assert.That(motor.IsPhysicallyGrounded(), Is.True);
                var ladder = platforms.GetComponentsInChildren<BoxCollider2D>()
                    .Where(b => b.name.StartsWith("Recovery_")).OrderBy(b => b.bounds.max.y).ToArray();
                Assert.That(ladder.Length, Is.GreaterThanOrEqualTo(5));
                foreach (var step in ladder)
                {
                    motor.ConsumeJumpCount(); motor.ApplyJumpLaunch();
                    yield return Settle(motor);
                    Assert.That(motor.RB.position.y, Is.EqualTo(step.bounds.max.y).Within(.1f), $"LV{level} cannot return via {step.name}");
                    Assert.That(motor.IsPhysicallyGrounded(), Is.True);
                }
                motor.ConsumeJumpCount(); motor.ApplyJumpLaunch();
                motor.SetHorizontalInput(-1);
                float deadline = Time.realtimeSinceStartup + 3f;
                float landingX = level == 4 ? 183f : 184.5f;
                while (motor.RB.position.x > landingX && Time.realtimeSinceStartup < deadline) yield return null;
                motor.SetHorizontalInput(0);
                yield return Settle(motor);
                Assert.That(motor.RB.position.x, Is.LessThan(landingX + .3f));
                Assert.That(motor.RB.position.y, Is.EqualTo(level == 4 ? 17.32f : 22.12f).Within(.1f), "Cannot jump back to the rooftop");
                Assert.That(motor.IsPhysicallyGrounded(), Is.True);
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(fixture); }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EveryDifficultySpawnsItsSharedQuotaAndAdvancesWithFiveSlots()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (int level in new[] { 4, 9 })
        foreach (var difficulty in new[] { RouteDifficulty.Easy, RouteDifficulty.Medium, RouteDifficulty.Hard })
        {
            var root = Object.Instantiate(Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}"));
            var player = new GameObject("Hard encounter trigger player");
            try
            {
                var roomData = AssetDatabase.LoadAssetAtPath<RoomData>($"Assets/Project/Resources/MapSO/LV/RoomData_LV_{level:00}.asset");
                ActiveRouteOffer.Set(new RoomChoiceOffer(roomData, difficulty, null, null));
                var palette = CombatRouteContent.LoadPalette(difficulty);
                player.tag = "Player"; player.layer = LayerMask.NameToLayer("Player");
                player.AddComponent<AudioListener>();
                var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
                var box = player.AddComponent<BoxCollider2D>(); box.size = new Vector2(1.55f, 2.6f);
                var stage = root.GetComponentInChildren<CombatStageController>();
                var waves = root.GetComponentInChildren<RoomWaveManager>();
                var active = typeof(RoomWaveManager).GetField("activeEnemies", flags);
                var current = typeof(CombatStageController).GetField("currentIndex", flags);
                var waiting = typeof(CombatStageController).GetField("waitingForIndex", flags);
                int totalQuota = difficulty == RouteDifficulty.Hard ? 20 : difficulty == RouteDifficulty.Medium ? 16 : 12;
                for (int i = 0; i < stage.ChamberCount; i++)
                {
                    int quota = totalQuota / stage.ChamberCount + (i < totalQuota % stage.ChamberCount ? 1 : 0);
                    int opening = Mathf.Min(palette.AliveCap, quota);
                    var trigger = i == 0 ? waves.GetComponent<Collider2D>() : stage.Chambers[i].enterTrigger;
                    body.position = new Vector2(trigger.bounds.center.x, trigger.bounds.min.y + 2f);
                    Physics2D.SyncTransforms();
                    float deadline = Time.realtimeSinceStartup + 8f;
                    var enemies = (System.Collections.Generic.List<EnemyController>)active.GetValue(waves);
                    while (((int)current.GetValue(stage) != i || enemies.Count(e => e != null && e.IsAlive) < opening)
                        && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(current.GetValue(stage), Is.EqualTo(i));
                    Assert.That(enemies.Count(e => e != null && e.IsAlive), Is.EqualTo(opening), $"LV{level} {difficulty} Room{i + 1} has the wrong opening count");
                    var bodies = enemies.Where(e => e != null && e.IsAlive).ToArray();
                    for (int a = 0; a < bodies.Length; a++)
                        for (int b = a + 1; b < bodies.Length; b++)
                            Assert.That(Physics2D.Distance(bodies[a].bodyCollider, bodies[b].bodyCollider).distance,
                                Is.GreaterThan(-.03f), $"LV{level} Room{i + 1}: {bodies[a].name} {bodies[a].transform.position} / {bodies[b].name} {bodies[b].transform.position} overlap");
                    if (i == stage.ChamberCount - 1) break;
                    int defeated = 0;
                    deadline = Time.realtimeSinceStartup + 8f;
                    while ((int)waiting.GetValue(stage) != i + 1 && Time.realtimeSinceStartup < deadline)
                    {
                        foreach (var enemy in enemies.ToArray())
                            if (enemy != null && enemy.IsAlive) { enemy.Die(); defeated++; }
                        yield return null;
                    }
                    Assert.That(defeated, Is.EqualTo(quota), $"LV{level} {difficulty} Room{i + 1} did not exhaust its share of the room quota");
                    Assert.That(waiting.GetValue(stage), Is.EqualTo(i + 1), "Defeating the difficulty quota did not unlock the next chamber");
                    Assert.That(stage.Chambers[i].exitWall.isTrigger, Is.True);
                }
            }
            finally { ActiveRouteOffer.Clear(); Object.DestroyImmediate(root); Object.DestroyImmediate(player); }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    private static Rect[] Solids(GameObject root) => root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>()
        .Where(b => b.enabled && b.gameObject.activeInHierarchy).Select(LayoutRoadAuthoring.WorldRect).ToArray();

    private static GameObject Actor(Vector2 feet)
    {
        var actor = new GameObject("Real player motor"); actor.SetActive(false); actor.layer = LayerMask.NameToLayer("Player");
        actor.AddComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
        var box = actor.AddComponent<BoxCollider2D>(); box.size = new Vector2(1.55f, 2.6f); box.offset = Vector2.up * 1.3f;
        var check = new GameObject("Feet"); check.transform.SetParent(actor.transform, false);
        var motor = actor.AddComponent<PlayerMovement>(); motor.profile = Resources.Load<MovementProfile>("MovementSO/MovementProfile_Sample");
        typeof(PlayerMovement).GetField("groundCheck", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motor, check.transform);
        actor.transform.position = feet; actor.SetActive(true); Physics2D.SyncTransforms();
        return actor;
    }

    private static IEnumerator Settle(PlayerMovement motor)
    {
        float start = Time.time;
        while (Time.time - start < .15f) yield return null;
        float deadline = Time.realtimeSinceStartup + 3f;
        while ((!motor.IsPhysicallyGrounded() || Mathf.Abs(motor.RB.linearVelocity.y) > .1f) && Time.realtimeSinceStartup < deadline) yield return null;
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
