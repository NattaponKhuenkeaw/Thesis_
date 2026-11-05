#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class LayoutExitSafetyAuditTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable]
    private class Row
    {
        public int level;
        public string character;
        public float exitX, exitY, entryX, entryY, entryGroundY;
        public bool entryHasGround, stopHasGround, approachGrounded, touchesExit, afterExitGrounded;
        public float approachX, approachY, targetX, finalX, finalY, maximumDrop, dropBeforeFadeEnds;
        public bool exitFinished;
    }

    [Serializable] private class Report { public List<Row> rows = new List<Row>(); }

    [Serializable]
    private class SupportRow
    {
        public int level;
        public float exitX, exitY, floorRightEdge, gapBeforeExit;
        public bool supportedAtExit, supportedAtWalkTarget;
    }
    [Serializable] private class SupportReport { public List<SupportRow> rows = new List<SupportRow>(); }

    [UnityTest]
    public IEnumerator MeasureTheActualCollisionFloorAlongEveryExitEdge()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var report = new SupportReport();
        for (int level = 1; level <= 10; level++)
        {
            var source = Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}");
            var geometry = new GameObject("Exit collision strip audit");
            Object.Instantiate(source.transform.Find("Terrain").gameObject, geometry.transform, false);
            var platforms = source.transform.Find("OneWayPlatforms");
            if (platforms != null) Object.Instantiate(platforms.gameObject, geometry.transform, false);
            try
            {
                Physics2D.SyncTransforms();
                var exit = LayoutRoadAuthoring.WorldRect(source.transform.Find("StageExitTrigger").GetComponent<BoxCollider2D>());
                var row = new SupportRow { level = level, exitX = exit.xMax, exitY = exit.yMin, floorRightEdge = exit.xMax - 4f };
                row.supportedAtExit = ExitFloorAt(exit.xMax, exit.yMin);
                row.supportedAtWalkTarget = ExitFloorAt(exit.xMax + 5f, exit.yMin);
                bool seenFloor = false;
                for (int sample = 0; sample <= 480; sample++)
                {
                    float x = exit.xMax - 4f + sample * .025f;
                    if (ExitFloorAt(x, exit.yMin)) { seenFloor = true; row.floorRightEdge = x; }
                    else if (seenFloor) break;
                }
                row.gapBeforeExit = row.exitX - row.floorRightEdge;
                report.rows.Add(row);
                Debug.Log("[Exit collision strip] " + JsonUtility.ToJson(row));
            }
            finally { Object.DestroyImmediate(geometry); }
            yield return null;
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/layout-exit-collision-strips.json", JsonUtility.ToJson(report, true));
        Assert.That(report.rows.Count, Is.EqualTo(10));
        yield return new ExitPlayMode();
    }

    private static bool ExitFloorAt(float x, float y)
    {
        var hit = Physics2D.Raycast(new Vector2(x, y + .25f), Vector2.down, .5f, LayerMask.GetMask("Ground", "OneWayPlatform"));
        return hit.collider != null && hit.normal.y > .5f;
    }

    [UnityTest]
    public IEnumerator AuditBothPlayableCharactersAtEverySavedLayoutExitWithoutEditingMaps()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var report = new Report();
        float pastDistance = (float)typeof(SceneTransitionManager).GetField("ExitWalkPastDistance", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        float maxDistance = (float)typeof(SceneTransitionManager).GetField("ExitWalkMaxDistance", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        for (int level = 1; level <= 10; level++)
        {
            string path = $"Assets/Project/Resources/RoomLayouts/LV/RoomLayout_LV_{level:00}.prefab";
            byte[] saved = File.ReadAllBytes(path);
            var source = Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}");
            var geometry = new GameObject("Read-only exit geometry audit");
            Object.Instantiate(source.transform.Find("Terrain").gameObject, geometry.transform, false);
            var platforms = source.transform.Find("OneWayPlatforms");
            if (platforms != null) Object.Instantiate(platforms.gameObject, geometry.transform, false);
            Physics2D.SyncTransforms();
            var exit = LayoutRoadAuthoring.WorldRect(source.transform.Find("StageExitTrigger").GetComponent<BoxCollider2D>());
            var entrance = source.GetComponentInChildren<EntrancePoint>(true);
            try
            {
                foreach (string character in new[] { "test_0_Main/CharacterObject_main", "test_1_CameraGirl/CharacterObject_cg" })
                {
                    var row = new Row { level = level, character = character, exitX = exit.xMax, exitY = exit.yMin };
                    row.entryX = entrance.SpawnPosition.x; row.entryY = entrance.SpawnPosition.y;
                    row.entryHasGround = GroundBelow(entrance.SpawnPosition, out row.entryGroundY);
                    row.stopHasGround = GroundBelow(entrance.StopPosition, out _);
                    var player = CreatePlayer(character, new Vector2(exit.xMax - 3f, exit.yMin + .08f));
                    try
                    {
                        var motor = player.Movement;
                        float until = Time.time + .6f;
                        while (Time.time < until) yield return null;
                        float stopX = exit.xMax - motor.GetBodyExtentToward(1f) - .04f;
                        float deadline = Time.realtimeSinceStartup + 3f;
                        while (Mathf.Abs(motor.RB.position.x - stopX) > .08f && Time.realtimeSinceStartup < deadline)
                        {
                            motor.SetHorizontalInput(Mathf.Clamp((stopX - motor.RB.position.x) / (9.5f * Mathf.Max(Time.deltaTime, .02f)), -1f, 1f));
                            yield return null;
                        }
                        motor.SetHorizontalInput(0f);
                        row.approachX = motor.RB.position.x; row.approachY = motor.RB.position.y;
                        row.approachGrounded = motor.IsPhysicallyGrounded();
                        var body = player.GetComponent<BoxCollider2D>().bounds;
                        row.touchesExit = new Rect(body.min, body.size).Overlaps(exit);
                        row.targetX = RoomBoundaryMath.ResolveLeaveWalkTargetX(row.approachX, exit.xMax, 1f, pastDistance, maxDistance);
                        var state = player.StateMachine.ExitState;
                        state.TargetPosition = new Vector3(row.targetX, row.approachY);
                        player.StateMachine.ChangeState(state);
                        float start = Time.time;
                        float minimumY = row.approachY;
                        deadline = Time.realtimeSinceStartup + 3f;
                        while (!state.FinishedNormally && Time.realtimeSinceStartup < deadline)
                        {
                            yield return null;
                            minimumY = Mathf.Min(minimumY, motor.RB.position.y);
                            if (Time.time - start <= .15f)
                                row.dropBeforeFadeEnds = Mathf.Max(row.dropBeforeFadeEnds, row.approachY - motor.RB.position.y);
                        }
                        row.exitFinished = state.FinishedNormally;
                        row.finalX = motor.RB.position.x; row.finalY = motor.RB.position.y;
                        row.maximumDrop = row.approachY - minimumY;
                        row.afterExitGrounded = motor.IsPhysicallyGrounded();
                        report.rows.Add(row);
                        Debug.Log("[Exit safety audit] " + JsonUtility.ToJson(row));
                    }
                    finally { Object.DestroyImmediate(player.gameObject); }
                    yield return null;
                }
            }
            finally
            {
                Object.DestroyImmediate(geometry);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved), "The audit must not rewrite the saved layout");
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/layout-exit-safety-audit.json", JsonUtility.ToJson(report, true));
        Assert.That(report.rows.Count, Is.EqualTo(20));
        yield return new ExitPlayMode();
    }

    private static bool GroundBelow(Vector3 position, out float groundY)
    {
        var hit = Physics2D.Raycast(position + Vector3.up * .2f, Vector2.down, 40f, LayerMask.GetMask("Ground", "OneWayPlatform"));
        groundY = hit.collider != null ? hit.point.y : 0f;
        return hit.collider != null;
    }

    private static PlayerCharacter CreatePlayer(string character, Vector2 position)
    {
        var host = new GameObject("Actual exit-state physics audit"); host.SetActive(false);
        host.layer = LayerMask.NameToLayer("Player"); host.transform.position = position;
        host.AddComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
        var collider = host.AddComponent<BoxCollider2D>(); collider.size = new Vector2(1.55f, 2.6f); collider.offset = Vector2.up * 1.3f;
        var feet = new GameObject("Feet"); feet.transform.SetParent(host.transform, false);
        var movement = host.AddComponent<PlayerMovement>();
        typeof(PlayerMovement).GetField("groundCheck", Private).SetValue(movement, feet.transform);
        var player = host.AddComponent<PlayerCharacter>();
        player.characterObject = AssetDatabase.LoadAssetAtPath<CharacterObject>($"Assets/Project/Character/Hero/{character}.asset");
        movement.profile = player.characterObject.movementProfile;
        var fsm = host.AddComponent<PlayerStateMachine>(); fsm.playerCharacter = player; fsm.showFsmLogs = false;
        player.Movement = movement; player.StateMachine = fsm;
        host.SetActive(true);
        Physics2D.SyncTransforms();
        return player;
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
