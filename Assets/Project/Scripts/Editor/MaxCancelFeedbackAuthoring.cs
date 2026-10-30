using TMPro;
using UnityEditor;
using UnityEngine;

public static class MaxCancelFeedbackAuthoring
{
    public const string LabelPath = "Assets/Project/Resources/UI/MaxCancelLabel.prefab";

    [MenuItem("Tools/Project/Combat/Author MAX CANCEL Pixel Label")]
    public static void Author()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset");
        if (font == null) throw new System.InvalidOperationException("Project GNF TTF font is missing.");
        var root = new GameObject("MAX CANCEL Pixel Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            var label = root.GetComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = 48f; label.fontStyle = FontStyles.Normal;
            label.text = "MAX CANCEL"; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 100f);
            PrefabUtility.SaveAsPrefabAsset(root, LabelPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/Project/Combat/Capture MAX CANCEL Sequence")]
    public static void Capture()
    {
        var owner = new GameObject("MAX CANCEL capture owner"); owner.SetActive(false);
        var player = owner.AddComponent<PlayerCharacter>();
        var cameraObject = new GameObject("MAX CANCEL capture camera", typeof(Camera));
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        GameObject root = null;
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(50000, 50000, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.018f, 0.04f);
            camera.targetTexture = target;
            MaxCancelFeedback.Show(player);
            var feedback = owner.GetComponent<MaxCancelFeedback>();
            root = (GameObject)typeof(MaxCancelFeedback).GetField("_root", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(feedback);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            System.IO.Directory.CreateDirectory("Temp/MaxCancelSequence");
            for (int i = 0; i < 28; i++)
            {
                feedback.Sample(i * MaxCancelFeedback.Duration / 28f);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes($"Temp/MaxCancelSequence/frame-{i:D2}.png", pixels.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous; cameraObject.GetComponent<Camera>().targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(cameraObject);
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(owner);
        }
    }
}
