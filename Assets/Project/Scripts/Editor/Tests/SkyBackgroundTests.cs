#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class SkyBackgroundTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void AllLayoutsRepeatSkyHorizontallyWithoutEnlargingArtwork(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var sky = root.GetComponentsInChildren<ParallaxBackground>(true).Single(SkyBackgroundRepair.IsSky);
        var image = sky.GetComponent<SpriteRenderer>();
        Assert.That(sky.transform.localScale, Is.EqualTo(Vector3.one * SkyBackgroundRepair.SkyScale));
        Assert.That(sky.centerVerticallyOnCamera, Is.True);
        Assert.That(sky.parallaxEffectMultiplier, Is.EqualTo(Vector2.one));
        Assert.That(image.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
        Assert.That(image.tileMode, Is.EqualTo(SpriteTileMode.Continuous));
        Assert.That(image.size.x, Is.GreaterThan(image.sprite.bounds.size.x));
        Assert.That(image.size.y, Is.EqualTo(image.sprite.bounds.size.y).Within(.001f));
        Assert.That(image.bounds.size.y, Is.GreaterThan(24f), "Cover the boss camera's maximum zoom without stretching the sky.");
        Assert.That(sky.infiniteHorizontal || sky.infiniteVertical, Is.False);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void SkyCoversEndsRoofsAndCameraRebindingWithoutGrowing(int level)
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        var camera = new GameObject("Sky traversal camera");
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var sky = root.GetComponentsInChildren<ParallaxBackground>(true).Single(SkyBackgroundRepair.IsSky);
            var image = sky.GetComponent<SpriteRenderer>();
            var scale = sky.transform.localScale;
            var size = image.size;
            float authoredX = sky.transform.position.x;
            var plan = LevelDesignLayoutBuilder.GetPlan(level);
            float left = LevelDesignLayoutBuilder.WorldX(plan, 108), right = LevelDesignLayoutBuilder.WorldX(plan, 1812);
            float roof = plan.solids.Max(r => LevelDesignLayoutBuilder.ToWorld(plan, r).yMax);
            camera.transform.position = new Vector3(left + 8f, roof + 6f, -100f);
            sky.targetCamera = camera.transform;
            typeof(ParallaxBackground).GetMethod("Start", Flags).Invoke(sky, null);
            Assert.That(sky.transform.position.x, Is.EqualTo(authoredX), "Keep the authored tiled row, not a single image centered horizontally on the camera.");
            foreach (var point in new[] { new Vector2(left + 8f, 6f), new Vector2((left + right) * .5f, roof + 15f), new Vector2(right - 8f, 6f) })
            {
                camera.transform.position = new Vector3(point.x, point.y, -100f);
                typeof(ParallaxBackground).GetMethod("LateUpdate", Flags).Invoke(sky, null);
                Assert.That(image.bounds.min.x, Is.LessThan(point.x - 28f));
                Assert.That(image.bounds.max.x, Is.GreaterThan(point.x + 28f));
                Assert.That(image.bounds.min.y, Is.LessThan(point.y - 12f));
                Assert.That(image.bounds.max.y, Is.GreaterThan(point.y + 12f));
                Assert.That(image.bounds.center.y, Is.EqualTo(point.y).Within(.001f));
            }
            camera.transform.position += new Vector3(100f, 50f, 0f);
            sky.ResetParallax();
            Assert.That(image.bounds.center.y, Is.EqualTo(camera.transform.position.y).Within(.001f));
            Assert.That(sky.transform.localScale, Is.EqualTo(scale));
            Assert.That(image.size, Is.EqualTo(size));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physics));
        }
        finally { Object.DestroyImmediate(camera); PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(1)] [TestCase(5)] [TestCase(6)] [TestCase(10)]
    public void RenderedSkyHasNoDefaultBackgroundAtStreetOrHighestRoof(int level)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 540, 24); target.Create();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)), scene);
            var host = new GameObject("Sky render camera"); SceneManager.MoveGameObjectToScene(host, scene);
            var camera = host.AddComponent<Camera>();
            camera.enabled = false; camera.scene = scene; camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.GetUniversalAdditionalCameraData();
            var plan = LevelDesignLayoutBuilder.GetPlan(level);
            float left = LevelDesignLayoutBuilder.WorldX(plan, 108), right = LevelDesignLayoutBuilder.WorldX(plan, 1812);
            float roof = plan.solids.Max(r => LevelDesignLayoutBuilder.ToWorld(plan, r).yMax);
            host.transform.position = new Vector3(left + 12f, 6f, -100f);
            var layers = root.GetComponentsInChildren<ParallaxBackground>(true).Where(p => p.enabled).ToArray();
            foreach (var layer in layers)
            {
                layer.targetCamera = host.transform;
                typeof(ParallaxBackground).GetMethod("Start", Flags).Invoke(layer, null);
            }
            var sky = layers.Single(SkyBackgroundRepair.IsSky);
            int originalLayer = sky.gameObject.layer;
            Directory.CreateDirectory("Logs/SkyRepair");
            for (int view = 0; view < 2; view++)
            {
                host.transform.position = view == 0 ? new Vector3(left + 12f, 6f, -100f) : new Vector3(right - 12f, roof + 12f, -100f);
                camera.orthographicSize = view == 0 ? 8f : 12f;
                foreach (var layer in layers) typeof(ParallaxBackground).GetMethod("LateUpdate", Flags).Invoke(layer, null);
                sky.gameObject.layer = 31; camera.cullingMask = 1 << 31;
                var isolated = Capture(camera, target);
                try
                {
                    int gaps = isolated.GetPixels32().Count(p => p.r > 245 && p.g < 10 && p.b > 245);
                    Assert.That(gaps, Is.Zero, "The sky must cover the viewport even when other layers do not.");
                }
                finally { Object.DestroyImmediate(isolated); sky.gameObject.layer = originalLayer; camera.cullingMask = -1; }
                var screenshot = Capture(camera, target);
                try { File.WriteAllBytes($"Logs/SkyRepair/LV_{level:00}_{(view == 0 ? "Street" : "Roof")}.png", screenshot.EncodeToPNG()); }
                finally { Object.DestroyImmediate(screenshot); }
            }
        }
        finally { Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void ReapplyingSkyConfigurationKeepsScaleSizeAndGeometryStable()
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(1));
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var sky = root.GetComponentsInChildren<ParallaxBackground>(true).Single(SkyBackgroundRepair.IsSky);
            SkyBackgroundRepair.Configure(sky);
            var scale = sky.transform.localScale;
            var size = sky.GetComponent<SpriteRenderer>().size;
            SkyBackgroundRepair.Configure(sky);
            Assert.That(sky.transform.localScale, Is.EqualTo(scale));
            Assert.That(sky.GetComponent<SpriteRenderer>().size, Is.EqualTo(size));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root), Is.EqualTo(physics));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Texture2D Capture(Camera camera, RenderTexture target)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            return image;
        }
        finally { RenderTexture.active = previous; }
    }
}
#endif
