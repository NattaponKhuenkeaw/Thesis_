#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class RoomExitLightTests
{
    private sealed class Gate : IRoomExitGate
    {
        public bool IsExitAvailable { get; set; }
        public int Writes;
        public void SetExitAvailable(bool available, bool playFeedback = true)
        { IsExitAvailable = available; Writes++; }
    }

    private static IEnumerable<string> Layouts => AssetDatabase.FindAssets("t:Prefab",
        new[] { "Assets/Project/Resources/RoomLayouts" }).Select(AssetDatabase.GUIDToAssetPath)
        .Where(path => path.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) < 0).OrderBy(path => path);

    [TestCaseSource(nameof(Layouts))]
    public void EachNonBossLayoutHasOneBoundLightPerExitWithoutNewCollision(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var exits = root.GetComponentsInChildren<TransitionTrigger>(true);
        var lights = root.GetComponentsInChildren<RoomExitLight>(true);
        Assert.That(exits.Length, Is.GreaterThan(0), path);
        Assert.That(lights.Length, Is.EqualTo(exits.Length), path);
        foreach (var exit in exits)
        {
            var light = exit.GetComponentInChildren<RoomExitLight>(true);
            Assert.That(light, Is.Not.Null, path);
            Assert.That(light.Exit, Is.EqualTo(exit));
            Assert.That(light.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(light.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            var renderer = light.GetComponentInChildren<SpriteRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.color.a, Is.LessThanOrEqualTo(.25f), "Keep the approved faint glow.");
            Assert.That(light.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
        }
    }

    [Test]
    public void BossLayoutHasNoExitLight()
    {
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Executioner.prefab");
        Assert.That(boss.GetComponentsInChildren<RoomExitLight>(true), Is.Empty);
    }

    [TestCase("a148dcd3fee0376418d8129a64e74e76")]
    [TestCase("d36c77798e07625409da16f442bc42f5")]
    public void SceneAuthoredSafeRoomsAlsoHaveBoundExitLights(string sceneGuid)
    {
        string path = AssetDatabase.GUIDToAssetPath(sceneGuid);
        EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath("4d6e8a1c3b5f7092e4a6c8d0b2f4e6a8"), OpenSceneMode.Single);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var exits = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TransitionTrigger>(true)).ToArray();
            Assert.That(exits.Length, Is.GreaterThan(0));
            foreach (var exit in exits)
                Assert.That(exit.GetComponentInChildren<RoomExitLight>(true)?.Exit, Is.EqualTo(exit));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ThreeFramesAreAlignedPointFilteredTransparentAndLoopAtFourFps()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(RoomExitLightAuthoring.SheetPath);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.maxTextureSize, Is.EqualTo(256));
        var sprites = AssetDatabase.LoadAllAssetsAtPath(RoomExitLightAuthoring.SheetPath)
            .OfType<Sprite>().OrderBy(s => s.name).ToArray();
        Assert.That(sprites.Length, Is.EqualTo(3));
        foreach (var sprite in sprites)
        {
            Assert.That(sprite.pivot.x / sprite.rect.width, Is.EqualTo(1f).Within(.005f));
            Assert.That(sprite.pivot.y / sprite.rect.height, Is.EqualTo(0f).Within(.005f));
            float texel = 1f / sprite.pixelsPerUnit;
            Assert.That(sprite.bounds.size.x, Is.EqualTo(sprites[0].bounds.size.x).Within(texel));
            Assert.That(sprite.bounds.size.y, Is.EqualTo(sprites[0].bounds.size.y).Within(texel));
        }
        var source = new Texture2D(2, 2);
        try
        {
            source.LoadImage(File.ReadAllBytes(RoomExitLightAuthoring.SheetPath));
            var pixels = source.GetPixels32();
            Assert.That(pixels.Count(p => p.a < 10), Is.GreaterThan(pixels.Length / 4));
        }
        finally { Object.DestroyImmediate(source); }
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RoomExitLightAuthoring.ArtFolder + "/ExitLightLoop.anim");
        Assert.That(clip.frameRate, Is.EqualTo(4f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            RoomExitLightAuthoring.ArtFolder + "/ExitLight.controller");
        Assert.That(controller.layers.Length, Is.EqualTo(1));
        Assert.That(controller.layers[0].stateMachine.defaultState.motion, Is.EqualTo(clip));
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        Assert.That(binding.path, Is.EqualTo("Light"));
        Assert.That(binding.propertyName, Is.EqualTo("m_Sprite"));
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        CollectionAssert.AreEqual(new Object[] { sprites[0], sprites[1], sprites[2], sprites[1], sprites[0] },
            keys.Select(k => k.value).ToArray());
        Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "The loop must not animate position, scale, or brightness.");
    }

    [Test]
    public void SpriteEditorCropsExcludeDoorFrameAndNeighbouringFrames()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(RoomExitLightAuthoring.SheetPath);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var frames = provider.GetSpriteRects().OrderBy(s => s.name).ToArray();
        Assert.That(frames.Length, Is.EqualTo(3));
        var source = new Texture2D(2, 2);
        try
        {
            source.LoadImage(File.ReadAllBytes(RoomExitLightAuthoring.SheetPath));
            for (int frame = 0; frame < frames.Length; frame++)
            {
                Rect rect = frames[frame].rect;
                Assert.That(rect, Is.EqualTo(new Rect(frame * 724 + 32, 112, 666, 612)));
                var pixels = source.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                for (int x = 0; x < (int)rect.width; x++)
                {
                    int bright = 0;
                    for (int y = 0; y < (int)rect.height; y++)
                    {
                        Color p = pixels[y * (int)rect.width + x];
                        if (p.a > .4f && p.r > .82f && p.g > .82f && p.b > .63f) bright++;
                    }
                    Assert.That(bright, Is.LessThan(rect.height * .6f),
                        $"Frame {frame} column {x} must not contain a vertical door-frame strip.");
                }
            }
        }
        finally { Object.DestroyImmediate(source); }
    }

    [UnityTest]
    public IEnumerator RuntimeGateControlsTheLightAndAnimationWithoutChangingTheGateOrTrigger()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        GameObject host = null;
        try
        {
            host = new GameObject("Exit light gate test");
            var box = host.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2f, 4f);
            var exit = host.AddComponent<TransitionTrigger>();
            var gate = new Gate();
            exit.SetExitGate(gate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomExitLightAuthoring.PrefabPath);
            var visual = Object.Instantiate(prefab, host.transform).GetComponent<RoomExitLight>();
            using (var data = new SerializedObject(visual))
            {
                data.FindProperty("exit").objectReferenceValue = exit;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var renderer = visual.GetComponentInChildren<SpriteRenderer>();
            var startPosition = host.transform.position;
            yield return null;
            Assert.That(visual.IsVisible, Is.False, "A locked room must not advertise the exit.");
            gate.IsExitAvailable = true;
            yield return WaitForGameTime(.37f);
            Assert.That(visual.IsVisible, Is.True);
            Assert.That(renderer.color.a, Is.EqualTo(.22f).Within(.01f));
            Sprite first = renderer.sprite;
            yield return WaitForGameTime(.25f);
            Assert.That(renderer.sprite, Is.Not.EqualTo(first), "The authored sprite loop must actually advance.");
            Time.timeScale = 0f;
            Sprite paused = renderer.sprite;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(renderer.sprite, Is.EqualTo(paused));
            Time.timeScale = 1f;
            gate.IsExitAvailable = false;
            yield return WaitForGameTime(.4f);
            Assert.That(visual.IsVisible, Is.False);
            gate.IsExitAvailable = true;
            yield return WaitForGameTime(.4f);
            Assert.That(visual.IsVisible, Is.True, "Revisiting or re-arming an exit must show the light again.");
            visual.gameObject.SetActive(false);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(gate.Writes, Is.Zero);
            Assert.That(host.transform.position, Is.EqualTo(startPosition));
            Assert.That(box.size, Is.EqualTo(new Vector2(2f, 4f)));
            Assert.That(box.isTrigger && box.enabled, Is.True);
        }
        finally
        {
            Time.timeScale = 1f;
            if (host != null) Object.Destroy(host);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditorAfterRuntimeTest()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static IEnumerator WaitForGameTime(float seconds)
    {
        float end = Time.time + seconds;
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.time < end)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Gameplay frames stopped advancing.");
            yield return null;
        }
    }

    [TestCase(1)] [TestCase(5)] [TestCase(6)] [TestCase(10)]
    public void RenderAtTheActualLayoutEdgeInBothBiomes(int level)
    {
        RenderLayout($"Assets/Project/Resources/RoomLayouts/LV/RoomLayout_LV_{level:00}.prefab", "LV_" + level);
    }

    [TestCase("Rest")] [TestCase("Level")]
    public void RenderSafeRoomExit(string room)
    {
        RenderLayout($"Assets/Project/Resources/RoomLayouts/RoomLayout_{room}_01.prefab", room);
    }

    private static void RenderLayout(string path, string name)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 540, 24);
        target.Create();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            var light = root.GetComponentInChildren<RoomExitLight>(true);
            var renderer = light.GetComponentInChildren<SpriteRenderer>();
            Vector3 anchor = light.transform.position;
            Physics2D.SyncTransforms();
            Assert.That(anchor.x, Is.EqualTo(light.Exit.ExitCollider.bounds.max.x).Within(.1f),
                "The light source must match the unlocked camera edge, not the old locked wall.");
            var cameraHost = new GameObject("Exit light preview camera");
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            var camera = cameraHost.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.aspect = 16f / 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.02f, .025f, .06f);
            camera.GetUniversalAdditionalCameraData();
            camera.transform.position = anchor + new Vector3(-9.58f, 2.5f, -100f);
            Assert.That(camera.WorldToViewportPoint(anchor).x, Is.InRange(.97f, 1.01f), "Source light must be on the screen's map edge.");
            var illumination = new GameObject("Exit preview illumination");
            SceneManager.MoveGameObjectToScene(illumination, scene);
            var global = illumination.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
            Directory.CreateDirectory("Logs/ExitLight");
            var previous = RenderTexture.active;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RoomExitLightAuthoring.ArtFolder + "/ExitLightLoop.anim");
            Color32[] baseline = null;
            int firstChanged = 0;
            try
            {
                for (int stage = -1; stage < 3; stage++)
                {
                    if (stage >= 0) clip.SampleAnimation(light.gameObject, stage / 4f);
                    renderer.enabled = stage >= 0;
                    RenderPipeline.SubmitRenderRequest(camera,
                        new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                        image.Apply();
                        Color32[] pixels = image.GetPixels32();
                        if (stage < 0) baseline = pixels;
                        else
                        {
                            int changed = 0;
                            for (int i = 0; i < pixels.Length; i++)
                                if (Mathf.Abs(pixels[i].r - baseline[i].r) > 5 || Mathf.Abs(pixels[i].g - baseline[i].g) > 5) changed++;
                            Assert.That(changed, Is.GreaterThan(250), "Light must be visibly rendered at the exit.");
                            Assert.That(changed, Is.LessThan(pixels.Length / 5), "The light must not wash out the whole scene.");
                            if (stage == 0) firstChanged = changed;
                            else Assert.That(changed, Is.InRange(firstChanged * .6f, firstChanged * 1.4f), "Frames must not flash to a radically different size.");
                            File.WriteAllBytes($"Logs/ExitLight/{name}_Frame{stage}.png", image.EncodeToPNG());
                        }
                    }
                    finally { Object.DestroyImmediate(image); }
                }
            }
            finally { RenderTexture.active = previous; }
        }
        finally
        {
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
