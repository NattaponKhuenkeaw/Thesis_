using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.Core.ZoneState;
using Project.Parallax;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BackgroundViewportAudit
{
    public const string OutputFolder = "Logs/BackgroundViewport";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public sealed class CoverageResult
    {
        public string Name;
        public int Gaps;
        public int Samples;
    }

    [MenuItem("Tools/Project/Room Backgrounds/Repair Static Sky Coverage")]
    public static void RepairAndCapture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var audit = new List<string>();
        foreach (string path in SkyBackgroundRepair.PrefabPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!RepairStaticSkies(root)) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                audit.Add(path + ": non-sky transforms, decoration, collision and gameplay unchanged");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in SkyBackgroundRepair.ScenePaths())
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= RepairStaticSkies(root);
                // Lobby/shop artwork and its environment controller can live in different roots.
                changed |= RefreshBindings(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RoomBiomeEnvironment>(true)));
                if (!changed) continue;
                EditorSceneManager.SaveScene(scene);
                audit.Add(path + ": non-sky transforms, decoration, collision and gameplay unchanged");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllLines(OutputFolder + "/preservation.txt", audit);
        var results = CaptureAll("after");
        Debug.Log($"[Background Viewport] Repaired {audit.Count} assets; {results.Sum(r => r.Samples)} views, "
            + $"{results.Count(r => r.Gaps > 0)} rooms/themes still uncovered.");
    }

    public static bool RepairStaticSkies(GameObject root)
    {
        var candidates = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => IsSkyImage(r) && r.enabled
            && (r.transform.parent == null || !r.transform.parent.TryGetComponent<SpriteRenderer>(out var parent) || !IsSkyImage(parent)))
            .Where(r => r.GetComponent<ParallaxBackground>() == null || !r.GetComponent<ParallaxBackground>().enabled).ToArray();
        if (candidates.Length == 0) return false;
        string before = ProtectedSnapshot(root);
        foreach (var image in candidates)
        {
            if (image.GetComponent<Collider2D>() != null) throw new InvalidOperationException("Sky owns gameplay collision: " + image.name);
            var layer = image.GetComponent<ParallaxBackground>();
            if (layer == null) layer = image.gameObject.AddComponent<ParallaxBackground>();
            layer.enabled = true;
            SkyBackgroundRepair.Configure(layer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
        }
        RefreshBindings(root.GetComponentsInChildren<RoomBiomeEnvironment>(true));
        if (before != ProtectedSnapshot(root)) throw new InvalidOperationException("Sky repair modified protected content: " + root.name);
        return true;
    }

    private static bool RefreshBindings(IEnumerable<RoomBiomeEnvironment> environments)
    {
        bool changed = false;
        foreach (var environment in environments)
        {
            using var data = new SerializedObject(environment);
            var images = data.FindProperty("biomeOneArtwork");
            var layers = data.FindProperty("biomeOneParallax");
            int oldImages = images.arraySize, oldLayers = layers.arraySize;
            var skies = Enumerable.Range(0, images.arraySize)
                .Select(i => images.GetArrayElementAtIndex(i).objectReferenceValue as SpriteRenderer)
                .Where(r => r != null && IsSkyImage(r))
                .Select(r => r.GetComponent<ParallaxBackground>()).Where(p => p != null && p.enabled).Distinct().ToArray();
            foreach (var sky in skies) AppendReferences(images, SkyBackgroundRepair.RowImages(sky));
            AppendReferences(layers, skies);
            if (images.arraySize == oldImages && layers.arraySize == oldLayers) continue;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(environment);
            changed = true;
        }
        return changed;
    }

    private static void AppendReferences(SerializedProperty array, IEnumerable<Object> additions)
    {
        foreach (var item in additions)
        {
            if (Enumerable.Range(0, array.arraySize).Any(i => array.GetArrayElementAtIndex(i).objectReferenceValue == item)) continue;
            int index = array.arraySize++;
            array.GetArrayElementAtIndex(index).objectReferenceValue = item;
        }
    }

    public static string ProtectedSnapshot(GameObject root)
    {
        return string.Join("\n", root.GetComponentsInChildren<Component>(true).Where(c =>
        {
            if (c is RoomBiomeEnvironment) return false; // Only sky membership arrays are extended.
            for (var t = c.transform; t != null; t = t.parent)
                if (t.TryGetComponent<SpriteRenderer>(out var image) && IsSkyImage(image)) return false;
            return true;
        }).Select(c => AnimationUtility.CalculateTransformPath(c.transform, root.transform)
            + "|" + c.GetType().FullName + "|" + EditorJsonUtility.ToJson(c)));
    }

    [MenuItem("Tools/Project/Room Backgrounds/Capture Viewport Coverage")]
    public static void Capture()
    {
        var results = CaptureAll("before");
        Debug.Log($"[Background Viewport] {results.Count} rooms/themes, {results.Sum(r => r.Samples)} views, "
            + $"{results.Count(r => r.Gaps > 0)} rooms/themes with uncovered pixels.");
    }

    public static List<CoverageResult> CaptureAll(string label)
    {
        Directory.CreateDirectory(OutputFolder + "/" + label);
        var results = new List<CoverageResult>();
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Resources/RoomLayouts" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                CaptureRoots(new[] { root }, scene, path, label, results);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        foreach (string path in SkyBackgroundRepair.ScenePaths())
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try { CaptureRoots(scene.GetRootGameObjects(), scene, path, label, results); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllLines(OutputFolder + "/" + label + "/coverage.txt",
            results.Select(r => $"{r.Name}: gaps={r.Gaps}, samples={r.Samples}"));
        return results;
    }

    private static void CaptureRoots(GameObject[] roots, Scene scene, string path, string label, List<CoverageResult> results)
    {
        var skyImages = roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)).Where(IsSkyImage).ToArray();
        if (skyImages.Length == 0) return; // Scene shells mount their tested RoomLayout at runtime.
        var environments = roots.SelectMany(r => r.GetComponentsInChildren<RoomBiomeEnvironment>(true)).ToArray();
        int themes = environments.Length > 0 ? 2 : 1;
        var host = new GameObject("Viewport coverage camera");
        SceneManager.MoveGameObjectToScene(host, scene);
        var camera = host.AddComponent<Camera>();
        camera.enabled = false;
        camera.scene = scene;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.magenta;
        camera.GetUniversalAdditionalCameraData();
        var target = new RenderTexture(840, 360, 24);
        target.Create();
        var entrance = roots.SelectMany(r => r.GetComponentsInChildren<EntrancePoint>(true)).FirstOrDefault();
        Vector3 start = entrance != null ? entrance.StopPosition : Vector3.zero;
        var colliders = roots.SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
            .Where(c => !c.isTrigger && c.enabled && c.gameObject.layer == LayerMask.NameToLayer("Ground")).ToArray();
        var area = colliders.Length > 0 ? colliders[0].bounds : new Bounds(start, new Vector3(40f, 20f));
        foreach (var collider in colliders.Skip(1)) area.Encapsulate(collider.bounds);
        float maxSize = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab")
            .GetComponentsInChildren<CinemachineCamera>(true).Max(c => c.Lens.OrthographicSize);
        maxSize = Mathf.Max(maxSize, roots.SelectMany(r => r.GetComponentsInChildren<CinemachineCamera>(true))
            .Select(c => c.Lens.OrthographicSize).DefaultIfEmpty(0f).Max());
        try
        {
            var positions = roots.SelectMany(r => r.GetComponentsInChildren<ParallaxBackground>(true))
                .ToDictionary(p => p, p => p.transform.position);
            for (int theme = 0; theme < themes; theme++)
            {
                foreach (var environment in environments) environment.Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"), theme);
                var layers = roots.SelectMany(r => r.GetComponentsInChildren<ParallaxBackground>()).Where(p => p.enabled).ToArray();
                var images = skyImages.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                var oldLayers = images.Select(r => r.gameObject.layer).ToArray();
                int themeNumber = themes > 1 ? theme + 1 : images.Any(r => AssetDatabase.GetAssetPath(r.sprite).Contains("/B2/")) ? 2 : 1;
                var result = new CoverageResult { Name = path + "/B" + themeNumber };
                if (images.Length == 0)
                {
                    result.Gaps = target.width * target.height;
                    results.Add(result);
                    continue;
                }
                for (int binding = 0; binding < 2; binding++)
                {
                foreach (var layer in layers) layer.transform.position = positions[layer];
                // Layout swaps start the new sky before the camera leaves the previous room.
                host.transform.position = new Vector3(binding == 0 ? start.x : area.max.x + 100f, start.y + 5f, -100f);
                foreach (var layer in layers)
                {
                    layer.targetCamera = host.transform;
                    typeof(ParallaxBackground).GetMethod("Start", Private).Invoke(layer, null);
                }
                var points = new[]
                {
                    new Vector2(start.x, start.y + 5f),
                    new Vector2(area.min.x + maxSize, start.y + 5f),
                    new Vector2(area.center.x, area.max.y),
                    new Vector2(area.max.x - maxSize, start.y + 5f)
                };
                for (int aspectIndex = 0; aspectIndex < 2; aspectIndex++)
                for (int view = 0; view < points.Length; view++)
                {
                    camera.aspect = aspectIndex == 0 ? 16f / 9f : 21f / 9f;
                    camera.orthographicSize = maxSize;
                    host.transform.position = new Vector3(points[view].x, points[view].y, -100f);
                    foreach (var layer in layers) typeof(ParallaxBackground).GetMethod("LateUpdate", Private).Invoke(layer, null);
                    for (int i = 0; i < images.Length; i++) images[i].gameObject.layer = 31;
                    camera.cullingMask = 1 << 31;
                    var isolated = CaptureImage(camera, target);
                    int gaps;
                    try { gaps = isolated.GetPixels32().Count(p => p.r > 245 && p.g < 10 && p.b > 245); }
                    finally { Object.DestroyImmediate(isolated); }
                    result.Gaps += gaps;
                    result.Samples++;
                    for (int i = 0; i < images.Length; i++) images[i].gameObject.layer = oldLayers[i];
                    camera.cullingMask = -1;
                    if (aspectIndex == 1 && (view == 0 || view == 2 || gaps > 0))
                    {
                        var full = CaptureImage(camera, target);
                        try
                        {
                            string name = Path.GetFileNameWithoutExtension(path) + $"_B{themeNumber}_{view}_{binding}";
                            File.WriteAllBytes(OutputFolder + "/" + label + "/" + name + ".png", full.EncodeToPNG());
                        }
                        finally { Object.DestroyImmediate(full); }
                    }
                }
                }
                results.Add(result);
            }
        }
        finally { Object.DestroyImmediate(target); Object.DestroyImmediate(host); }
    }

    private static bool IsSkyImage(SpriteRenderer image)
    {
        if (image.sprite == null) return false;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.sprite));
        return guid == "d5dc2f4803c6c354589688fa16b3872e" || guid == "33b0de1be090fb14784d4ed3a02deb5f";
    }

    private static Texture2D CaptureImage(Camera camera, RenderTexture target)
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

    [MenuItem("Tools/Project/Room Backgrounds/Audit Camera Coverage")]
    public static void Audit()
    {
        var lines = new List<string>();
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab");
        foreach (var camera in rig.GetComponentsInChildren<CinemachineCamera>(true))
            lines.Add($"CAMERA {camera.name}: {camera.Lens.OrthographicSize}");
        foreach (string path in SkyBackgroundRepair.PrefabPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Describe(root, path, lines); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in SkyBackgroundRepair.ScenePaths())
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var root in scene.GetRootGameObjects()) Describe(root, path, lines);
                foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CinemachineCamera>(true)))
                    lines.Add($"SCENE CAMERA {path}/{camera.name}: {camera.Lens.OrthographicSize}");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllLines(OutputFolder + "/audit.txt", lines);
        Debug.Log($"[Background Viewport] Audited {lines.Count} camera/sky entries without saving scenes or prefabs.");
    }

    private static void Describe(GameObject root, string path, List<string> lines)
    {
        foreach (var sky in root.GetComponentsInChildren<ParallaxBackground>(true).Where(SkyBackgroundRepair.IsSky))
        {
            var image = sky.GetComponent<SpriteRenderer>();
            var bounds = SkyBackgroundRepair.RowBounds(sky);
            lines.Add($"SKY {path}/{AnimationUtility.CalculateTransformPath(sky.transform, root.transform)} "
                + $"active={sky.gameObject.activeInHierarchy && sky.enabled && image.enabled} "
                + $"center={bounds.center:F2} size={bounds.size:F2} localScale={sky.transform.localScale:F2} "
                + $"multiplier={sky.parallaxEffectMultiplier:F2} centeredY={sky.centerVerticallyOnCamera} "
                + $"sprite={image.sprite.bounds.size:F2} rowImages={SkyBackgroundRepair.RowImages(sky).Length}");
        }
    }
}
