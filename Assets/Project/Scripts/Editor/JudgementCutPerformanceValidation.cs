#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated presentation benchmark; never saves or replaces the user's scene.</summary>
public static class JudgementCutPerformanceValidation
{
    private static readonly ProfilerMarker Generation = new ProfilerMarker("JudgementCut.Generation");
    private static readonly ProfilerMarker Rendering = new ProfilerMarker("JudgementCut.SynchronizedRender");

    [MenuItem("Tools/Project/Combat/Profile Judgement Cut Presentation")]
    public static void Profile()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before the isolated presentation benchmark.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D pixel = null;
        Camera camera = null;
        Shader baselineShader = null;
        Material baselineMaterial = null;
        try
        {
            var root = new GameObject("Judgement Cut Benchmark");
            SceneManager.MoveGameObjectToScene(root, scene);
            var cameraObject = new GameObject("Benchmark Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 6.8f;
            camera.transform.position = new Vector3(0, 2, -10);
            target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var backdrop = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Project/Prefab/Environment/CombatBackground_B1.prefab"), scene);
            backdrop.transform.SetParent(root.transform);
            for (int i = 0; i < 8; i++)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab"), scene);
                enemy.transform.SetParent(root.transform);
                enemy.transform.position = new Vector3(-7 + i * 2, 0, 0);
            }
            var light = new GameObject("Benchmark Light", typeof(UnityEngine.Rendering.Universal.Light2D));
            light.transform.SetParent(root.transform);
            light.GetComponent<UnityEngine.Rendering.Universal.Light2D>().lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            var fieldObject = new GameObject("Benchmark Field");
            fieldObject.transform.SetParent(root.transform);
            var field = fieldObject.AddComponent<ProceduralSlashField>();
            var table = AssetDatabase.LoadAssetAtPath<ActionResolverTable>(MainDimensionSlashAuthoring.MainPath + "ActionResolverTable_main.asset");
            // Clone authoring data so repeatable benchmarking never dirties the action asset.
            var authored = table.skillActions.Single(a => a.actionId == MainDimensionSlashAuthoring.ActionId)
                .comboTree.rootNode.stepData.eventData.entries.First(e => e.eventType == ActionEventType.ProceduralSlashField).slashFieldSettings;
            var settings = JsonUtility.FromJson<ProceduralSlashFieldSettings>(JsonUtility.ToJson(authored));
            settings.material = authored.material;
            settings.seed = 1234;
            field.Begin(null, settings, Vector3.zero, 1, null, true);
            SceneManager.MoveGameObjectToScene(field.VisualRoot.gameObject, scene);
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceName}; 1920x1080; saved settings scale={settings.overallScale}, ribbons={field.TotalSlashCount}; seed=1234");
            report.AppendLine("Editor presentation only: eight static enemy prefabs + CombatBackground_B1; no live AI, physics contacts or damage. SynchronizedRender includes submission + GPU completion/readback, not pure GPU time.");
            for (int i = 0; i < 16; i++) field.Begin(null, settings, Vector3.zero, 1, null, true);
            using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1))
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "JudgementCut.Generation", 1))
            {
                using (Generation.Auto())
                    for (int i = 0; i < 256; i++) field.Begin(null, settings, Vector3.zero, 1, null, true);
                recorder.Stop();
                allocations.Stop();
                report.AppendLine($"Warm Begin: {recorder.LastValue / 256000000.0:F4} ms/cast; GC.Alloc events/cast={(allocations.Count > 0 ? allocations.GetSample(allocations.Count - 1).Count / 256.0 : 0):F1}; recorder valid={allocations.Valid}.");
            }
            // Camera.main can select the retained user scene's camera. Bind only the isolated probe,
            // then rebuild once outside measurements so viewport fit and refraction share this camera.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ProceduralSlashField).GetField("_camera", flags).SetValue(field, camera);
            var ribbons = (System.Collections.IList)typeof(ProceduralSlashField).GetField("_cutRibbons", flags).GetValue(field);
            ribbons.Clear();
            typeof(ProceduralSlashField).GetMethod("BuildMesh", flags).Invoke(field, new object[] { settings, settings.seed, 1 });
            string baselinePath = Path.GetFullPath("Temp/judgement-cut-baseline.shader");
            if (File.Exists(baselinePath))
            {
                baselineShader = ShaderUtil.CreateShaderAsset(File.ReadAllText(baselinePath)
                    .Replace("Project/DimensionSlashRibbon", "Hidden/JudgementCutBenchmarkBaseline"), false);
                baselineMaterial = new Material(authored.material) { shader = baselineShader };
            }
            var renderers = field.VisualRoot.GetComponentsInChildren<MeshRenderer>();
            foreach (float frame in new[] { -1f, 0f, 24f, 42f, 64f, 75f })
            {
                field.Sample(Mathf.Max(0, frame));
                field.VisualRoot.gameObject.SetActive(frame >= 0);
                for (int i = 0; i < 4; i++) Render(camera, pixel);
                if (baselineMaterial != null)
                {
                    foreach (var renderer in renderers) renderer.sharedMaterial = baselineMaterial;
                    for (int i = 0; i < 4; i++) Render(camera, pixel);
                }
                var samples = new double[24];
                var baselineSamples = new double[24];
                for (int i = 0; i < samples.Length; i++)
                {
                    // Alternate the order each pair to reduce drift / thermal / background bias.
                    for (int j = 0; j < (baselineMaterial != null ? 2 : 1); j++)
                    {
                        bool baseline = baselineMaterial != null && ((i + j) % 2 == 0);
                        foreach (var renderer in renderers) renderer.sharedMaterial = baseline ? baselineMaterial : authored.material;
                        using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "JudgementCut.SynchronizedRender", 1))
                        {
                            using (Rendering.Auto()) Render(camera, pixel);
                            recorder.Stop();
                            (baseline ? baselineSamples : samples)[i] = recorder.LastValue / 1000000.0;
                        }
                    }
                }
                Array.Sort(samples);
                Array.Sort(baselineSamples);
                report.AppendLine($"Frame {frame}: render median={samples[12]:F3} ms, p95={samples[22]:F3} ms.");
                if (baselineMaterial != null)
                {
                    report.AppendLine($"  Baseline median={baselineSamples[12]:F3} ms, p95={baselineSamples[22]:F3} ms.");
                    foreach (var renderer in renderers) renderer.sharedMaterial = baselineMaterial;
                    Render(camera, pixel);
                    Color32[] before = Capture(target, $"baseline-judgement-cut-frame-{frame}");
                    foreach (var renderer in renderers) renderer.sharedMaterial = authored.material;
                    Render(camera, pixel);
                    Color32[] after = Capture(target, $"judgement-cut-frame-{frame}");
                    long total = 0;
                    int maximum = 0, changed = 0;
                    for (int i = 0; i < before.Length; i++)
                    {
                        int r = Mathf.Abs(before[i].r - after[i].r), g = Mathf.Abs(before[i].g - after[i].g), b = Mathf.Abs(before[i].b - after[i].b);
                        total += r + g + b;
                        maximum = Mathf.Max(maximum, Mathf.Max(r, Mathf.Max(g, b)));
                        if (r + g + b > 0) changed++;
                    }
                    report.AppendLine($"  RGB8 comparison: max delta={maximum}, mean delta={total / (before.Length * 3.0):F6}, changed pixels={changed}/{before.Length}.");
                }
            }
            string path = Path.GetFullPath("Temp/judgement-cut-presentation.txt");
            File.WriteAllText(path, report.ToString());
            Debug.Log(report + "Report: " + path);
        }
        finally
        {
            if (baselineMaterial != null) Object.DestroyImmediate(baselineMaterial);
            if (baselineShader != null) Object.DestroyImmediate(baselineShader);
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (pixel != null) Object.DestroyImmediate(pixel);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static Color32[] Capture(RenderTexture target, string name)
    {
        var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        RenderTexture saved = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            File.WriteAllBytes(Path.GetFullPath($"Temp/{name}.png"), capture.EncodeToPNG());
            return capture.GetPixels32();
        }
        finally { RenderTexture.active = saved; Object.DestroyImmediate(capture); }
    }

    private static void Render(Camera camera, Texture2D pixel)
    {
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = camera.targetTexture;
            pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
        }
        finally { RenderTexture.active = previous; }
    }

#if UNITY_INCLUDE_TESTS
    [MenuItem("Tools/Project/Combat/Validate Judgement Cut Performance")]
    public static void Validate()
    {
        typeof(ShadowMeterValidation).GetMethod("RunFixtures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { new[] { "ActionVfxLifetimeTests" },
                "Temp/judgement-cut-performance-tests.xml", false });
    }
#endif
}
#endif
