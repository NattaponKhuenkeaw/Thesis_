#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using Project.Core.ZoneState;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class Boss112EntranceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void CorridorHasContinuousFloorAnOpenArenaGateAndATriggerBeyondTheStopPoint()
    {
        var room = Resources.Load<RoomData>("MapSO/RoomData_Boss_02");
        Assert.That(room.UseStaticCamera, Is.False, "The entry corridor must use the normal player camera.");
        var root = PrefabUtility.LoadPrefabContents(Boss112RunAuthoring.ArenaPath);
        try
        {
            Physics2D.SyncTransforms();
            var entrance = root.GetComponentInChildren<EntrancePoint>();
            var corridor = root.transform.Find("EntranceCorridorFloor").GetComponent<BoxCollider2D>();
            var floor = root.transform.Find("ArenaTerrain/Floor").GetComponent<BoxCollider2D>();
            var gate = root.transform.Find("ArenaTerrain/LeftWall").GetComponent<BoxCollider2D>();
            var wall = root.transform.Find("ArenaTerrain/RightWall").GetComponent<BoxCollider2D>();
            var trigger = root.transform.Find("BossSpawnTrigger").GetComponent<BoxCollider2D>();
            var boundary = root.GetComponent<SceneBoundaryController>();
            using var data = new SerializedObject(boundary);
            Assert.That(data.FindProperty("_entranceWall").objectReferenceValue,
                Is.SameAs(root.transform.Find("StageStartWall").GetComponent<BoxCollider2D>()));
            Assert.That(data.FindProperty("_fightEntranceWall").objectReferenceValue, Is.SameAs(gate));
            Assert.That(gate.isTrigger, Is.True);
            Assert.That(wall.bounds.min.x - gate.bounds.max.x, Is.EqualTo(ExecutionerArenaWidth()).Within(.001f));
            Assert.That(floor.bounds.min.x, Is.LessThanOrEqualTo(gate.bounds.min.x + .001f));
            Assert.That(floor.bounds.max.x, Is.GreaterThanOrEqualTo(wall.bounds.max.x - .001f));
            Assert.That(corridor.bounds.max.y, Is.EqualTo(floor.bounds.max.y).Within(.001f));
            Assert.That(corridor.bounds.min.x, Is.LessThan(entrance.SpawnPosition.x - 2f));
            Assert.That(corridor.bounds.max.x, Is.GreaterThan(floor.bounds.min.x));
            Assert.That(trigger.bounds.min.x, Is.GreaterThan(gate.bounds.max.x + 2f));
            Assert.That(trigger.bounds.min.x, Is.GreaterThan(entrance.StopPosition.x + 2f));
            Assert.That(trigger.bounds.Contains(entrance.SpawnPosition), Is.False);
            Assert.That(trigger.bounds.Contains(entrance.StopPosition), Is.False);
            for (float x = entrance.SpawnPosition.x; x < trigger.bounds.center.x; x += .25f)
                Assert.That(x >= corridor.bounds.min.x && x <= corridor.bounds.max.x
                    || x >= floor.bounds.min.x && x <= floor.bounds.max.x, Is.True, $"Unsupported entrance X={x}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static float ExecutionerArenaWidth()
    {
        var reference = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(
            Resources.Load<RoomData>("MapSO/RoomData_Boss_01").LayoutPrefab));
        try
        {
            Physics2D.SyncTransforms();
            var walls = reference.GetComponentsInChildren<BoxCollider2D>(true);
            return walls.Single(c => c.name == "ExitWall").bounds.min.x
                - walls.Single(c => c.name == "ArenaGate").bounds.max.x;
        }
        finally { PrefabUtility.UnloadPrefabContents(reference); }
    }

    [UnityTest] public IEnumerator MainCanWalkInBeforeStartingTheBoss()
        => WalkIntoArena("test_0_Main/CharacterObject_main");
    [UnityTest] public IEnumerator CameragirlCanWalkInBeforeStartingTheBoss()
        => WalkIntoArena("test_1_CameraGirl/CharacterObject_cg");

    private static IEnumerator WalkIntoArena(string character)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("Boss 112 entrance fixture");
        var provider = ScriptableObject.CreateInstance<ZoneStateProvider>();
        var player = PlayerIntroRevealTests.CreatePlayer(false, out var profile);
        player.transform.SetParent(root.transform); player.tag = "Player";
        var groundCheck = new GameObject("Ground check").transform; groundCheck.SetParent(player.transform, false);
        typeof(PlayerMovement).GetField("groundCheck", Private).SetValue(player.Movement, groundCheck);
        var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab"), root.transform);
        var manager = rig.GetComponent<CameraManager>();
        typeof(CameraManager).GetField("_zoneStateProvider", Private).SetValue(manager, provider);
        typeof(CameraManager).GetField("_playerTransform", Private).SetValue(manager, player.transform);
        var host = new GameObject("Layout host"); host.transform.SetParent(root.transform);
        var layout = host.AddComponent<RoomLayoutHost>();
        try
        {
            Assert.That(layout.MountRoom(Resources.Load<RoomData>("MapSO/RoomData_Boss_02")), Is.True);
            var arena = layout.ActiveLayoutInstance;
            var entrance = arena.GetComponentInChildren<EntrancePoint>();
            var camera = arena.GetComponentInChildren<CinemachineCamera>();
            var gate = arena.transform.Find("ArenaTerrain/LeftWall").GetComponent<BoxCollider2D>();
            var boundary = arena.GetComponent<SceneBoundaryController>();
            Assert.That(camera.enabled, Is.False);
            Assert.That(gate.isTrigger, Is.True);
            player.transform.position = entrance.SpawnPosition;
            var driver = PlayerIntroRevealTests.AttachRealAnimator(player, character);
            player.Movement.RB.constraints = RigidbodyConstraints2D.FreezeRotation;
            Physics2D.SyncTransforms();
            boundary.PrepareForIntro(entrance);
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance); player.StateMachine.ChangeState(intro); intro.BeginVisibleWalk();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (player.StateMachine.CurrentStateId == PlayerStateId.Intro)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                Assert.That(player.transform.position.y, Is.GreaterThan(-.15f));
                Assert.That(Object.FindFirstObjectByType<Boss112Controller>(), Is.Null);
                yield return null;
            }
            Assert.That(player.transform.position.x, Is.EqualTo(entrance.StopPosition.x).Within(.3f));
            Assert.That(player.StateMachine.IsCinematicTraversal, Is.False);
            float stoppedAt = Time.time;
            while (Time.time - stoppedAt < .4f) yield return null;
            Assert.That(Object.FindFirstObjectByType<Boss112Controller>(), Is.Null, "Stopping at the entrance must not start the fight.");
            Assert.That(camera.enabled, Is.False);
            deadline = Time.realtimeSinceStartup + 10f;
            Boss112Controller boss;
            while ((boss = Object.FindFirstObjectByType<Boss112Controller>()) == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Player was blocked before reaching the fight trigger.");
                Assert.That(player.transform.position.y, Is.GreaterThan(-.15f), "Player fell through the corridor floor.");
                player.StateMachine.CurrentState.OnMoveInput(Vector2.right);
                yield return null;
            }
            player.StateMachine.CurrentState.OnMoveInput(Vector2.zero);
            Assert.That(player.transform.position.x - .775f, Is.GreaterThan(gate.bounds.max.x));
            Assert.That(gate.isTrigger, Is.False, "The arena gate must seal behind the player, not in front of them.");
            Assert.That(camera.enabled, Is.True);
            Assert.That(boss.IsSpawnRevealing, Is.True);
            Assert.That(boss.Phase, Is.EqualTo(Boss112Phase.Idle));
            Assert.That(driver.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(profile); Object.DestroyImmediate(provider);
            if (Project.UI.BossHealthHUD.Instance != null) Object.DestroyImmediate(Project.UI.BossHealthHUD.Instance.gameObject);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
