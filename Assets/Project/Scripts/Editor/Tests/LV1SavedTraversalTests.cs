#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class LV1SavedTraversalTests
{
    private byte[] saved;
    [SetUp] public void RecordSavedLayout() => saved=File.ReadAllBytes(BiomeLayoutAuthoring.LayoutPath(1));
    [TearDown] public void NeverRewriteTheArtistLayout() => Assert.That(File.ReadAllBytes(BiomeLayoutAuthoring.LayoutPath(1)),Is.EqualTo(saved));

    [TestCase(6.425f)] [TestCase(53.975f)] [TestCase(110f)] [TestCase(170f)]
    public void VisibleRoofMatchesThePhysicalWalkableHeight(float x)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(1));
        try
        {
            float visible=LV1TraversalAudit.VisibleCropTop(root,x);
            Assert.That(float.IsNegativeInfinity(visible),Is.False,"No visible crop artwork at sample point.");
            float physical=root.transform.Find("Terrain").Cast<Transform>().Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>()))
                .Where(r=>x>=r.xMin && x<=r.xMax).Max(r=>r.yMax);
            Assert.That(visible,Is.EqualTo(physical).Within(.12f),$"At X={x}, visible rooftop={visible:F3} but collision top={physical:F3}. Feet will appear inside the building or float.");
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    [UnityTest]
    public IEnumerator PlayerMotorCanWalkGroundAndLandOnBothExistingRaisedRoofs()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture=BuildFixture();
        var actor=CreateActor(new Vector2(-40.25f,.04f));
        try
        {
            var motor=actor.GetComponent<PlayerMovement>();
            yield return Settle(motor);
            Assert.That(motor.IsPhysicallyGrounded(),Is.True,"Player cannot stand at the saved entrance.");
            yield return Walk(motor,-5f);
            yield return Walk(motor,-35f);
            foreach(var target in new[]{new Vector2(6.425f,6.37f),new Vector2(53.975f,8.515f)})
            {
                motor.SetHorizontalInput(0); motor.RB.position=target+Vector2.up*2f; motor.RB.linearVelocity=Vector2.zero; Physics2D.SyncTransforms();
                yield return Settle(motor);
                Assert.That(motor.IsPhysicallyGrounded(),Is.True);
                Assert.That(motor.RB.position.y,Is.EqualTo(target.y).Within(.08f));
                yield return Walk(motor,target.x+.9f); yield return Walk(motor,target.x-.9f);
            }
        }
        finally {Object.DestroyImmediate(actor); Object.DestroyImmediate(fixture);}
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DoubleJumpCanReachTheFirstArtistAuthoredRoof()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture=BuildFixture(); var actor=CreateActor(new Vector2(1.7f,.04f));
        try
        {
            var motor=actor.GetComponent<PlayerMovement>(); yield return Settle(motor);
            Assert.That(motor.IsPhysicallyGrounded(),Is.True);
            motor.SetHorizontalInput(1); motor.ConsumeJumpCount(); motor.ApplyJumpLaunch();
            float start=Time.time;
            while(Time.time-start<.32f) yield return null;
            Assert.That(motor.CanJump(),Is.True,"The second jump must still be available.");
            motor.ConsumeJumpCount(); motor.ApplyJumpLaunch();
            yield return Walk(motor,6.425f); yield return Settle(motor);
            Assert.That(motor.IsPhysicallyGrounded(),Is.True);
            Assert.That(motor.RB.position.y,Is.EqualTo(6.37f).Within(.08f));
        }
        finally {Object.DestroyImmediate(actor); Object.DestroyImmediate(fixture);}
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LongRightHandBuildingPhysicallySupportsTheVisibleRoof()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture=BuildFixture();
        var actor=CreateActor(new Vector2(130f,8f));
        try
        {
            var motor=actor.GetComponent<PlayerMovement>();
            yield return Settle(motor);
            Assert.That(motor.IsPhysicallyGrounded(),Is.True);
            Assert.That(motor.RB.position.y,Is.EqualTo(5.68f).Within(.12f),$"At X=130 the visible building is near Y=5.68, but the real motor fell to Y={motor.RB.position.y:F3}.");
        }
        finally {Object.DestroyImmediate(actor); Object.DestroyImmediate(fixture);}
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator WalkingAtTheEndReachesTheExitAndBothLateEncounterTriggers()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var fixture=BuildFixture();
        var actor=CreateActor(new Vector2(105f,8f));
        try
        {
            var motor=actor.GetComponent<PlayerMovement>(); yield return Settle(motor);
            var source=Resources.Load<GameObject>("RoomLayouts/LV/RoomLayout_LV_01");
            var exit=LayoutRoadAuthoring.WorldRect(source.transform.Find("StageExitTrigger").GetComponent<BoxCollider2D>());
            foreach(string room in new[]{"Room_04","Room_05"})
            {
                var trigger=LayoutRoadAuthoring.WorldRect(source.transform.Find("CombatChambers/"+room+"/EnterTrigger").GetComponent<BoxCollider2D>());
                yield return Walk(motor,trigger.center.x);
                var body=new Rect(motor.RB.position+new Vector2(-.775f,0),new Vector2(1.55f,2.6f));
                Assert.That(body.Overlaps(trigger),Is.True,room+" cannot be reached by normal walking.");
            }
            yield return Walk(motor,exit.center.x);
            Assert.That(new Rect(motor.RB.position+new Vector2(-.775f,0),new Vector2(1.55f,2.6f)).Overlaps(exit),Is.True,
                "Walking along the final roof must touch the actual exit trigger.");
        }
        finally {Object.DestroyImmediate(actor); Object.DestroyImmediate(fixture);}
        yield return new ExitPlayMode();
    }

    private static GameObject BuildFixture()
    {
        var root=new GameObject("Saved LV1 movement fixture");
        var source=Resources.Load<GameObject>("RoomLayouts/LV/RoomLayout_LV_01");
        var terrain=Object.Instantiate(source.transform.Find("Terrain").gameObject,root.transform);
        terrain.GetComponent<CompositeCollider2D>().GenerateGeometry();
        Object.Instantiate(source.transform.Find("OneWayPlatforms").gameObject,root.transform);
        Physics2D.SyncTransforms(); return root;
    }
    private static GameObject CreateActor(Vector2 feet)
    {
        var root=new GameObject("Actual player motor probe"); root.SetActive(false); root.layer=LayerMask.NameToLayer("Player");
        var body=root.AddComponent<Rigidbody2D>(); body.constraints=RigidbodyConstraints2D.FreezeRotation;
        var box=root.AddComponent<BoxCollider2D>(); box.size=new Vector2(1.55f,2.6f); box.offset=Vector2.up*1.3f;
        var check=new GameObject("Feet"); check.transform.SetParent(root.transform,false);
        var motor=root.AddComponent<PlayerMovement>(); motor.profile=Resources.Load<MovementProfile>("MovementSO/MovementProfile_Sample");
        typeof(PlayerMovement).GetField("groundCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(motor,check.transform);
        root.transform.position=feet; root.SetActive(true); Physics2D.SyncTransforms(); return root;
    }
    private static IEnumerator Settle(PlayerMovement motor)
    {
        float simulationStart=Time.time;
        while(Time.time-simulationStart<.15f) yield return null;
        float end=Time.realtimeSinceStartup+3;
        while((!motor.IsPhysicallyGrounded() || Mathf.Abs(motor.RB.linearVelocity.y)>.1f) && Time.realtimeSinceStartup<end) yield return null;
    }
    private static IEnumerator Walk(PlayerMovement motor,float x)
    {
        float end=Time.realtimeSinceStartup+Mathf.Abs(x-motor.RB.position.x)/9.5f+2f;
        while(Mathf.Abs(motor.RB.position.x-x)>.2f && Time.realtimeSinceStartup<end)
        {motor.SetHorizontalInput(Mathf.Clamp((x-motor.RB.position.x)/(9.5f*Mathf.Max(Time.deltaTime,.02f)),-1,1)); yield return null;}
        motor.SetHorizontalInput(0);
        Assert.That(Mathf.Abs(motor.RB.position.x-x),Is.LessThan(.25f),$"Walking stopped at {motor.RB.position}; target X={x}.");
    }
    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure() {if(Application.isPlaying) yield return new ExitPlayMode();}
}
#endif
