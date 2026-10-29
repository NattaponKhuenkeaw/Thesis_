#if UNITY_INCLUDE_TESTS
using System.IO;
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

public class B1VeryFarBackgroundTests
{
    [Test]
    public void VeryFarRetainsPixelResolutionAndSitsBetweenTheSkyAndOriginalBuildings()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(B1VeryFarBackground.PrefabPath);
        var layer = root.transform.Find(B1VeryFarBackground.LayerPath);
        Assert.That(layer, Is.Not.Null);
        var image = layer.GetComponent<SpriteRenderer>();
        var sky = root.transform.Find("GameObject/background").GetComponent<SpriteRenderer>();
        var far = root.transform.Find("GameObject/building4_0").GetComponent<SpriteRenderer>();
        var parallax = layer.GetComponent<ParallaxBackground>();
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(B1VeryFarBackground.TexturePath));
        Assert.That(image.sprite.rect.size, Is.EqualTo(new Vector2(3840f, 1392f)));
        Assert.That(image.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(image.sortingLayerID, Is.EqualTo(far.sortingLayerID));
        Assert.That(image.sortingOrder, Is.GreaterThan(sky.sortingOrder));
        Assert.That(image.sortingOrder, Is.LessThan(far.sortingOrder));
        Assert.That(image.bounds.min.y, Is.EqualTo(far.bounds.min.y).Within(.001f));
        Assert.That(layer.lossyScale.y, Is.EqualTo(layer.lossyScale.x).Within(.001f), "Preserve the image's aspect ratio.");
        // A larger world-space camera multiplier means less apparent screen travel.
        Assert.That(parallax.parallaxEffectMultiplier.x, Is.GreaterThan(far.GetComponent<ParallaxBackground>().parallaxEffectMultiplier.x));
        Assert.That(parallax.parallaxEffectMultiplier.x, Is.LessThan(sky.GetComponent<ParallaxBackground>().parallaxEffectMultiplier.x));
        Assert.That(layer.GetComponent<Collider2D>(), Is.Null);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void VeryFarCoversBothStageEndsAfterCameraTravel(int level)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var camera = new GameObject("VeryFar coverage camera");
        try
        {
            var plan = LevelDesignLayoutBuilder.GetPlan(level);
            float left = LevelDesignLayoutBuilder.WorldX(plan, 108);
            float right = LevelDesignLayoutBuilder.WorldX(plan, 1812);
            var layer = root.transform.Find("CombatBackground_B1/" + B1VeryFarBackground.LayerPath);
            var image = layer.GetComponent<SpriteRenderer>();
            var parallax = layer.GetComponent<ParallaxBackground>();
            Assert.That(image.enabled && parallax.enabled && layer.gameObject.activeInHierarchy, Is.True);
            Assert.That(image.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            camera.transform.position = new Vector3(left + 8f, 5f, -10f);
            parallax.targetCamera = camera.transform;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ParallaxBackground).GetMethod("Start", flags).Invoke(parallax, null);
            foreach (float x in new[] { left + 8f, (left + right) * .5f, right - 8f })
            {
                camera.transform.position = new Vector3(x, 5f, -10f);
                typeof(ParallaxBackground).GetMethod("LateUpdate", flags).Invoke(parallax, null);
                Assert.That(image.bounds.min.x, Is.LessThan(x - 18f));
                Assert.That(image.bounds.max.x, Is.GreaterThan(x + 18f));
            }
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(camera); }
    }

    [TestCase(1)] [TestCase(5)]
    public void VeryFarIsVisibleInTheRenderedLayout(int level)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 540, 24);
        target.Create();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var cameraHost = new GameObject("VeryFar render camera");
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            var camera = cameraHost.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.GetUniversalAdditionalCameraData();
            camera.transform.position = (Vector3)LevelDesignLayoutBuilder.ToWorld(LevelDesignLayoutBuilder.GetPlan(level),
                LevelDesignLayoutBuilder.GetPlan(level).start) + new Vector3(10f, 10f, -100f);
            var image = root.transform.Find("CombatBackground_B1/" + B1VeryFarBackground.LayerPath).GetComponent<SpriteRenderer>();
            image.enabled = false;
            var before = Capture(camera, target);
            image.enabled = true;
            var after = Capture(camera, target);
            try
            {
                var a = before.GetPixels32();
                var b = after.GetPixels32();
                int changed = 0;
                for (int i = 0; i < a.Length; i++)
                    if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) changed++;
                Directory.CreateDirectory("Logs/B1VeryFar");
                File.WriteAllBytes($"Logs/B1VeryFar/LV_{level:00}_Before.png", before.EncodeToPNG());
                File.WriteAllBytes($"Logs/B1VeryFar/LV_{level:00}_After.png", after.EncodeToPNG());
                Assert.That(changed, Is.GreaterThan(a.Length / 1000), "The new layer must not be hidden behind the sky or old buildings.");
            }
            finally { Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
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
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            return image;
        }
        finally { RenderTexture.active = previous; }
    }
}
#endif
