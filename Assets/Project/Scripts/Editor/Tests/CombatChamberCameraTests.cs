#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class CombatChamberCameraTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void BindingFirstChamberReplacesTheWholeStageCameraBounds(int level)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefab/Camera/CameraSetUp.prefab"));
        var layout = Object.Instantiate(Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}"));
        try
        {
            var stage = layout.GetComponentInChildren<CombatStageController>();
            var boundary = layout.GetComponentInChildren<SceneBoundaryController>();
            var first = stage.Chambers[0];
            var last = stage.Chambers[stage.ChamberCount - 1];
            Physics2D.SyncTransforms();
            Assert.That(boundary.BindChamberWalls(first.entranceWall, last.exitWall), Is.True);
            Assert.That(boundary.BindChamberWalls(first.entranceWall, first.exitWall), Is.True);
            Physics2D.SyncTransforms();
            foreach (var confiner in rig.GetComponentsInChildren<CinemachineConfiner2D>())
            {
                Assert.That(confiner.BoundingShape2D.bounds.max.x,
                    Is.EqualTo(first.exitWall.bounds.min.x).Within(.01f), $"LV{level} {confiner.name}");
            }
        }
        finally { Object.DestroyImmediate(layout); Object.DestroyImmediate(rig); }
    }

    [UnityTest]
    public IEnumerator Lv1FirstChamberKeepsTheExploreViewportBehindTheClosedGate()
        => VerifyFirstChamber(false, 16f / 9f);

    [UnityTest]
    public IEnumerator CombatEntrancesKeepThePlayerOnScreenBeforeCombat()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab"));
        var manager = rig.GetComponent<CameraManager>();
        var output = rig.GetComponentInChildren<Camera>();
        var explore = rig.transform.Find("CinemachineCamera_Explore").GetComponent<CinemachineCamera>();
        var confiner = explore.GetComponent<CinemachineConfiner2D>();
        float authoredSize = explore.Lens.OrthographicSize;
        var provider = ScriptableObject.CreateInstance<ZoneStateProvider>();
        typeof(CameraManager).GetField("_zoneStateProvider", Private).SetValue(manager, provider);
        var player = new GameObject("Combat entrance camera target") { tag = "Player" };
        var body = player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        try
        {
            foreach (float aspect in new[] { 16f / 9f, 21f / 9f })
            {
                output.aspect = aspect;
                for (int level = 1; level <= 10; level++)
                {
                    provider.SetCurrentRoom(Resources.Load<RoomData>($"MapSO/RoomData_Combat_{level:00}"));
                    var layout = Object.Instantiate(Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}"));
                    try
                    {
                        var firstGate = layout.GetComponentInChildren<CombatStageController>().Chambers[0].exitWall;
                        float deadline = Time.realtimeSinceStartup + 2f;
                        while (Mathf.Abs(confiner.BoundingShape2D.bounds.max.x - firstGate.bounds.min.x) > .01f)
                        {
                            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"LV{level:00} did not bind its first gate.");
                            yield return null;
                        }
                        var entrance = layout.GetComponentInChildren<EntrancePoint>();
                        Vector2 previous = body.position;
                        body.position = entrance.StopPosition;
                        Physics2D.SyncTransforms();
                        manager.SnapToPlayerForRoomEnter(body.position - previous);
                        float revealAt = Time.time + .15f;
                        while (Time.time < revealAt) yield return null;
                        var viewport = output.WorldToViewportPoint((Vector3)body.position + Vector3.up);
                        Assert.That(manager.IsInCombat, Is.False);
                        Assert.That(viewport.y, Is.InRange(.05f, .95f),
                            $"LV{level:00} aspect={aspect:F2} player={body.position}, camera={output.transform.position}, output size={output.orthographicSize}, explore size={explore.Lens.OrthographicSize}, bounds={confiner.BoundingShape2D.bounds}");
                        var bounds = confiner.BoundingShape2D.bounds;
                        float halfWidth = output.orthographicSize * output.aspect;
                        Assert.That(output.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(bounds.min.x - .03f));
                        Assert.That(output.transform.position.x + halfWidth, Is.LessThanOrEqualTo(bounds.max.x + .03f));
                        if (bounds.size.x >= 2f * (authoredSize + .05f) * aspect)
                            Assert.That(explore.Lens.OrthographicSize, Is.EqualTo(authoredSize).Within(.001f));
                    }
                    finally { Object.DestroyImmediate(layout); }
                    yield return null;
                }
            }
            provider.SetCurrentRoom(Resources.Load<RoomData>("MapSO/RoomData_Boss_02"));
            yield return null;
            Assert.That(explore.Lens.OrthographicSize, Is.EqualTo(authoredSize).Within(.001f),
                "A narrow combat room must not leave the boss entrance zoomed in.");
        }
        finally
        {
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(provider);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator Lv1FirstChamberKeepsTheCombatViewportBehindTheClosedGate()
        => VerifyFirstChamber(true, 16f / 9f);

    [UnityTest]
    public IEnumerator Lv1FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate()
        => VerifyFirstChamber(true, 21f / 9f);

    [UnityTest] public IEnumerator Lv2FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 2);
    [UnityTest] public IEnumerator Lv3FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 3);
    [UnityTest] public IEnumerator Lv4FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 4);
    [UnityTest] public IEnumerator Lv5FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 5);
    [UnityTest] public IEnumerator Lv6FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 6);
    [UnityTest] public IEnumerator Lv7FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 7);
    [UnityTest] public IEnumerator Lv8FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 8);
    [UnityTest] public IEnumerator Lv9FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 9);
    [UnityTest] public IEnumerator Lv10FirstChamberKeepsTheWideCombatViewportBehindTheClosedGate() => VerifyFirstChamber(true, 21f / 9f, 10);

    private static IEnumerator VerifyFirstChamber(bool combat, float aspect, int level = 1)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefab/Camera/CameraSetUp.prefab"));
        var player = new GameObject("Chamber camera player");
        var body = player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerMovement>().enabled = false;
        var taggedChild = new GameObject(combat ? "HitBox" : "Visual") { tag = "Player" };
        taggedChild.transform.SetParent(player.transform, false);
        if (combat) taggedChild.AddComponent<Rigidbody2D>().simulated = false;
        var output = rig.GetComponentInChildren<Camera>();
        output.aspect = aspect;
        var manager = rig.GetComponent<CameraManager>();
        var provider = ScriptableObject.CreateInstance<ZoneStateProvider>();
        provider.SetCurrentRoom(Resources.Load<RoomData>($"MapSO/RoomData_Combat_{level:00}"));
        typeof(CameraManager).GetField("_zoneStateProvider", Private).SetValue(manager, provider);
        var layout = Object.Instantiate(Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}"));
        var stage = layout.GetComponentInChildren<CombatStageController>();
        var boundary = layout.GetComponentInChildren<SceneBoundaryController>();
        var first = stage.Chambers[0];
        var enemyObject = new GameObject("Uncleared chamber enemy");
        try
        {
            Physics2D.SyncTransforms();
            boundary.BindChamberWalls(first.entranceWall, first.exitWall);
            float right = first.exitWall.bounds.min.x;
            body.position = new Vector2(right - 1f, 1.5f);
            var lookAhead = rig.GetComponentInChildren<CameraLookAhead>();
            yield return null;
            Assert.That(lookAhead.playerRigidbody, Is.SameAs(body), "Camera must follow the motor, not the tagged child.");
            lookAhead.SnapToPlayer();
            if (combat)
            {
                enemyObject.transform.position = new Vector3(right - 4f, 1.5f, 0f);
                enemyObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var enemy = enemyObject.AddComponent<EnemyController>();
                yield return null;
                EnemyLifecycleTestUtility.Activate(enemy);
                enemy.SetInCombat(true);
            }
            for (int frame = 0; frame < 90; frame++) yield return null;
            var brain = rig.GetComponentInChildren<CinemachineBrain>();
            var confiner = rig.GetComponentsInChildren<CinemachineConfiner2D>().First();
            var bounds = (BoxCollider2D)confiner.BoundingShape2D;
            Assert.That(first.exitWall.enabled, Is.True);
            Assert.That(boundary.IsExitAvailable, Is.False);
            Assert.That(bounds.bounds.max.x, Is.EqualTo(right).Within(.01f), "First chamber camera bounds changed before clear");
            float viewRight = output.transform.position.x + output.orthographicSize * output.aspect;
            Assert.That(viewRight, Is.LessThanOrEqualTo(right + .03f),
                $"{brain.ActiveVirtualCamera?.Name}: viewport right={viewRight}, gate={right}, size={output.orthographicSize}, combat={manager.IsInCombat}");
            if (combat) Assert.That(manager.IsInCombat, Is.True);
            else
            {
                float previousX = output.transform.position.x;
                float left = first.entranceWall.bounds.max.x;
                body.position = new Vector2(Mathf.Lerp(left, right, .25f), body.position.y);
                float deadline = Time.realtimeSinceStartup + 3f;
                while (output.transform.position.x >= previousX - 1f && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(output.transform.position.x, Is.LessThan(previousX - 1f),
                    "The actual viewport must follow the player back into the chamber.");
            }
        }
        finally
        {
            Object.DestroyImmediate(layout);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(provider);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualRoomClearsOpenTheCameraOnlyTowardTheNextClosedGateInEveryLayout()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var current = typeof(CombatStageController).GetField("currentIndex", Private);
        var waiting = typeof(CombatStageController).GetField("waitingForIndex", Private);
        for (int level = 1; level <= 10; level++)
        {
            var player = new GameObject("Chamber progression player") { tag = "Player", layer = LayerMask.NameToLayer("Player") };
            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var playerCollider = player.AddComponent<BoxCollider2D>();
            playerCollider.size = new Vector2(1.55f, 2.6f);
            body.position = new Vector2(-1000f, 1000f);
            var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab"));
            var output = rig.GetComponentInChildren<Camera>();
            output.aspect = 16f / 9f;
            var provider = ScriptableObject.CreateInstance<ZoneStateProvider>();
            provider.SetCurrentRoom(Resources.Load<RoomData>($"MapSO/RoomData_Combat_{level:00}"));
            typeof(CameraManager).GetField("_zoneStateProvider", Private).SetValue(rig.GetComponent<CameraManager>(), provider);
            var layout = Object.Instantiate(Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}"));
            try
            {
                var stage = layout.GetComponentInChildren<CombatStageController>();
                var waves = layout.GetComponentInChildren<RoomWaveManager>();
                var boundary = layout.GetComponentInChildren<SceneBoundaryController>();
                var bounds = rig.GetComponentsInChildren<CinemachineConfiner2D>().First().BoundingShape2D;
                var lookAhead = rig.GetComponentInChildren<CameraLookAhead>();
                lookAhead.playerRigidbody = body;
                Physics2D.SyncTransforms();
                var entry = waves.GetComponent<Collider2D>().bounds;
                body.position = new Vector2(entry.center.x, entry.min.y + 7f);
                for (int room = 0; room < stage.ChamberCount; room++)
                {
                    float deadline = Time.realtimeSinceStartup + 6f;
                    while ((int)current.GetValue(stage) != room && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(current.GetValue(stage), Is.EqualTo(room), $"LV{level} Room{room + 1}: entrance did not start encounter");
                    var chamber = stage.Chambers[room];
                    float right = chamber.exitWall.bounds.min.x;
                    Assert.That(chamber.exitWall.isTrigger, Is.False);
                    Assert.That(boundary.IsExitAvailable, Is.False);
                    body.position = new Vector2(right - 1.1f, chamber.spawnSlots.Average(s => s.position.y) + 1.3f);
                    lookAhead.SnapToPlayer();
                    // Batch-mode frames can run much faster than the timed corridor expansion.
                    deadline = Time.realtimeSinceStartup + 4f;
                    var slide = typeof(SceneBoundaryController).GetField("_boundSlideRoutine", Private);
                    while (slide.GetValue(boundary) != null && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(slide.GetValue(boundary), Is.Null, $"LV{level} Room{room + 1}: camera expansion did not finish");
                    for (int frame = 0; frame < 90; frame++) yield return null;
                    Assert.That(bounds.bounds.max.x, Is.EqualTo(right).Within(.03f), $"LV{level} Room{room + 1}: bounds cross the closed gate");
                    float viewportRight = output.transform.position.x + output.orthographicSize * output.aspect;
                    Assert.That(viewportRight, Is.LessThanOrEqualTo(right + .05f),
                        $"LV{level} Room{room + 1}: viewport={viewportRight}, gate={right}, size={output.orthographicSize}");
                    Assert.That(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Any(e => e.IsAlive), Is.True);
                    if (room == stage.ChamberCount - 1) break;

                    deadline = Time.realtimeSinceStartup + 6f;
                    while ((int)waiting.GetValue(stage) != room + 1 && Time.realtimeSinceStartup < deadline)
                    {
                        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                            if (enemy.IsAlive) enemy.Die();
                        yield return null;
                    }
                    Assert.That(waiting.GetValue(stage), Is.EqualTo(room + 1), $"LV{level} Room{room + 1}: clear did not open the corridor");
                    Assert.That(chamber.exitWall.isTrigger, Is.True);
                    entry = stage.Chambers[room + 1].enterTrigger.bounds;
                    body.position = new Vector2(entry.center.x, entry.min.y + 7f);
                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                }
                Debug.Log($"[Chamber camera audit] LV{level}: all {stage.ChamberCount} chambers retained their closed forward camera edge.");
            }
            finally
            {
                Object.DestroyImmediate(layout);
                foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    Object.DestroyImmediate(enemy.gameObject);
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(provider);
            }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
