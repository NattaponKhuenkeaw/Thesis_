using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RoomExitLightAuthoring
{
    public const string ArtFolder = "Assets/Project/VFX/RoomExit";
    public const string SheetPath = ArtFolder + "/ExitLight_Pixel_3Frames.png";
    public const string PrefabPath = "Assets/Project/Prefab/Environment/RoomExitLight.prefab";
    public static string[] SafeScenes => new[] {
        AssetDatabase.GUIDToAssetPath("a148dcd3fee0376418d8129a64e74e76"),
        AssetDatabase.GUIDToAssetPath("d36c77798e07625409da16f442bc42f5")
    };

    [MenuItem("Tools/Never Night/Author Exit Lights")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath("4d6e8a1c3b5f7092e4a6c8d0b2f4e6a8"), OpenSceneMode.Single);
        ImportFrames();
        BuildPrefab();
        var report = new List<string>();
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Resources/RoomLayouts" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            if (path.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = CaptureGeometry(root);
                int count = Attach(root);
                if (count == 0) throw new InvalidOperationException("No exit found: " + path);
                if (before != CaptureGeometry(root)) throw new InvalidOperationException("Existing geometry changed: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add(path + ": " + count + " exit light(s); geometry unchanged");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in SafeScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    string before = CaptureGeometry(root);
                    int count = Attach(root);
                    if (before != CaptureGeometry(root)) throw new InvalidOperationException("Existing scene geometry changed: " + path);
                    if (count > 0) report.Add(path + ": " + count + " exit light(s); geometry unchanged");
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/exit-light-authoring.txt", report);
        Debug.Log("Exit light authoring complete: " + report.Count + " layouts/scenes, boss excluded.");
    }

    [MenuItem("Tools/Never Night/Crop Exit Light Sprites Only")]
    public static void CropFramesOnly()
    {
        ImportFrames();
        Debug.Log("Exit light sprite crops updated; layouts and source artwork unchanged.");
    }

    private static void ImportFrames()
    {
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 724f / 6f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 256;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects();
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        if (width != 2172 || height != 724)
            throw new InvalidOperationException("Exit light crops require the original 2172 x 724 sheet.");
        // Keep the beam, excluding the bright door frame and bleed from the preceding frame.
        var frames = Enumerable.Range(0, 3).Select(i => new SpriteRect {
            name = "ExitLight_" + i,
            rect = new Rect(i * 724 + 32, 112, 666, 612),
            pivot = new Vector2(1f, 0f),
            alignment = SpriteAlignment.Custom,
            spriteID = previous.FirstOrDefault(s => s.name == "ExitLight_" + i)?.spriteID ?? GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(frames);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            frames.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply();
        AssetDatabase.WriteImportSettingsIfDirty(SheetPath);
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void BuildPrefab()
    {
        string materialPath = ArtFolder + "/ExitLight_Unlit.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (frames.Length != 3) throw new InvalidOperationException("Exit light needs exactly three sprites.");
        var clip = new AnimationClip { name = "ExitLightLoop", frameRate = 4f };
        var keys = new[] { 0, 1, 2, 1, 0 }.Select((frame, index) =>
            new ObjectReferenceKeyframe { time = index / 4f, value = frames[frame] }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("Light", typeof(SpriteRenderer), "m_Sprite"), keys);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        clipSettings.startTime = 0f;
        clipSettings.stopTime = 1f;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        string clipPath = ArtFolder + "/ExitLightLoop.anim";
        var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (savedClip == null) AssetDatabase.CreateAsset(clip, clipPath);
        else { EditorUtility.CopySerialized(clip, savedClip); Object.DestroyImmediate(clip); clip = savedClip; }

        string controllerPath = ArtFolder + "/ExitLight.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var layers = controller.layers;
        layers[0].defaultWeight = 1f;
        controller.layers = layers;
        var machine = layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "ExitLightLoop")
            ?? machine.AddState("ExitLightLoop");
        state.motion = clip;
        machine.defaultState = state;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        var root = new GameObject("RoomExitLight");
        try
        {
            var art = new GameObject("Light");
            art.transform.SetParent(root.transform, false);
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sharedMaterial = material;
            renderer.color = new Color(1f, .96f, .85f, .22f);
            renderer.sortingOrder = 530;
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var visual = root.AddComponent<RoomExitLight>();
            using (var data = new SerializedObject(visual))
            {
                data.FindProperty("lightRenderer").objectReferenceValue = renderer;
                data.FindProperty("loopAnimator").objectReferenceValue = animator;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static int Attach(GameObject root)
    {
        int count = 0;
        foreach (var exit in root.GetComponentsInChildren<TransitionTrigger>(true))
        {
            var existing = exit.GetComponentInChildren<RoomExitLight>(true);
            var visualRoot = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), exit.transform);
            visualRoot.name = "RoomExitLight";
            Transform guide = root.transform.Find("ExitGuide");
            Physics2D.SyncTransforms();
            Bounds trigger = exit.ExitCollider.bounds;
            float x = guide != null ? guide.position.x : trigger.max.x;
            float floor = guide != null ? guide.position.y : FindFloor(exit, trigger);
            visualRoot.transform.position = new Vector3(x, floor, exit.transform.position.z);
            visualRoot.transform.rotation = Quaternion.identity;
            visualRoot.transform.localScale = new Vector3(
                1f / Mathf.Max(.0001f, Mathf.Abs(exit.transform.lossyScale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(exit.transform.lossyScale.y)), 1f);
            var visual = visualRoot.GetComponent<RoomExitLight>();
            using (var data = new SerializedObject(visual))
            {
                data.FindProperty("exit").objectReferenceValue = exit;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            count++;
        }
        return count;
    }

    private static float FindFloor(TransitionTrigger exit, Bounds trigger)
    {
        // Prefab-content preview scenes do not participate in ordinary physics queries.
        if (EditorSceneManager.IsPreviewScene(exit.gameObject.scene))
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene probeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject copy = null;
            try
            {
                Transform source = exit.transform.root;
                string path = AnimationUtility.CalculateTransformPath(exit.transform, source);
                copy = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(copy, probeScene);
                Physics2D.SyncTransforms();
                var probe = copy.transform.Find(path).GetComponent<TransitionTrigger>();
                return FindFloor(probe, probe.ExitCollider.bounds);
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                EditorSceneManager.CloseScene(probeScene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        int mask = LayerMask.GetMask("Ground", "OneWayPlatform");
        foreach (var hit in Physics2D.RaycastAll(new Vector2(trigger.center.x, trigger.max.y + 3f),
            Vector2.down, trigger.size.y + 30f, mask))
            if (hit.collider != exit.ExitCollider && !hit.collider.isTrigger && hit.normal.y > .7f)
                return hit.point.y;
        throw new InvalidOperationException("No supporting floor at exit: " + exit.transform.root.name + "/" + exit.name);
    }

    public static string CaptureGeometry(GameObject root)
    {
        return string.Join("\n", root.GetComponentsInChildren<Transform>(true)
            .Where(t => t.GetComponentInParent<RoomExitLight>(true) == null)
            .Select(t => AnimationUtility.CalculateTransformPath(t, root.transform) + ":"
                + JsonUtility.ToJson(new TransformState { position = t.localPosition, rotation = t.localRotation, scale = t.localScale })
                + string.Join("", t.GetComponents<Collider2D>().Select(c => EditorJsonUtility.ToJson(c)))));
    }

    [Serializable] private struct TransformState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
}
