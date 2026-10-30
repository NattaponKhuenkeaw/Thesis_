using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LV1BuildingAuthoring
{
    public const string KitFolder = "Assets/Project/Art/BG/B1/LevelDesignTiles";
    public const string FacadeA = KitFolder + "/LV1_Foreground1.png";
    public const string FacadeB = KitFolder + "/LV1_Foreground2.png";
    public const string Details = KitFolder + "/LV1_BuildingDetails.png";
    public const string RearBridge = "LV1_RearOverpass";
    public const float PixelsPerUnit = 64f;
    private const string Art = "Assets/Project/Art/BG/B1/";
    private static readonly BindingFlags PrivateMethods = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Project/Level Design/LV1/Use Original Foreground Palette")]
    public static void ApplyPaletteAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        ImportKit();
        string path = BiomeLayoutAuthoring.LayoutPath(1);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            ApplyForegroundPalette(root);
            if (physics != BiomeLayoutAuthoring.PhysicsSnapshot(root))
                throw new InvalidOperationException("Changing the foreground palette must not change physics.");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Capture();
        Debug.Log("[LV1 Buildings] Applied original FG1/FG2 artwork and colors only to the assembled buildings. Transforms, sizes and gameplay preserved.");
    }

    public static void ApplyForegroundPalette(GameObject root)
    {
        if(ForegroundGroundDressing.IsPrepared)
        {
            foreach(var image in root.transform.Find("Terrain").GetComponentsInChildren<SpriteRenderer>(true))
                if(ForegroundGroundDressing.IsForeground(image.sprite)) image.color=Color.white;
            return;
        }
        var sprites = new[] { FacadeA, FacadeB }.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>())
            .ToDictionary(s => s.name);
        var groups = root.transform.Find("Terrain").Cast<Transform>().Select(t => t.Find("BuildingFacade"))
            .Where(t => t != null).Concat(new[] { root.transform.Find(RearBridge) }).Where(t => t != null);
        foreach (var image in groups.SelectMany(t => t.GetComponentsInChildren<SpriteRenderer>(true)))
        {
            if (image.sprite != null && (AssetDatabase.GetAssetPath(image.sprite) == BiomeLayoutAuthoring.CityModules || image.sprite.name == "CityWall")) continue;
            if (image.sprite == null || !sprites.TryGetValue(image.sprite.name, out var replacement))
                throw new InvalidOperationException("No foreground replacement for " + image.name);
            Vector2 size = image.size;
            Vector3 scale = image.transform.localScale;
            image.sprite = replacement;
            image.size = size;
            image.transform.localScale = scale;
            image.color = Color.white;
        }
    }

    [MenuItem("Tools/Project/Level Design/LV1/Assemble Building And Rear Overpass Example")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        ImportKit();
        string path = BiomeLayoutAuthoring.LayoutPath(1);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
            Apply(root);
            if (physics != BiomeLayoutAuthoring.PhysicsSnapshot(root))
                throw new InvalidOperationException("LV1 dressing must preserve every collider and its position.");
            LevelDesignLayoutBuilder.ValidateGeometry(root, LevelDesignLayoutBuilder.GetPlan(1));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Capture();
        Debug.Log("[LV1 Buildings] Baked existing FG1/FG2 building pieces and a decorative rear overpass into LV1 only. Collision, encounters and route preserved.");
    }

    public static void Apply(GameObject root)
    {
        if(ForegroundGroundDressing.IsPrepared) {ForegroundGroundDressing.Apply(root,1); return;}
        var kit = new[] { FacadeA, FacadeB }.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>())
            .ToDictionary(s => s.name);
        var details = AssetDatabase.LoadAllAssetsAtPath(Details).OfType<Sprite>().ToDictionary(s => s.name);
        var buildings = AssetDatabase.LoadAllAssetsAtPath(BiomeLayoutAuthoring.CityModules).OfType<Sprite>().ToDictionary(s => s.name);
        var solidRects = root.transform.Find("Terrain").Cast<Transform>().Skip(1)
            .Select(t => LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        foreach (Transform block in root.transform.Find("Terrain"))
        {
            if (block.name == "Ground_01") continue;
            var box = block.GetComponent<BoxCollider2D>();
            Rect bounds = LayoutRoadAuthoring.WorldRect(box);
            Remove(block.Find("BuildingFacade"));
            var facade = Child(block, "BuildingFacade");
            block.GetComponent<SpriteRenderer>().enabled = false;
            var wall = new Rect(bounds.xMin + .12f, bounds.yMin, bounds.width - .24f, bounds.height - .2f);
            Piece(facade, "BuildingBody", kit["BodyA"], wall, 12);
            AssembleWholeFacades(facade, bounds, buildings, kit["BodyA"]);
            // A continuous plinth covers window rows cut by the street-level crop.
            var plinth = Piece(facade, "FoundationSkirt", details["CityWall"],
                new Rect(bounds.xMin + .12f, bounds.yMin, bounds.width - .24f, 1.5f), 14);
            plinth.color = new Color(.60f, .68f, .78f, 1f);
            foreach (Transform top in block)
            {
                if (top.name != "WalkingSurface") continue;
                var old = top.GetComponent<SpriteRenderer>();
                Vector2 center = top.position;
                float width = old.size.x * top.lossyScale.x;
                old.enabled = false;
                var trim = Child(facade, "Roofline");
                const float end = .35f;
                const float height = .1875f;
                Piece(trim, "LeftCorner", details["RoofLeft"], new Rect(center.x - width / 2, bounds.yMax - height, end, height), 15);
                Piece(trim, "Middle", details["RoofMiddle"], new Rect(center.x - width / 2 + end, bounds.yMax - height, width - end * 2, height), 15);
                Piece(trim, "RightCorner", details["RoofRight"], new Rect(center.x + width / 2 - end, bounds.yMax - height, end, height), 15);
            }
        }
        var district = root.transform.Find(BiomeLayoutDressing.GroupName);
        if (district != null)
            foreach (var image in district.GetComponentsInChildren<SpriteRenderer>(true))
                if (image.name.StartsWith("FacadeWindow_", StringComparison.Ordinal) || image.name.StartsWith("CornerNeon_", StringComparison.Ordinal))
                    image.enabled = false;
                else if (image.name.StartsWith("RoofDistrict_", StringComparison.Ordinal)
                    || solidRects.Any(r => image.bounds.max.x > r.xMin - .5f && image.bounds.min.x < r.xMax + .5f
                        && image.bounds.max.y > r.yMax - .25f && image.bounds.min.y < r.yMax + .25f))
                    image.enabled = false;
        Remove(root.transform.Find(RearBridge));
        var bridge = Child(root.transform, RearBridge);
        var plan = LevelDesignLayoutBuilder.GetPlan(1);
        float left = LevelDesignLayoutBuilder.WorldX(plan, 600);
        float right = LevelDesignLayoutBuilder.WorldX(plan, 700);
        if (district != null)
            foreach (var image in district.GetComponentsInChildren<SpriteRenderer>(true))
                if (!image.name.StartsWith("FacadeWindow_", StringComparison.Ordinal) && !image.name.StartsWith("CornerNeon_", StringComparison.Ordinal)
                    && image.transform.position.x > left && image.transform.position.x < right && image.bounds.min.y < 1f)
                    image.enabled = false;
        const float underside = 9.6f;
        Piece(bridge, "RearUpperStorey", kit["BodyB"], new Rect(left, underside, right - left, 3.2f), 3);
        Piece(bridge, "RearRoofTrim", kit["RoofMiddle"], new Rect(left, underside + 2.64f, right - left, .56f), 4);
        Piece(bridge, "RearBeam", kit["RoofMiddle"], new Rect(left, underside, right - left, .38f), 4);
        Piece(bridge, "RearLeftAnnex", kit["BodyA"], new Rect(left, underside + 3.2f, 5.2f, 2.8f), 3);
        Piece(bridge, "RearLeftAnnexRoof", kit["RoofMiddle"], new Rect(left, underside + 5.2f, 5.2f, .8f), 4);
        Piece(bridge, "RearRightAnnex", kit["BodyB"], new Rect(right - 4.4f, underside + 3.2f, 4.4f, 1.6f), 3);
        Piece(bridge, "RearRightAnnexRoof", kit["RoofMiddle"], new Rect(right - 4.4f, underside + 4f, 4.4f, .8f), 4);
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(left + .7f, right - .7f, i / 3f);
            var pier = Child(bridge, "RearPier_" + i);
            Piece(pier, "RearColumn", kit["Column"], new Rect(x - .22f, 0, .44f, underside), 3);
            Piece(pier, "RearFoot", kit["RoofMiddle"], new Rect(x - .5f, 0, 1f, .32f), 4);
            if (i == 0 || i == 3)
                Native(pier, "RearLamp", kit["Lamp"], new Vector2(x + .35f, 3.4f), 4);
            if (i < 3)
                Native(bridge, "RearVent_" + i, kit["Grille"], new Vector2(x + (right - left) / 6f, underside + 1.5f), 4);
        }
        BiomeMaterialAuthoring.ApplyIfPrepared(root);
    }

    private static void AssembleWholeFacades(Transform facade, Rect bounds, System.Collections.Generic.Dictionary<string, Sprite> buildings, Sprite maskSprite)
    {
        var maskTransform = Child(facade, "FacadeClip");
        maskTransform.position = bounds.center;
        maskTransform.localScale = new Vector3(bounds.width / maskSprite.bounds.size.x, bounds.height / maskSprite.bounds.size.y, 1f);
        var mask = maskTransform.gameObject.AddComponent<SpriteMask>();
        mask.sprite = maskSprite;
        mask.isCustomRangeActive = true;
        mask.frontSortingOrder = 14;
        mask.backSortingOrder = 12;
        // Complete building artwork keeps windows, floors and pipes in their original relationship.
        int count = Mathf.Max(1, Mathf.RoundToInt(bounds.width / 7.7f));
        var modules = Enumerable.Range(0, count).Select(i => buildings[i % 3 == 1 ? "Garage" : i % 3 == 2 ? "CornerShop" : "Apartment"]).ToArray();
        float scale = bounds.width / modules.Sum(s => s.bounds.size.x);
        float cursor = bounds.xMin;
        for (int i = 0; i < modules.Length; i++)
        {
            Sprite sprite = modules[i];
            float width = sprite.bounds.size.x * scale;
            var image = Native(facade, "ArchitectureBay_" + i, sprite,
                new Vector2(cursor + width * .5f, bounds.yMax - sprite.bounds.max.y * scale), 13);
            image.transform.localScale = new Vector3(scale, scale, 1f);
            image.color = new Color(.60f, .68f, .78f, 1f);
            image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            cursor += width;
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void Remove(Transform item)
    {
        if (item != null) Object.DestroyImmediate(item.gameObject);
    }

    private static SpriteRenderer Piece(Transform parent, string name, Sprite sprite, Rect rect, int order)
    {
        var image = Native(parent, name, sprite, rect.center, order);
        image.drawMode = SpriteDrawMode.Tiled;
        image.tileMode = SpriteTileMode.Continuous;
        image.size = rect.size;
        image.transform.localScale = Vector3.one;
        return image;
    }

    private static SpriteRenderer Native(Transform parent, string name, Sprite sprite, Vector2 center, int order)
    {
        var child = Child(parent, name);
        child.position = new Vector3(center.x, center.y, 0);
        var image = child.gameObject.AddComponent<SpriteRenderer>();
        image.sprite = sprite;
        image.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        image.sortingOrder = order;
        image.color = Color.white;
        return image;
    }

    private static void ImportKit()
    {
        Import(Art + "B1_Parallax_Foreground1.PNG", FacadeA, new[] {
            ("BodyA", new Rect(96, 128, 320, 320)),
            ("RoofLeft", new Rect(0, 840, 48, 76)), ("RoofMiddle", new Rect(48, 840, 360, 76)),
            ("RoofRight", new Rect(432, 840, 48, 76)), ("Column", new Rect(8, 96, 28, 512)),
            ("WindowDark", new Rect(128, 650, 152, 170)), ("Grille", new Rect(740, 260, 192, 100)),
            ("Lamp", new Rect(48, 96, 32, 180))
        });
        Import(Art + "B1_Parallax_Foreground2.PNG", FacadeB, new[] { ("BodyB", new Rect(128, 128, 320, 320)) });
        EnsureWallSlice();
    }

    private static void EnsureWallSlice()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Details);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        if (old.Any(s => s.name == "CityWall")) return;
        var slices = old.Concat(new[] { new SpriteRect { name = "CityWall", rect = new Rect(44,606,144,16),
            pivot = Vector2.one * .5f, alignment = SpriteAlignment.Center, spriteID = GUID.Generate() } }).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply(); AssetDatabase.WriteImportSettingsIfDirty(Details);
        AssetDatabase.ImportAsset(Details, ImportAssetOptions.ForceUpdate);
    }

    private static void Import(string source, string path, (string name, Rect rect)[] definitions)
    {
        if (!File.Exists(path) && !AssetDatabase.CopyAsset(source, path)) throw new IOException("Cannot copy building source: " + source);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var slices = definitions.Select(d => new SpriteRect { name = d.name, rect = d.rect, pivot = Vector2.one * .5f,
            alignment = SpriteAlignment.Center, spriteID = old.FirstOrDefault(s => s.name == d.name)?.spriteID ?? GUID.Generate() }).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply(); AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    public static void Capture()
    {
        Directory.CreateDirectory("Logs/LV1Buildings");
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(1600, 900, 24); target.Create();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(1)), scene);
            var host = new GameObject("LV1 architectural preview camera"); SceneManager.MoveGameObjectToScene(host, scene);
            var camera = host.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.GetUniversalAdditionalCameraData();
            var views = new[] { ("RaisedBuildings", new Vector2(10, 5.5f), 9f), ("RearOverpass", new Vector2(30, 6), 10f),
                ("TallBuilding", new Vector2(54, 6), 9f), ("RooftopStreet", new Vector2(115, 9.5f), 9f),
                ("Overview", new Vector2(75, 4), 69f) };
            var layers = root.GetComponentsInChildren<ParallaxBackground>(true).Where(p => p.enabled).ToArray();
            var marker = Child(root.transform, "PreviewOnly_PlayerScale").gameObject.AddComponent<SpriteRenderer>();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Project/Character/Hero/test_0_Main/animation/main_Idle.anim");
            marker.sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).First())[0].value;
            marker.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
            marker.sortingLayerName = "Player";
            foreach (var layer in layers) { layer.targetCamera = host.transform; typeof(ParallaxBackground).GetMethod("Start", PrivateMethods).Invoke(layer, null); }
            foreach (var view in views)
            {
                host.transform.position = new Vector3(view.Item2.x, view.Item2.y, -100);
                camera.orthographicSize = view.Item3;
                float playerX = view.Item1 == "RaisedBuildings" ? 6.425f : view.Item1 == "TallBuilding" ? 54f : view.Item2.x + 3f;
                float feet=root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>().Select(LayoutRoadAuthoring.WorldRect)
                    .Where(r=>playerX>=r.xMin && playerX<=r.xMax).Max(r=>r.yMax);
                marker.transform.position = new Vector3(playerX, feet, 0);
                foreach (var layer in layers) typeof(ParallaxBackground).GetMethod("LateUpdate", PrivateMethods).Invoke(layer, null);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                    File.WriteAllBytes("Logs/LV1Buildings/" + view.Item1 + ".png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
            }
        }
        finally { Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
