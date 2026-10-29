#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class RevisedFifthLayoutTests
{
    [TestCase(5)] [TestCase(10)]
    public void ForwardRouteHasNoOverheadSolidsOrHiddenReturnSeals(int level)
    {
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        Assert.That(plan.gates.Select(g=>g.x),Is.Ordered.Ascending);
        Assert.That(plan.solids.Skip(1).All(r=>Mathf.Abs(r.yMax-plan.baseline)<.01f),Is.True);
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        Assert.That(root.transform.Find("Terrain").childCount,Is.EqualTo(plan.solids.Length));
        Assert.That(root.transform.Find("OneWayPlatforms").childCount,Is.EqualTo(plan.platforms.Length));
        Assert.That(root.transform.Find("Boundaries/UpperExitSeal"),Is.Null);
        var rooms=LevelDesignSpawnBuilder.Rooms(level);
        Assert.That(rooms,Has.Length.EqualTo(5));
        for(int i=0;i<rooms.Length;i++)
        {
            Assert.That(rooms[i].slots.Length,Is.GreaterThanOrEqualTo(5));
            if(i>0) Assert.That(rooms[i].entry.x,Is.GreaterThan(rooms[i-1].entry.x));
        }
    }

    [UnityTest]
    public IEnumerator RealMotorCanTraverseTheRevisedMarketDoubleJumpClimbs()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture=new GameObject("Revised market jump route"); fixture.transform.position=Vector3.right*1000;
        try
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(10);
            var prefab=Resources.Load<GameObject>("RoomLayouts/LV/RoomLayout_LV_10");
            var terrain=Object.Instantiate(prefab.transform.Find("Terrain").gameObject,fixture.transform,false);
            terrain.GetComponent<CompositeCollider2D>().GenerateGeometry();
            Object.Instantiate(prefab.transform.Find("OneWayPlatforms").gameObject,fixture.transform,false);
            var rects=plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
            var low=LevelDesignLayoutBuilder.ToWorld(plan,plan.platforms[3]);
            var high=LevelDesignLayoutBuilder.ToWorld(plan,plan.platforms[4]);
            var routes=new[] {
                (new Vector2(rects[1].xMax-2f,rects[1].yMax),new Vector2(rects[2].xMin+2f,rects[2].yMax)),
                (new Vector2(low.x-2f,0f),low),(low,high),(high,new Vector2(rects[3].xMin+2f,rects[3].yMax))
            };
            foreach(var route in routes)
            {
                var actor=new GameObject("Playable jump motor"); actor.SetActive(false); actor.transform.SetParent(fixture.transform,false);
                actor.layer=LayerMask.NameToLayer("Player");
                try
                {
                    var body=actor.AddComponent<Rigidbody2D>(); body.constraints=RigidbodyConstraints2D.FreezeRotation;
                    var box=actor.AddComponent<BoxCollider2D>(); box.size=new Vector2(1.55f,2.6f); box.offset=Vector2.up*1.3f;
                    var feet=new GameObject("Feet"); feet.transform.SetParent(actor.transform,false);
                    var motor=actor.AddComponent<PlayerMovement>(); motor.profile=Resources.Load<MovementProfile>("MovementSO/MovementProfile_Sample");
                    typeof(PlayerMovement).GetField("groundCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(motor,feet.transform);
                    actor.transform.position=(Vector3)route.Item1+fixture.transform.position+Vector3.up*.04f;
                    actor.SetActive(true); Physics2D.SyncTransforms();
                    float deadline=Time.realtimeSinceStartup+2f;
                    while(!motor.IsPhysicallyGrounded() && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(motor.IsPhysicallyGrounded(),Is.True,"Must stand before starting the jump route.");
                    motor.ConsumeJumpCount(); motor.ApplyJumpLaunch();
                    bool second=false,landed=false; float targetX=1000+route.Item2.x;
                    deadline=Time.realtimeSinceStartup+6f;
                    while(Time.realtimeSinceStartup<deadline)
                    {
                        yield return null;
                        if(!second && body.linearVelocity.y<=.1f)
                        {
                            motor.ConsumeJumpCount(); motor.ApplyJumpLaunch(); second=true;
                        }
                        float dx=targetX-body.position.x;
                        motor.SetHorizontalInput(Mathf.Clamp(dx/(motor.profile.moveSpeed*Time.fixedDeltaTime),-1f,1f));
                        if(second && motor.IsPhysicallyGrounded() && Mathf.Abs(body.position.x-targetX)<.1f
                            && Mathf.Abs(body.position.y-route.Item2.y)<.1f) { landed=true; break; }
                    }
                    Assert.That(landed,Is.True,$"Cannot traverse {route.Item1} -> {route.Item2}; stopped at {body.position}, velocity {body.linearVelocity}.");
                }
                finally { Object.DestroyImmediate(actor); }
            }
        }
        finally { Object.DestroyImmediate(fixture); }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if(Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
