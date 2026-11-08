using System;
using Project.AIBehaviour.Boss;
using Project.Training;
using UnityEditor;
using UnityEngine;

public static class Boss112WormTestAuthoring
{
    public const string TestBossPath = Boss112PrototypeAuthoring.Folder + "/Boss112_WormTest.prefab";
    private const string SegmentPath = Boss112PrototypeAuthoring.Folder + "/Boss112_BodySegment.png";

    [MenuItem("Tools/Never Night/Boss 112/Build Worm Test Prototype")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(SegmentPath);
        if (importer == null) throw new InvalidOperationException("Boss 112 body segment image is missing.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 170f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

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
        var segmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentPath);
        if (segmentSprite == null) throw new InvalidOperationException("Boss 112 body segment sprite is missing.");

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
            renderer.sprite = segmentSprite;
            renderer.sortingOrder = 18 + i / 3;
            renderer.color = new Color(.68f, .05f, .61f);
            segments[i] = renderer;
            var contact = piece.AddComponent<CircleCollider2D>();
            contact.isTrigger = true;
            contact.radius = 1.45f;
            contact.enabled = false;
            if (i == segments.Length - 1) tail = contact;
        }
        body.Configure(root.GetComponent<Boss112Controller>(), root.GetComponent<EnemyColorVariants>(), segments, tail);
        return true;
    }
}
