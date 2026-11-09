using System;
using System.IO;
using System.Linq;
using Project.AIBehaviour.Boss;
using Project.Training;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Boss112WormTestAuthoring
{
    public const string TestBossPath = Boss112PrototypeAuthoring.Folder + "/Boss112_WormTest.prefab";
    private const int CellSize = 208;
    private const int FrameCount = 5;

    [MenuItem("Tools/Never Night/Boss 112/Import Segment Animation")]
    public static void ImportSegmentAnimation()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        foreach (char color in "abc") ImportSegmentStrip(Path.Combine(folder, $"Enm112{color}.png"), color);
        var frames = LoadSegmentFrames();
        foreach (var path in new[] { TestBossPath, Boss112RunAuthoring.BossPath })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BindSegmentArt(root.GetComponent<Boss112WormBody>(), frames);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    private static string SegmentPath(char color) => Boss112PrototypeAuthoring.Folder + $"/Boss112_Segments_{color}.png";

    private static void ImportSegmentStrip(string sourcePath, char color)
    {
        string path = SegmentPath(color);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var strip = new Texture2D(CellSize * FrameCount, CellSize, TextureFormat.RGBA32, false);
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(sourcePath)) || source.width != 9 * CellSize || source.height != 5 * CellSize)
                throw new InvalidOperationException($"Expected the five-row Boss 112 sheet at {sourcePath}.");
            strip.SetPixels(source.GetPixels(0, source.height - 2 * CellSize, strip.width, strip.height));
            File.WriteAllBytes(path, strip.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(strip); }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 34f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        // Align the round core rather than the cell center so the planted tail does not bob.
        int[] coreY = { 105, 112, 105, 93, 95 };
        var slices = coreY.Select((y, i) => new SpriteRect
        {
            name = $"Segment_{i:00}", rect = new Rect(i * CellSize, 0, CellSize, CellSize),
            alignment = SpriteAlignment.Custom, pivot = new Vector2(106f / CellSize, (float)y / CellSize),
            spriteID = old.FirstOrDefault(slice => slice.name == $"Segment_{i:00}")?.spriteID ?? GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            slices.Select(slice => new SpriteNameFileIdPair(slice.name, slice.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadSegmentFrames() => "abc".SelectMany(color =>
        AssetDatabase.LoadAllAssetsAtPath(SegmentPath(color)).OfType<Sprite>().OrderBy(sprite => sprite.name)).ToArray();

    private static void BindSegmentArt(Boss112WormBody body, Sprite[] frames)
    {
        if (frames.Length != 3 * FrameCount) throw new InvalidOperationException("Import all three Boss 112 segment strips first.");
        using var data = new SerializedObject(body);
        var animation = data.FindProperty("segmentFrames");
        animation.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) animation.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        var segments = data.FindProperty("segments");
        for (int i = 0; i < segments.arraySize; i++)
        {
            var renderer = (SpriteRenderer)segments.GetArrayElementAtIndex(i).objectReferenceValue;
            renderer.sprite = frames[i % FrameCount];
            renderer.color = Color.white;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Never Night/Boss 112/Build Worm Test Prototype")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var root = PrefabUtility.LoadPrefabContents(Boss112PrototypeAuthoring.BossPath);
        try
        {
            root.name = "Boss112_WormTest";
            AttachBody(root);
            PrefabUtility.SaveAsPrefabAsset(root, TestBossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var panel = PrefabUtility.LoadPrefabContents(Boss112PrototypeAuthoring.PanelPath);
        try
        {
            var room = panel.GetComponent<Boss112TestRoom>();
            if (room == null) throw new InvalidOperationException("Boss 112 test room panel is missing.");
            var serialized = new SerializedObject(room);
            serialized.FindProperty("bossPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(TestBossPath).GetComponent<Boss112Controller>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(panel, Boss112PrototypeAuthoring.PanelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(panel); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Boss112] Worm test prefab is ready.");
    }

    internal static bool AttachBody(GameObject root)
    {
        if (root.GetComponent<Boss112WormBody>() != null) return false;
        var frames = LoadSegmentFrames();
        if (frames.Length != 3 * FrameCount) throw new InvalidOperationException("Import all three Boss 112 segment strips first.");

        var body = root.AddComponent<Boss112WormBody>();
        var segments = new SpriteRenderer[5];
        CircleCollider2D tail = null;
        for (int i = 0; i < segments.Length; i++)
        {
            var piece = new GameObject("BodySegment_" + (i + 1), typeof(SpriteRenderer));
            piece.layer = LayerMask.NameToLayer("Enemy");
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = new Vector3(i * .35f, 8f - i * 1.5f, 0f);
            var renderer = piece.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 18 + i / 3;
            segments[i] = renderer;
            var contact = piece.AddComponent<CircleCollider2D>();
            contact.isTrigger = true;
            contact.radius = 1.45f;
            contact.enabled = false;
            if (i == segments.Length - 1) tail = contact;
        }
        body.Configure(root.GetComponent<Boss112Controller>(), root.GetComponent<EnemyColorVariants>(), segments, tail);
        BindSegmentArt(body, frames);
        return true;
    }
}
