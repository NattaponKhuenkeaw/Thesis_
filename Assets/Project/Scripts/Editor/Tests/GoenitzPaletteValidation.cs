using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GoenitzPaletteValidation
{
    [Serializable] private sealed class Entry { public string source; public string target; }
    [Serializable] private sealed class Mapping { public Entry[] entries; }
    [Serializable] private sealed class Report
    {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }

    [MenuItem("Tools/Project/Validation/Goenitz Frenzy Palette")]
    public static void Run()
    {
        var report = new Report();
        Shader shader = Resources.Load<Shader>("GoenitzFrenzyPalette");
        Check(report, "Palette shader imports and compiles", () =>
        {
            Require(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Shader unavailable or has errors");
        });
        if (shader != null)
        {
            Check(report, "All 21 source pairs render as their original red-palette targets", () => RenderPairs(shader, Color.white));
            Check(report, "Palette lookup precedes renderer tint", () => RenderPairs(shader, new Color(.5f, .75f, .25f, 1f)));
            Check(report, "Material restoration, reentry, swaps and teardown", ValidateLifecycle);
            Check(report, "Dash animation retains palette and authored visibility", ValidateDashAnimation);
            Check(report, "Dash afterimages capture the current palette material", ValidateAfterimages);
        }
        const string folder = "Docs_JJK/3_Outputs/Reports/GoenitzPalette";
        Directory.CreateDirectory(folder);
        File.WriteAllText(folder + "/unity-validation.json", JsonUtility.ToJson(report, true));
        Debug.Log($"Goenitz palette validation: {report.passed.Count} passed, {report.failed.Count} failed.");
        if (report.failed.Count > 0) Debug.LogError(string.Join("\n", report.failed));
    }

    private static void ValidateLifecycle()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var feedback = new FrenzySpriteFeedback();
        var original = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        try
        {
            var first = CreateRenderer(scene, "Goenitz palette lifecycle", original);
            var second = CreateRenderer(scene, "Other renderer", original);
            feedback.Tick(first);
            Require(first.sharedMaterial == original, "Default flash-only path changed the material");
            feedback.Tick(first, true);
            Material palette = first.sharedMaterial;
            Require(palette != original && palette.shader.name == "Project/GoenitzFrenzyPalette", "Palette not applied");
            feedback.Tick(first, true);
            Require(first.sharedMaterial == palette, "Material allocated on repeated tick");
            feedback.Tick(first, false);
            Require(first.sharedMaterial == original, "Same-renderer actor change did not restore");
            feedback.Tick(first, true);
            Require(first.sharedMaterial == palette, "Palette was not reused on reentry");
            feedback.Tick(second, true);
            Require(first.sharedMaterial == original && second.sharedMaterial == palette, "Renderer swap leaked palette");
            feedback.Tick(null);
            Require(second.sharedMaterial == original && second.transform.childCount == 0, "Mode exit did not clean up");
            feedback.Tick(first, true);
            feedback.Dispose();
            Require(first.sharedMaterial == original && first.transform.childCount == 0 && palette == null, "Dispose leaked feedback");
            feedback.Dispose();
            feedback.Tick(second, true);
            Object.DestroyImmediate(second.gameObject);
            feedback.Tick(null);
        }
        finally
        {
            feedback.Dispose();
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(original);
        }
    }

    private static SpriteRenderer CreateRenderer(Scene scene, string name, Material material)
    {
        var host = new GameObject(name);
        SceneManager.MoveGameObjectToScene(host, scene);
        var image = host.AddComponent<SpriteRenderer>();
        image.sharedMaterial = material;
        return image;
    }

    private static void ValidateDashAnimation()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Project/Character/Hero/test_15_goenitz/Animation/goe_Dash.anim");
        Require(clip != null, "Dash clip missing");
        Scene scene = EditorSceneManager.NewPreviewScene();
        var original = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        var feedback = new FrenzySpriteFeedback();
        try
        {
            var image = CreateRenderer(scene, "Dash animation validation", original);
            for (int frame = 0; frame <= 9; frame++)
            {
                clip.SampleAnimation(image.gameObject, frame / 24f);
                bool visible = image.enabled;
                feedback.Tick(image, true);
                Require(image.sharedMaterial.shader.name == "Project/GoenitzFrenzyPalette", $"Dash frame {frame} lost palette");
                var flash = image.transform.GetChild(0).GetComponent<SpriteRenderer>();
                Require(flash.sprite == image.sprite && flash.enabled == visible, "Flash disagrees with dash visibility/sprite");
            }
        }
        finally { feedback.Dispose(); EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(original); }
    }

    private static void ValidateAfterimages()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var original = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        var feedback = new FrenzySpriteFeedback();
        var ghosts = new List<GameObject>();
        PlayerMovement movement = null;
        try
        {
            var image = CreateRenderer(scene, "Dash afterimage validation", original);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Character/Hero/test_15_goenitz/Goenitz sprite/Goenitz_dash_0.png");
            Require(image.sprite != null, "Dash source sprite missing");
            movement = image.gameObject.AddComponent<PlayerMovement>();
            foreach (bool frenzy in new[] { false, true })
            {
                feedback.Tick(image, frenzy);
                var existing = new HashSet<int>();
                foreach (var renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>()) existing.Add(renderer.GetInstanceID());
                movement.SpawnAfterimage();
                SpriteRenderer ghost = null;
                foreach (var renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
                    if (renderer.name == "DashGhost" && !existing.Contains(renderer.GetInstanceID())) { ghost = renderer; break; }
                Require(ghost != null, "Afterimage was not created");
                ghosts.Add(ghost.gameObject); SceneManager.MoveGameObjectToScene(ghost.gameObject, scene);
                Require(ghost.sharedMaterial == image.sharedMaterial && ghost.sprite == image.sprite, "Afterimage lost palette or pose");
                if (frenzy)
                {
                    feedback.Tick(null);
                    Require(ghost.sharedMaterial.shader.name == "Project/GoenitzFrenzyPalette", "Existing ghost lost its red snapshot on mode exit");
                }
            }
        }
        finally
        {
            if (movement != null) movement.StopAllCoroutines();
            foreach (var ghost in ghosts) if (ghost != null) Object.DestroyImmediate(ghost);
            feedback.Dispose(); EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(original);
        }
    }

    private static void RenderPairs(Shader shader, Color tint)
    {
        var mapping = JsonUtility.FromJson<Mapping>(File.ReadAllText("Docs_JJK/3_Outputs/Reports/GoenitzPalette/mapping.json"));
        int pairs = mapping.entries.Length;
        Require(pairs == 21, "Unexpected palette size");
        int width = pairs * 2 + 2;
        var pixels = new Color32[width];
        for (int i = 0; i < pairs; i++)
        {
            Require(ColorUtility.TryParseHtmlString(mapping.entries[i].source, out var source), "Invalid source color");
            Require(ColorUtility.TryParseHtmlString(mapping.entries[i].target, out var target), "Invalid target color");
            pixels[i] = source;
            pixels[pairs + i] = target;
        }
        pixels[width - 2] = new Color32(34, 167, 89, 255);
        pixels[width - 1] = pixels[0];
        pixels[width - 1].a = 128;
        var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, 1), Vector2.one * .5f, 1);
        var material = new Material(shader);
        var targetTexture = new RenderTexture(width, 1, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var readback = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture previous = RenderTexture.active;
        try
        {
            var image = CreateRenderer(scene, "Palette swatches", material);
            image.sprite = sprite; image.color = tint;
            var host = new GameObject("Palette test camera"); SceneManager.MoveGameObjectToScene(host, scene);
            var camera = host.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = .5f; camera.aspect = width;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var lightHost = new GameObject("Palette test light"); SceneManager.MoveGameObjectToScene(lightHost, scene);
            var light = lightHost.AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global;
            light.color = Color.white; light.intensity = 1;
            targetTexture.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = targetTexture });
            RenderTexture.active = targetTexture;
            readback.ReadPixels(new Rect(0, 0, width, 1), 0, 0); readback.Apply();
            Color32[] output = readback.GetPixels32();
            for (int i = 0; i < pairs; i++)
            {
                Color32 a = output[i], b = output[pairs + i];
                Require(a.a > 250 && b.a > 250, $"Swatch {i} was not rendered");
                Require(Mathf.Abs(a.r - b.r) <= 2 && Mathf.Abs(a.g - b.g) <= 2 && Mathf.Abs(a.b - b.b) <= 2,
                    $"Pair {i} {mapping.entries[i].source}: mapped {a}, expected target control {b}");
            }
            Require(Mathf.Abs(output[width - 1].a - 128) <= 2, "Texture alpha was not preserved");
            if (tint == Color.white)
            {
                Color32 unchanged = output[width - 2];
                Require(Mathf.Abs(unchanged.r - 34) <= 2 && Mathf.Abs(unchanged.g - 167) <= 2 && Mathf.Abs(unchanged.b - 89) <= 2,
                    $"Unmatched color changed: {unchanged}");
            }
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            targetTexture.Release(); Object.DestroyImmediate(targetTexture); Object.DestroyImmediate(readback);
            Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
        }
    }

    private static void Check(Report report, string name, Action check)
    {
        try { check(); report.passed.Add(name); }
        catch (Exception error) { report.failed.Add(name + ": " + error.Message); }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
