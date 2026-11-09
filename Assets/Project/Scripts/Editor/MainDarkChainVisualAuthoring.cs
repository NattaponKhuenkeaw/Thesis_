using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bakes the procedural pixel icon once in the editor. Runtime uses the normal recipe presentation pool.
public static class MainDarkChainVisualAuthoring
{
    private const string Root = "Assets/Project/Resources/FieldEffects";

    [MenuItem("Tools/Project/Combat/Capture Dark Chain Icon")]
    public static void CapturePreview()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var actor = new GameObject("Chain icon preview");
        SceneManager.MoveGameObjectToScene(actor, scene);
        var render = new RenderTexture(512, 512, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            var sprite = actor.AddComponent<SpriteRenderer>();
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Project/Character/Hero/test_0_Main/animation/main_Idle.anim");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(idle)[0];
            sprite.sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(idle, binding)[0].value;
            sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Chevron.mat");
            Bounds body = FieldEffectVisualPreviewWindow.PreviewContext.VisibleBounds(sprite.sprite);
            actor.transform.localScale = Vector3.one * (2f / body.size.y);
            actor.transform.position = -actor.transform.TransformPoint(body.center);
            var boundsHost = new GameObject("Visible body bounds");
            boundsHost.transform.SetParent(actor.transform, false);
            boundsHost.transform.localPosition = body.center;
            var reference = boundsHost.AddComponent<SpriteRenderer>();
            reference.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Chevron.png");
            reference.enabled = false;
            boundsHost.transform.localScale = new Vector3(body.size.x / reference.sprite.bounds.size.x,
                body.size.y / reference.sprite.bounds.size.y, 1f);
            var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/FieldDarkChain.prefab"), actor.transform)
                .GetComponent<FieldEffectVisual>();
            view.Begin(reference.transform, reference);
            typeof(FieldEffectVisual).GetMethod("Sample", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(view, new object[] { .7f });
            var cameraHost = new GameObject("Icon preview camera");
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            var camera = cameraHost.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 1.45f;
            camera.transform.position = new Vector3(.25f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .12f, .15f);
            camera.targetTexture = render;
            camera.enabled = false;
            camera.Render();
            RenderTexture.active = render;
            var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            image.Apply();
            File.WriteAllBytes("Temp/main-dark-chain-icon-preview.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Debug.Log("Dark Chain icon preview: Temp/main-dark-chain-icon-preview.png");
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            render.Release();
            Object.DestroyImmediate(render);
        }
    }

    public static GameObject CreateOrUpdate()
    {
        const int width = 48, height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        // Alternating long and foreshortened links form an interlocking, hanging chain.
        for (int link = 0; link < 4; link++)
        {
            Vector2 center = new Vector2(link % 2 == 0 ? 20f : 27f, 50f - link * 12f);
            float angle = (link % 2 == 0 ? 42f : -42f) * Mathf.Deg2Rad;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 delta = new Vector2(x + .5f, y + .5f) - center;
                    float u = delta.x * Mathf.Cos(angle) + delta.y * Mathf.Sin(angle);
                    float v = -delta.x * Mathf.Sin(angle) + delta.y * Mathf.Cos(angle);
                    float outer = u * u / (11.5f * 11.5f) + v * v / (7f * 7f);
                    float inner = u * u / (7.5f * 7.5f) + v * v / (3f * 3f);
                    if (outer > 1f || inner < 1f) continue;
                    float edge = u * u / 100f + v * v / 30.25f;
                    float hole = u * u / 72.25f + v * v / 16f;
                    Color color = new Color32(24, 10, 37, 255);
                    if (edge < 1f && hole > 1f)
                        color = v > 1.3f ? new Color32(174, 112, 211, 255)
                            : v > -1.8f ? new Color32(103, 53, 145, 255)
                            : new Color32(57, 25, 85, 255);
                    pixels[y * width + x] = color;
                }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        string imagePath = Root + "/DarkChainIcon.png";
        File.WriteAllBytes(imagePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = height;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
        Scene scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("FieldDarkChain");
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var visual = root.AddComponent<FieldEffectVisual>();
            visual.style = FieldEffectVisual.VisualStyle.FloatingIcon;
            visual.tint = Color.white;
            visual.enterSeconds = .18f;
            visual.endingSeconds = .25f;
            var child = new GameObject("Chain Icon");
            child.transform.SetParent(root.transform, false);
            visual.motif = child.AddComponent<SpriteRenderer>();
            visual.motif.sprite = icon;
            visual.motif.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Chevron.mat");
            return PrefabUtility.SaveAsPrefabAsset(root, Root + "/FieldDarkChain.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
