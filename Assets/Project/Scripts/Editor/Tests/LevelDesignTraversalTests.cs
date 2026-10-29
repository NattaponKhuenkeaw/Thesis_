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
using Object=UnityEngine.Object;

public class LevelDesignTraversalTests
{
    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void FirstArenaFitsTheUnmodifiedCombatCameraRange(int level)
    {
        var root=PrefabUtility.LoadPrefabContents($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        try
        {
            var room=root.GetComponentInChildren<CombatStageController>().Chambers[0];
            var left=(BoxCollider2D)room.entranceWall; var right=(BoxCollider2D)room.exitWall;
            float width=right.transform.position.x-right.size.x*.5f-left.transform.position.x-left.size.x*.5f;
            Vector2 authored=new Vector2(6.8f,8f);
            Assert.That(width,Is.GreaterThanOrEqualTo(36f));
            Assert.That(RoomBoundaryMath.LimitCombatOrthoRange(authored,width,16f/9f),Is.EqualTo(authored));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(5,784.25f,805.9f)] [TestCase(10,1342.9f,1456.7f)]
    public void AddedCombatSpaceDoesNotStretchRequiredRoofJumps(int level,float from,float to)
    {
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        Assert.That(LevelDesignLayoutBuilder.WorldX(plan,to)-LevelDesignLayoutBuilder.WorldX(plan,from),
            Is.EqualTo((to-from)*LevelDesignLayoutBuilder.HorizontalScale).Within(.001f));
    }

    [UnityTest]
    public IEnumerator PlayerMotorClimbsEveryAuthoredPlatformLadder()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        for (int level=1;level<=9;level++)
        {
            var fixture=new GameObject($"LV{level} Ladder Physics Test");
            try
            {
                var source=Resources.Load<GameObject>($"RoomLayouts/LV/RoomLayout_LV_{level:00}");
                var terrain=Object.Instantiate(source.transform.Find("Terrain").gameObject,fixture.transform);
                terrain.GetComponent<CompositeCollider2D>().GenerateGeometry();
                var stairs=Object.Instantiate(source.transform.Find("OneWayPlatforms").gameObject,fixture.transform);
                Physics2D.SyncTransforms();
                var plan=LevelDesignLayoutBuilder.GetPlan(level);
                foreach (var ladder in stairs.GetComponentsInChildren<BoxCollider2D>()
                    .GroupBy(b=>Mathf.RoundToInt(b.transform.position.x*1000f)))
                {
                    var steps=ladder.OrderBy(b=>b.bounds.max.y).ToArray();
                    for (int i=1;i<steps.Length;i++)
                        Assert.That(steps[i].bounds.min.y-steps[i-1].bounds.max.y,Is.GreaterThan(2.7f),"Standing body must fit below the next platform");
                    float x=steps[0].bounds.center.x;
                    float sourceY=plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r))
                        .Where(r=>x>=r.xMin && x<=r.xMax && r.yMax<steps[0].bounds.max.y-.01f)
                        .Max(r=>r.yMax);
                    var player=new GameObject("Playable Body And Motor"); player.SetActive(false);
                    try
                    {
                        player.layer=LayerMask.NameToLayer("Player");
                        var body=player.AddComponent<Rigidbody2D>(); body.constraints=RigidbodyConstraints2D.FreezeRotation;
                        var box=player.AddComponent<BoxCollider2D>(); box.size=new Vector2(1.55f,2.6f); box.offset=Vector2.up*1.3f;
                        var check=new GameObject("Feet"); check.transform.SetParent(player.transform,false);
                        var motor=player.AddComponent<PlayerMovement>();
                        motor.profile=Resources.Load<MovementProfile>("MovementSO/MovementProfile_Sample");
                        typeof(PlayerMovement).GetField("groundCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(motor,check.transform);
                        player.transform.position=new Vector3(x,sourceY+.04f,0);
                        player.SetActive(true); Physics2D.SyncTransforms();
                        float settleUntil=Time.realtimeSinceStartup+2f;
                        while (!motor.IsPhysicallyGrounded() && Time.realtimeSinceStartup<settleUntil) yield return null;
                        for(int stepIndex=0;stepIndex<steps.Length;stepIndex++)
                        {
                            var step=steps[stepIndex];
                            Assert.That(motor.IsPhysicallyGrounded(),Is.True,$"LV{level} cannot stand before {step.name}: feet={body.position}, floor={sourceY}, velocity={body.linearVelocity}, paths={terrain.GetComponent<CompositeCollider2D>().pathCount}");
                            motor.ConsumeJumpCount(); motor.ApplyJumpLaunch();
                            for (int f=0;f<2;f++) yield return null;
                            float landingDeadline=Time.realtimeSinceStartup+3f;
                            while (!motor.IsPhysicallyGrounded() && Time.realtimeSinceStartup<landingDeadline) yield return null;
                            Assert.That(motor.IsPhysicallyGrounded(),Is.True,$"LV{level} cannot land on {step.name}: feet={body.position}, velocity={body.linearVelocity}, target={step.bounds.max.y}");
                            int landedIndex=System.Array.FindIndex(steps,s=>Mathf.Abs(body.position.y-s.bounds.max.y)<.08f);
                            Assert.That(landedIndex,Is.GreaterThanOrEqualTo(stepIndex),$"LV{level} jump did not reach {step.name}: feet={body.position}");
                            stepIndex=landedIndex;
                        }
                    }
                    finally { Object.DestroyImmediate(player); }
                }
            }
            finally { Object.DestroyImmediate(fixture); }
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
