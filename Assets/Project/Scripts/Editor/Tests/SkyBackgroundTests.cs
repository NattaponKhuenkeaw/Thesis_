#if UNITY_INCLUDE_TESTS
using System.Collections;
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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class SkyBackgroundTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static ParallaxBackground MainSky(GameObject root)
        => root.GetComponentsInChildren<ParallaxBackground>(true)
            .Single(p => SkyBackgroundRepair.IsSky(p) && (p.name == "background" || p.name == "Sky"));

    [Test]
    public void LayoutFiveKeepsTheUpperSkyEditableButOnlyEnablesThePrimarySkyRow()
    {
        var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(5));
        try
        {
            var primary = MainSky(root);
            var duplicate = root.GetComponentsInChildren<ParallaxBackground>(true).Single(p => p.name == "background (1)");
            Assert.That(primary.gameObject.activeInHierarchy && primary.enabled, Is.True);
            Assert.That(duplicate.gameObject.activeSelf, Is.False);
            Assert.That(SkyBackgroundRepair.RowImages(duplicate).Length, Is.GreaterThanOrEqualTo(3), "Retain the editable upper row instead of deleting artwork");
            Assert.That(root.GetComponentsInChildren<ParallaxBackground>().Count(SkyBackgroundRepair.IsSky), Is.EqualTo(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [UnityTest]
    public IEnumerator LayoutFiveRuntimeSkyStaysSingleAndCoversStreetRoofsAndExit()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var holder = new GameObject("Layout five sky regression");
        holder.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(5)), holder.transform);
        root.transform.localPosition = Vector3.zero;
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is ParallaxBackground)) component.enabled = false;
        var skies = root.GetComponentsInChildren<ParallaxBackground>(true).Where(SkyBackgroundRepair.IsSky).ToArray();
        var row = skies.SelectMany(SkyBackgroundRepair.RowImages).ToArray();
        foreach (var image in root.GetComponentsInChildren<SpriteRenderer>(true)) image.enabled = row.Contains(image);
        var cameraHost = new GameObject("Layout five sky camera");
        var camera = cameraHost.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.magenta;
        camera.GetUniversalAdditionalCameraData();
        cameraHost.AddComponent<AudioListener>();
        foreach (var sky in skies) sky.targetCamera = cameraHost.transform;
        var target = new RenderTexture(960, 540, 24);
        target.Create();
        Directory.CreateDirectory("Logs/Layout5SkyFix");
        try
        {
            cameraHost.transform.position = new Vector3(10f, 6f, -100f);
            holder.SetActive(true);
            var primary = MainSky(root);
            var duplicate = skies.Single(p => p.name == "background (1)");
            int view = 0;
            foreach (var point in new[] { new Vector3(10f, 6f, 8f), new Vector3(90f, 26f, 12f), new Vector3(170f, 8f, 8f) })
            {
                cameraHost.transform.position = new Vector3(point.x, point.y, -100f);
                camera.orthographicSize = point.z;
                int frame = Time.frameCount;
                while (Time.frameCount < frame + 3) yield return null;
                Assert.That(root.GetComponentsInChildren<ParallaxBackground>().Count(SkyBackgroundRepair.IsSky), Is.EqualTo(1));
                Assert.That(duplicate.gameObject.activeInHierarchy, Is.False);
                Assert.That(primary.transform.position.y, Is.EqualTo(point.y).Within(.001f));
                var image = Capture(camera, target);
                try
                {
                    Assert.That(image.GetPixels32().Count(p => p.r > 245 && p.g < 10 && p.b > 245), Is.Zero,
                        "The primary sky must still fill the screen without its duplicate");
                    File.WriteAllBytes($"Logs/Layout5SkyFix/View{view++}.png", image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(cameraHost);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitAfterRuntimeFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void AllLayoutsJoinSimpleSkyImagesWithoutEnlargingArtwork(int level)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var sky = MainSky(root);
        var image = sky.GetComponent<SpriteRenderer>();
        Assert.That(sky.transform.localScale, Is.EqualTo(Vector3.one * SkyBackgroundRepair.SkyScale));
        Assert.That(sky.centerVerticallyOnCamera, Is.True);
        Assert.That(sky.parallaxEffectMultiplier, Is.EqualTo(Vector2.one));
        Assert.That(image.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
        var row = SkyBackgroundRepair.RowImages(sky);
        Assert.That(row.Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(SkyBackgroundRepair.RowBounds(sky).size.x, Is.GreaterThan(image.bounds.size.x));
        foreach (var tile in row)
        {
            Assert.That(tile.drawMode, Is.EqualTo(SpriteDrawMode.Simple));
            Assert.That(tile.sprite, Is.EqualTo(image.sprite));
            Assert.That(tile.sharedMaterial, Is.EqualTo(image.sharedMaterial));
        }
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
            var sky = MainSky(root);
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
                Assert.That(SkyBackgroundRepair.RowBounds(sky).min.x, Is.LessThan(point.x - 28f));
                Assert.That(SkyBackgroundRepair.RowBounds(sky).max.x, Is.GreaterThan(point.x + 28f));
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
            var sky = MainSky(root);
            var rowImages = SkyBackgroundRepair.RowImages(sky);
            var originalLayers = rowImages.Select(r => r.gameObject.layer).ToArray();
            Directory.CreateDirectory("Logs/SkyRepair");
            for (int view = 0; view < 2; view++)
            {
                host.transform.position = view == 0 ? new Vector3(left + 12f, 6f, -100f) : new Vector3(right - 12f, roof + 12f, -100f);
                camera.orthographicSize = view == 0 ? 8f : 12f;
                foreach (var layer in layers) typeof(ParallaxBackground).GetMethod("LateUpdate", Flags).Invoke(layer, null);
                foreach (var tile in rowImages) tile.gameObject.layer = 31;
                camera.cullingMask = 1 << 31;
                var isolated = Capture(camera, target);
                try
                {
                    int gaps = isolated.GetPixels32().Count(p => p.r > 245 && p.g < 10 && p.b > 245);
                    Assert.That(gaps, Is.Zero, "The sky must cover the viewport even when other layers do not.");
                }
                finally
                {
                    Object.DestroyImmediate(isolated);
                    for (int i = 0; i < rowImages.Length; i++) rowImages[i].gameObject.layer = originalLayers[i];
                    camera.cullingMask = -1;
                }
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
            var sky = MainSky(root);
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

    [TestCase("Assets/Project/Prefab/Environment/CombatBackground_B1.prefab")]
    [TestCase("Assets/Project/Prefab/Environment/CombatBackground_B2.prefab")]
    public void RenderedSimpleCloudJoinsDoNotHaveAHardVerticalCut(string path)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 540, 24); target.Create();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            var sky = MainSky(root);
            var row = SkyBackgroundRepair.RowImages(sky).OrderBy(r => r.bounds.min.x).ToArray();
            foreach (var image in root.GetComponentsInChildren<SpriteRenderer>()) image.enabled = row.Contains(image);
            var host = new GameObject("Sky seam camera"); SceneManager.MoveGameObjectToScene(host, scene);
            var camera = host.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = row[0].bounds.size.y * .5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.GetUniversalAdditionalCameraData();
            float seam = sky.GetComponent<SpriteRenderer>().bounds.max.x;
            host.transform.position = new Vector3(seam, row[0].bounds.center.y, -100f);
            var screenshot = Capture(camera, target);
            try
            {
                int cuts = 0;
                for (int y = 0; y < screenshot.height; y++)
                {
                    Color left = screenshot.GetPixel(screenshot.width / 2 - 1, y);
                    Color right = screenshot.GetPixel(screenshot.width / 2, y);
                    if (Vector3.Distance((Vector4)left, (Vector4)right) > .08f) cuts++;
                }
                Assert.That(cuts, Is.LessThanOrEqualTo(2), "Mirrored cloud edges must agree in the rendered image, not merely touch in world space");
                Directory.CreateDirectory("Logs/SkyRepair");
                string theme = path.EndsWith("B1.prefab") ? "B1" : "B2";
                File.WriteAllBytes($"Logs/SkyRepair/{theme}_SimpleSeam.png", screenshot.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(screenshot); }
        }
        finally { Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
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
