using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class PlayableBuildingDressing
{
    public const string MarketDetails = "Assets/Project/Art/BG/B2/B2_PlayableBuildingDetails.png";
    private const string FacadeName = "BuildingFacade";
    private static readonly Color CityTint = new Color(.60f, .68f, .78f, 1f);
    private static readonly Color MarketTint = new Color(.72f, .74f, .82f, 1f);

    [MenuItem("Tools/Project/Level Design/Apply Approved Buildings To Other Layouts")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        PrepareArt();
        Directory.CreateDirectory("Logs");
        var audit = new List<string>();
        string reference = BiomeLayoutAuthoring.LayoutPath(1);
        byte[] approved = File.ReadAllBytes(reference);
        for (int level = 2; level <= 10; level++)
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before = GameplaySnapshot(root);
                Apply(root, level);
                if (before != GameplaySnapshot(root))
                    throw new InvalidOperationException("Building artwork changed gameplay: " + path);
                LevelDesignLayoutBuilder.ValidateGeometry(root, LevelDesignLayoutBuilder.GetPlan(level));
                PrefabUtility.SaveAsPrefabAsset(root, path);
                audit.Add($"LV{level:00}: colliders, bodies, gameplay transforms and script bindings unchanged.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        if (!approved.SequenceEqual(File.ReadAllBytes(reference)))
            throw new InvalidOperationException("The approved LV1 reference changed.");
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Logs/playable-building-gameplay-audit.txt", audit);
        BiomeLayoutAuthoring.CaptureAll();
        Debug.Log("[Playable Buildings] Dressed layouts 02-10 with biome-specific architecture. LV1, physics, routes, platforms and room bindings preserved.");
    }

    public static void Apply(GameObject root, int level)
    {
        if (level < 2 || level > 10) throw new ArgumentOutOfRangeException(nameof(level));
        if(level<=5 && ForegroundGroundDressing.IsPrepared) {ForegroundGroundDressing.Apply(root,level); return;}
        bool city = level <= 5;
        var material = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        var cityParts = Sprites(LV1BuildingAuthoring.Details);
        var cityModules = Sprites(BiomeLayoutAuthoring.CityModules);
        Sprite maskSprite = Sprites(LV1BuildingAuthoring.FacadeA)["BodyA"];
        Sprite fill = city ? maskSprite : BiomeLayoutAuthoring.Load("IMG_4789", "B2_WallFill");
        Sprite storey = city ? cityParts["CityStorey"] : Sprites(MarketDetails)["MarketStorey"];
        Sprite cap = city ? cityParts["RoofMiddle"] : BiomeLayoutAuthoring.Load("IMG_4787", "B2_Walkway");
        Sprite beam = city ? cap : BiomeLayoutAuthoring.Load("IMG_4789", "B2_Beam");
        Color tint = city ? CityTint : MarketTint;
        var blocks = root.transform.Find("Terrain").Cast<Transform>().ToArray();
        var rects = blocks.Select(t => LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        for (int index = 1; index < blocks.Length; index++)
        {
            Transform block = blocks[index];
            Rect bounds = rects[index];
            var previous = block.Find(FacadeName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var facade = Child(block, FacadeName);
            // Keep masks local: an upper facade must not leak through a lower solid's mask.
            facade.gameObject.AddComponent<SortingGroup>().sortingOrder = 13;
            block.GetComponent<SpriteRenderer>().enabled = false;
            foreach (Transform child in block)
                if (child.name == "WalkingSurface") child.GetComponent<SpriteRenderer>().enabled = false;

            var body = Tile(facade, "BuildingBody", fill,
                new Rect(bounds.xMin + .4f, bounds.yMin, bounds.width - .8f, bounds.height - .52f), material, 12);
            body.color = city ? Color.white : new Color(.78f, .8f, .86f, 1f);
            if (bounds.height < 3.5f)
            {
                Tile(facade, "StructuralBeam", beam, bounds, material, 13).color = tint;
            }
            else if (bounds.width < 3.8f)
            {
                Tile(facade, "StructuralColumn", city ? cityParts["Column"] : BiomeLayoutAuthoring.Load("IMG_4790"),
                    bounds, material, 13).color = tint;
            }
            else
            {
                Mask(facade, bounds, city ? maskSprite : fill);
                // Continuous complete storeys cover tall walls below the intact roof-level modules.
                var backing = Tile(facade, "StoreyFill", storey, bounds, material, 13);
                float densityScale = storey.pixelsPerUnit / 100f * 1.1f;
                backing.size = bounds.size / densityScale;
                backing.transform.localScale = new Vector3(densityScale, densityScale, 1f);
                backing.color = tint;
                backing.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                int bayIndex = 0;
                bool upperStoreys = bounds.yMax > 8f;
                foreach (var roofSpan in LevelDesignLayoutBuilder.ExposedTop(bounds, rects))
                {
                    float spanWidth = roofSpan.y - roofSpan.x;
                    float pitch = upperStoreys ? (city ? 5.5f : 9.5f) : (city ? 7.7f : 9f);
                    int count = Mathf.Max(1, Mathf.RoundToInt(spanWidth / pitch));
                    var modules = Enumerable.Range(0, count).Select(i => city
                        ? cityModules[upperStoreys ? "Apartment" : new[] { "Apartment", "Garage", "CornerShop" }[(i + level + index) % 3]]
                        : BiomeLayoutAuthoring.Load(upperStoreys ? "IMG_4781" : new[] { "IMG_4781", "IMG_4780", "IMG_4783", "IMG_4779" }[(i + level + index) % 4])).ToArray();
                    float scale = spanWidth / modules.Sum(s => s.bounds.size.x);
                    float cursor = roofSpan.x;
                    for (int i = 0; i < modules.Length; i++)
                    {
                        Sprite sprite = modules[i];
                        float width = sprite.bounds.size.x * scale;
                        var image = Image(facade, "ArchitectureBay_" + bayIndex++, sprite, material, 14);
                        // Align painted roof pixels, not transparent padding or a chimney.
                        float roofTop = city ? sprite.bounds.max.y :
                            (LayoutRoadAuthoring.OpaqueTop(sprite) - sprite.pivot.y) / sprite.pixelsPerUnit;
                        if (!city && sprite.name == "IMG_4781") roofTop = (sprite.rect.height - 40f - sprite.pivot.y) / sprite.pixelsPerUnit;
                        image.transform.position = new Vector3(cursor + width * .5f, bounds.yMax - roofTop * scale, 0);
                        image.transform.localScale = new Vector3(scale, scale, 1f);
                        image.color = tint;
                        image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        cursor += width;
                    }
                }
                float foundationHeight = bounds.yMin > .1f ? .1875f : Mathf.Min(1.5f, bounds.height * .2f);
                Tile(facade, "FoundationSkirt", city ? cityParts["CityWall"] : fill,
                    new Rect(bounds.xMin + .1f, bounds.yMin, bounds.width - .2f, foundationHeight), material, 15).color = tint;
            }
            int roofIndex = 0;
            foreach (var span in LevelDesignLayoutBuilder.ExposedTop(bounds, rects))
                Tile(facade, "WalkableEdge_" + roofIndex++, cap,
                    new Rect(span.x, bounds.yMax - .1875f, span.y - span.x, .1875f), material, 16);
        }
        HideConflictingBackdrop(root, rects.Skip(1).ToArray());
        BuildingCornerAuthoring.ApplyMarketCorners(root);
        BiomeMaterialAuthoring.ApplyIfPrepared(root);
    }

    private static void HideConflictingBackdrop(GameObject root, Rect[] solids)
    {
        var group = root.transform.Find(BiomeLayoutDressing.GroupName);
        foreach (var image in group.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (image.name.StartsWith("FacadeWindow_", StringComparison.Ordinal) || image.name.StartsWith("CornerNeon_", StringComparison.Ordinal)
                || image.name.StartsWith("RoofDistrict_", StringComparison.Ordinal) || image.name.StartsWith("RoofShop_", StringComparison.Ordinal)
                || image.name.StartsWith("RoofCargo_", StringComparison.Ordinal))
                image.enabled = false;
            else if (image.sortingOrder >= 0 && solids.Any(r => image.bounds.max.x > r.xMin - .5f && image.bounds.min.x < r.xMax + .5f
                && image.bounds.max.y > r.yMax - .25f && image.bounds.min.y < r.yMax + .25f))
                image.enabled = false;
        }
    }

    public static string GameplaySnapshot(GameObject root)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true).Where(t => !InsideFacade(t, root.transform));
        return BiomeLayoutAuthoring.PhysicsSnapshot(root) + "\n" + string.Join("\n", transforms.Select(t =>
            AnimationUtility.CalculateTransformPath(t, root.transform) + "|" + t.localPosition.ToString("R")
            + t.localRotation.ToString("R") + t.localScale.ToString("R") + "|" + t.gameObject.activeSelf))
            + "\n" + string.Join("\n", root.GetComponentsInChildren<Rigidbody2D>(true).Cast<Component>()
                .Concat(root.GetComponentsInChildren<MonoBehaviour>(true)).Select(c =>
                    AnimationUtility.CalculateTransformPath(c.transform, root.transform) + "|" + EditorJsonUtility.ToJson(c)));
    }

    private static bool InsideFacade(Transform item, Transform root)
    {
        for (var current = item; current != null && current != root; current = current.parent)
            if (current.name == FacadeName) return true;
        return false;
    }

    private static void Mask(Transform facade, Rect rect, Sprite sprite)
    {
        var child = Child(facade, "FacadeClip"); child.position = rect.center;
        child.localScale = new Vector3(rect.width / sprite.bounds.size.x, rect.height / sprite.bounds.size.y, 1f);
        var mask = child.gameObject.AddComponent<SpriteMask>(); mask.sprite = sprite;
        mask.isCustomRangeActive = true;
        mask.frontSortingOrder = 15; mask.backSortingOrder = 12;
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
    }

    private static SpriteRenderer Image(Transform parent, string name, Sprite sprite, Material material, int order)
    {
        var image = Child(parent, name).gameObject.AddComponent<SpriteRenderer>();
        image.sprite = sprite; image.sharedMaterial = material; image.sortingOrder = order;
        return image;
    }

    private static SpriteRenderer Tile(Transform parent, string name, Sprite sprite, Rect rect, Material material, int order)
    {
        var image = Image(parent, name, sprite, material, order);
        image.transform.position = rect.center; image.drawMode = SpriteDrawMode.Tiled;
        image.tileMode = SpriteTileMode.Continuous; image.size = rect.size;
        image.transform.localScale = Vector3.one;
        return image;
    }

    private static Dictionary<string, Sprite> Sprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);

    public static void PrepareArt()
    {
        AddSlice(LV1BuildingAuthoring.Details, "CityStorey", new Rect(0,312,456,532));
        if (!File.Exists(MarketDetails))
        {
            if (!AssetDatabase.CopyAsset(BiomeLayoutAuthoring.B2Art + "IMG_4781.PNG", MarketDetails))
                throw new IOException("Cannot copy existing market facade art.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(MarketDetails);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        AddSlice(MarketDetails, "MarketStorey", new Rect(40,448,904,240));
        AddSlice(MarketDetails, "RoofLeft", new Rect(40,676,64,52));
        AddSlice(MarketDetails, "RoofRight", new Rect(892,676,64,52));
        AddSlice(MarketDetails, "RoofMiddle", new Rect(104,676,788,52));
    }

    private static void AddSlice(string path, string name, Rect rect)
    {
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path)); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        if (old.Any(s => s.name == name)) return;
        var slices = old.Concat(new[] { new SpriteRect { name = name, rect = rect, pivot = Vector2.one * .5f,
            alignment = SpriteAlignment.Center, spriteID = GUID.Generate() } }).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply(); AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
