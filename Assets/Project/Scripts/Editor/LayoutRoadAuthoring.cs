using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LayoutRoadAuthoring
{
    public const string BossPath = "Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Executioner.prefab";
    public const string BiomeOneRoad = "Assets/Project/Art/BG/B1/LevelDesignTiles/LV_Road.png";
    public const string BiomeTwoRoad = "Assets/Project/Art/BG/B2/IMG_4786.PNG";
    private static readonly Dictionary<Sprite, float> OpaqueTops = new Dictionary<Sprite, float>();

    public static string GroupName(int level) => level <= 5 ? "Road_B1" : "Road_B2";

    public static Sprite RoadSprite(int level)
    {
        if (level > 5)
            return AssetDatabase.LoadAssetAtPath<Sprite>(BiomeTwoRoad);

        return AssetDatabase.LoadAssetAtPath<Sprite>(BiomeOneRoad);
    }

    public static Sprite BossRoadSprite()
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).GetComponentsInChildren<SpriteRenderer>(true)
            .First(r => r.transform.parent != null && r.transform.parent.name == "Road"
                && AssetDatabase.GetAssetPath(r.sprite).EndsWith("B1_Asset_LongPlatform.PNG", StringComparison.Ordinal)).sprite;
    }

    private static void PrepareRoadSprite()
    {
        // The boss uses a tight mesh. A separate FullRect import tiles cleanly without changing its source asset.
        var source = BossRoadSprite();
        if (!File.Exists(BiomeOneRoad))
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), BiomeOneRoad);
        var importer = (TextureImporter)AssetImporter.GetAtPath(BiomeOneRoad);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = source.pixelsPerUnit;
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
        importer.SaveAndReimport();
    }

    [MenuItem("Tools/Project/Level Design/Apply Biome Roads To All Layouts")]
    public static void UpdateAll()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before authoring roads.");

        PrepareRoadSprite();
        for (int level = 1; level <= 10; level++)
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                ApplyToLayout(root, level);
                if (before != BiomeLayoutAuthoring.PhysicsSnapshot(root))
                    throw new InvalidOperationException("Road artwork changed gameplay collision: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        BiomeLayoutAuthoring.CaptureAll();
        Debug.Log("[Layout Roads] Applied biome-specific road art only to exposed base ground in ten layouts. Boss source and all gameplay collision preserved.");
    }

    public static void ApplyToLayout(GameObject root, int level)
    {
        if (RoadSprite(1) == null)
            PrepareRoadSprite();
        var terrain = root.transform.Find("Terrain");
        var baseGround = terrain.Find("Ground_01");
        Rect ground = WorldRect(baseGround.GetComponent<BoxCollider2D>());
        Rect[] solids = terrain.Cast<Transform>().Select(t => WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        foreach (string name in new[] { "Road_B1", "Road_B2" })
        {
            var old = root.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var group = new GameObject(GroupName(level));
        group.transform.SetParent(root.transform, false);
        Sprite road = RoadSprite(level);
        float opaqueTop = OpaqueTop(road);
        var material = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        int index = 0;
        foreach (Vector2 span in LevelDesignLayoutBuilder.ExposedTop(ground, solids))
        {
            var segment = new GameObject($"Road_{++index:00}");
            segment.transform.SetParent(group.transform, false);
            // The supplied roads include transparent padding above the curb. Align opaque pixels, not the sprite rectangle.
            segment.transform.position = new Vector3((span.x + span.y) * .5f,
                ground.yMax + (road.pivot.y - opaqueTop) / road.pixelsPerUnit, 0f);
            var image = segment.AddComponent<SpriteRenderer>();
            image.sprite = road;
            image.sharedMaterial = material;
            image.sortingOrder = 18;
            image.drawMode = SpriteDrawMode.Tiled;
            image.tileMode = SpriteTileMode.Continuous;
            image.size = new Vector2(span.y - span.x, road.rect.height / road.pixelsPerUnit);
            segment.transform.localScale = Vector3.one;
        }
        foreach (Transform child in baseGround)
            if (child.name == "WalkingSurface" && child.TryGetComponent<SpriteRenderer>(out var oldCap))
                oldCap.enabled = false;
        BiomeMaterialAuthoring.ApplyIfPrepared(group);
    }

    public static Rect WorldRect(BoxCollider2D box)
    {
        Vector2 center = box.transform.TransformPoint(box.offset);
        Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
        return new Rect(center - size * .5f, size);
    }

    public static float OpaqueTop(Sprite sprite)
    {
        if (OpaqueTops.TryGetValue(sprite, out float top)) return top;
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            int left = Mathf.RoundToInt(sprite.rect.x), bottom = Mathf.RoundToInt(sprite.rect.y);
            var pixels = texture.GetPixels32();
            for (int y = Mathf.RoundToInt(sprite.rect.height) - 1; y >= 0; y--)
                for (int x = 0; x < Mathf.RoundToInt(sprite.rect.width); x++)
                    if (pixels[(bottom + y) * texture.width + left + x].a > 32)
                    {
                        top = y + 1f;
                        OpaqueTops[sprite] = top;
                        return top;
                    }
            throw new InvalidOperationException("Road sprite is empty: " + sprite.name);
        }
        finally { Object.DestroyImmediate(texture); }
    }
}
