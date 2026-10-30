#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Parallax;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ParallaxTimingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void BothParallaxImplementationsRunAfterTheInstalledCinemachineBrain()
    {
        var brain = AssetDatabase.LoadAssetAtPath<MonoScript>("Packages/com.unity.cinemachine/Runtime/Behaviours/CinemachineBrain.cs");
        Assert.That(brain, Is.Not.Null);
        int cameraOrder = MonoImporter.GetExecutionOrder(brain);
        foreach (var type in new[] { typeof(ParallaxBackground), typeof(ParallaxLayer) })
        {
            var order = (DefaultExecutionOrder)Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder));
            Assert.That(order, Is.Not.Null);
            Assert.That(order.order, Is.GreaterThan(cameraOrder), type.Name);
        }
    }

    [TestCase("Assets/Project/Prefab/Environment/CombatBackground_B1.prefab")]
    [TestCase("Assets/Project/Prefab/Environment/CombatBackground_B2.prefab")]
    public void SimpleSkyPiecesKeepTheirNativeShapeAndMeetWithoutGaps(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var sky = root.GetComponentsInChildren<ParallaxBackground>(true).Single(SkyBackgroundRepair.IsSky);
        var row = SkyBackgroundRepair.RowImages(sky).OrderBy(r => r.bounds.min.x).ToArray();
        Assert.That(row.Length, Is.GreaterThanOrEqualTo(3));
        for (int i = 0; i < row.Length; i++)
        {
            Assert.That(row[i].drawMode, Is.EqualTo(SpriteDrawMode.Simple));
            Assert.That(row[i].bounds.size.x / row[i].bounds.size.y,
                Is.EqualTo(row[i].sprite.bounds.size.x / row[i].sprite.bounds.size.y).Within(.001f));
            if (i > 0) Assert.That(row[i].bounds.min.x, Is.EqualTo(row[i - 1].bounds.max.x).Within(.001f));
            if (i > 0) Assert.That(row[i].flipX, Is.Not.EqualTo(row[i - 1].flipX), "Adjacent clouds must share matching mirrored edges");
            if (row[i].transform != sky.transform)
            {
                Assert.That(row[i].transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(row[i].GetComponent<ParallaxBackground>(), Is.Null, "Only the row root should move; pieces cannot drift independently");
            }
        }
    }

    [TestCase("Test_Lobby")] [TestCase("Test_Shop")] [TestCase("Test_Rest")] [TestCase("Test_Level")] [TestCase("Test_Boss")]
    public void SwitchingRoomBiomesAlsoHidesAllNewSkyPieces(string sceneName)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/" + sceneName + ".unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var skies = roots.SelectMany(r => r.GetComponentsInChildren<ParallaxBackground>(true)).Where(SkyBackgroundRepair.IsSky).ToArray();
            Assert.That(skies, Is.Not.Empty);
            var environments = roots.SelectMany(r => r.GetComponentsInChildren<RoomBiomeEnvironment>(true)).ToArray();
            Assert.That(environments, Is.Not.Empty);
            var catalog = Resources.Load<MapSO>("MapSO/Test_MapSO");
            foreach (int theme in new[] { 0, 1, 0 })
            {
                foreach (var environment in environments) environment.Apply(catalog, theme);
                foreach (var sky in skies)
                {
                    var center = sky.GetComponent<SpriteRenderer>();
                    foreach (var tile in SkyBackgroundRepair.RowImages(sky))
                    {
                        Assert.That(tile.drawMode, Is.EqualTo(SpriteDrawMode.Simple), sceneName + "/" + sky.name);
                        Assert.That(tile.enabled && tile.gameObject.activeInHierarchy,
                            Is.EqualTo(center.enabled && center.gameObject.activeInHierarchy), "Sky pieces from the other biome must never leak into this view");
                    }
                }
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [TestCase(1)] [TestCase(6)]
    public void ReapplyingSimpleRowsPreservesAllGroundDecorationAndGameplayBindings(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            string before = SkyBackgroundRepair.ProtectedSnapshot(root);
            int objects = root.GetComponentsInChildren<Transform>(true).Length;
            Assert.That(SkyBackgroundRepair.ApplyToRoot(root), Is.False, "The saved sky conversion should already be complete");
            Assert.That(SkyBackgroundRepair.ProtectedSnapshot(root), Is.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects), "Reapplying cannot keep adding duplicate sky images");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [UnityTest]
    public IEnumerator SkyTracksTheFinalCinemachinePositionInTheSameRenderedFrame()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var player = new GameObject("Parallax timing target") { tag = "Player" };
        var body = player.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
        var rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab"));
        foreach (var confiner in rig.GetComponentsInChildren<CinemachineConfiner2D>()) confiner.enabled = false;
        var camera = rig.GetComponentInChildren<Camera>();
        var background = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(B1VeryFarBackground.PrefabPath));
        var sky = background.GetComponentsInChildren<ParallaxBackground>().Single(SkyBackgroundRepair.IsSky);
        sky.targetCamera = camera.transform;
        try
        {
            for (int i = 0; i < 5; i++) yield return null;
            Vector3 startCamera = (Vector3)typeof(ParallaxBackground).GetField("_startCameraPosition", Private).GetValue(sky);
            Vector3 startLayer = (Vector3)typeof(ParallaxBackground).GetField("_startPosition", Private).GetValue(sky);
            Vector3 firstCamera = camera.transform.position;
            for (int frame = 0; frame < 90; frame++)
            {
                body.position = new Vector2(frame * .3f, Mathf.Sin(frame * .12f) * 3f);
                yield return new WaitForSecondsRealtime(.016f);
                Vector3 expected = startLayer + Vector3.Scale(camera.transform.position - startCamera, new Vector3(1f, 1f, 0f));
                Assert.That(Vector2.Distance(sky.transform.position, expected), Is.LessThan(.001f), "The sky used last frame's camera position");
            }
            Assert.That(Vector2.Distance(camera.transform.position, firstCamera), Is.GreaterThan(2f), "The real camera must actually move during this regression test");
        }
        finally
        {
            Object.DestroyImmediate(background); Object.DestroyImmediate(rig); Object.DestroyImmediate(player);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
