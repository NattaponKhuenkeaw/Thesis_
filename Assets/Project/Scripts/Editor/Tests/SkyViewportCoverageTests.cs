#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class SkyViewportCoverageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void SkySurvivesPreviousRoomCameraBindingWithoutTouchingGroundOrDecoration(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        var host = new GameObject("Previous room camera");
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 12f;
        camera.aspect = 21f / 9f;
        try
        {
            string protectedBefore = BackgroundViewportAudit.ProtectedSnapshot(root);
            var skies = root.GetComponentsInChildren<ParallaxBackground>().Where(p => p.enabled && SkyBackgroundRepair.IsSky(p)).ToArray();
            Assert.That(skies, Is.Not.Empty);
            host.transform.position = new Vector3(500f, 5f, -100f);
            foreach (var sky in skies) { sky.targetCamera = host.transform; Invoke(sky, "Start"); }
            foreach (var point in new[] { new Vector2(-30f, 5f), new Vector2(100f, 40f), new Vector2(250f, 8f) })
            {
                host.transform.position = new Vector3(point.x, point.y, -100f);
                foreach (var sky in skies)
                {
                    Vector3 scale = sky.transform.localScale;
                    Invoke(sky, "LateUpdate");
                    AssertCovered(sky, camera);
                    Assert.That(sky.transform.localScale, Is.EqualTo(scale));
                }
            }
            Assert.That(BackgroundViewportAudit.ProtectedSnapshot(root), Is.EqualTo(protectedBefore));
        }
        finally { Object.DestroyImmediate(host); PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void ZoomOutRepairsAnExposedEdgeWithoutReanchoringAnAlreadyCoveredRow()
    {
        var root = PrefabUtility.LoadPrefabContents(B1VeryFarBackground.PrefabPath);
        var host = new GameObject("Sky zoom camera");
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.aspect = 1f;
        try
        {
            var sky = root.GetComponentsInChildren<ParallaxBackground>().Single(SkyBackgroundRepair.IsSky);
            float authoredX = sky.transform.position.x;
            host.transform.position = new Vector3(SkyBackgroundRepair.RowBounds(sky).max.x - 6f, 5f, -100f);
            sky.targetCamera = host.transform;
            Invoke(sky, "Start");
            Assert.That(sky.transform.position.x, Is.EqualTo(authoredX).Within(.001f));
            camera.orthographicSize = 12f;
            camera.aspect = 21f / 9f;
            Invoke(sky, "LateUpdate");
            AssertCovered(sky, camera);
            float repairedX = sky.transform.position.x;
            for (int frame = 0; frame < 50; frame++) Invoke(sky, "LateUpdate");
            Assert.That(sky.transform.position.x, Is.EqualTo(repairedX).Within(.001f), "A repair must not accumulate drift.");
            Assert.That(sky.transform.localScale, Is.EqualTo(Vector3.one * SkyBackgroundRepair.SkyScale));
        }
        finally { Object.DestroyImmediate(host); PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void StaticSkyRepairIsIdempotentAndPreservesEveryNonSkyComponent()
    {
        var root = new GameObject("Static sky repair fixture");
        try
        {
            var decoration = new GameObject("Protected decoration");
            decoration.transform.SetParent(root.transform);
            decoration.transform.localPosition = new Vector3(8f, 4f, 2f);
            decoration.AddComponent<BoxCollider2D>();
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(B1VeryFarBackground.PrefabPath)
                .GetComponentsInChildren<ParallaxBackground>().Single(SkyBackgroundRepair.IsSky).GetComponent<SpriteRenderer>();
            var image = new GameObject("Static Sky", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            image.transform.SetParent(root.transform);
            EditorUtility.CopySerialized(original, image);
            string before = BackgroundViewportAudit.ProtectedSnapshot(root);
            Assert.That(BackgroundViewportAudit.RepairStaticSkies(root), Is.True);
            Assert.That(BackgroundViewportAudit.ProtectedSnapshot(root), Is.EqualTo(before));
            int objects = root.GetComponentsInChildren<Transform>(true).Length;
            Assert.That(BackgroundViewportAudit.RepairStaticSkies(root), Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            Assert.That(image.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator ActualStartAndLateUpdateRecoverAfterAnOutputCameraWarp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var host = new GameObject("Warp test camera") { tag = "MainCamera" };
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 12f;
        camera.aspect = 21f / 9f;
        GameObject root = null;
        try
        {
            for (int level = 1; level <= 10; level++)
            {
                host.transform.position = new Vector3(500f, 5f, -100f);
                var holder = new GameObject("Inactive layout");
                holder.SetActive(false);
                root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)), holder.transform);
                foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(script is ParallaxBackground)) script.enabled = false;
                var skies = root.GetComponentsInChildren<ParallaxBackground>(true).Where(p => p.enabled && SkyBackgroundRepair.IsSky(p)).ToArray();
                foreach (var sky in skies) sky.targetCamera = host.transform;
                holder.SetActive(true);
                int start = Time.frameCount;
                while (Time.frameCount < start + 2) yield return null;
                host.transform.position = new Vector3(0f, 10f, -100f);
                start = Time.frameCount;
                while (Time.frameCount < start + 2) yield return null;
                foreach (var sky in skies.Where(p => p.isActiveAndEnabled)) AssertCovered(sky, camera);
                Object.DestroyImmediate(holder);
                root = null;
            }
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root.transform.parent.gameObject);
            Object.DestroyImmediate(host);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }

    private static void AssertCovered(ParallaxBackground sky, Camera camera)
    {
        var images = SkyBackgroundRepair.RowImages(sky).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        for (int x = 0; x <= 30; x++)
        for (int y = 0; y <= 4; y++)
        {
            var point = camera.transform.position + new Vector3((x / 30f * 2f - 1f) * camera.orthographicSize * camera.aspect,
                (y / 4f * 2f - 1f) * camera.orthographicSize, 0f);
            Assert.That(images.Any(r => point.x >= r.bounds.min.x - .001f && point.x <= r.bounds.max.x + .001f
                && point.y >= r.bounds.min.y - .001f && point.y <= r.bounds.max.y + .001f), Is.True,
                $"{sky.name}: uncovered camera point {point}");
        }
    }

    private static void Invoke(ParallaxBackground layer, string method)
        => typeof(ParallaxBackground).GetMethod(method, Private).Invoke(layer, null);
}
#endif
