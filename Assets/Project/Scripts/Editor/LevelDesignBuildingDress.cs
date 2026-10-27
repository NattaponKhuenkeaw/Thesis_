using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LevelDesignBuildingDress
{
    public const string Sheet = "Assets/Project/Art/BG/B1/LevelDesignTiles/LV_Layout02BuildingPieces.png";
    public const string ReflectionSource = "Assets/Project/Art/BG/B1/B1_Parallax_Ground2.PNG";
    private const string Source = "Assets/Project/Art/BG/B1/B1_Parallax_Foreground2.PNG";
    // Ground (1) in Combat 02 uses 100 PPU and a 2.6398 transform scale.
    private const float PixelsPerUnit = 100f / 2.6398f;

    public static void ApplyAndCapture()
    {
        ApplyAll();
        LevelDesignLayoutPreview.CaptureAll();
    }

    public static void ApplyReflectionsAndCapture()
    {
        ApplyReflections();
        LevelDesignLayoutPreview.CaptureAll();
    }

    [MenuItem("Tools/Project/Level Design/Add Layout 02 Reflection Below Existing LV Floors")]
    public static void ApplyReflections()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Resources/RoomLayouts/RoomLayout_Combat_02.prefab")
            .GetComponentsInChildren<SpriteRenderer>(true)
            .First(r=>AssetDatabase.GetAssetPath(r.sprite)==ReflectionSource);
        Vector2 referenceSize = Vector2.Scale(reference.size,reference.transform.lossyScale);
        if (referenceSize.x <= 0f || referenceSize.y <= 0f)
            throw new InvalidOperationException("Combat 02 reflection has invalid dimensions.");
        for (int level = 1; level <= 5; level++)
        {
            string path = $"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string physicsBefore = PhysicsSnapshot(root);
                var floor = root.transform.Find("Terrain/Ground_01").GetComponent<BoxCollider2D>();
                var old = root.transform.Find("StreetReflection");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var reflection = new GameObject("StreetReflection");
                reflection.transform.SetParent(root.transform,false);
                Vector2 size = Vector2.Scale(floor.size,floor.transform.lossyScale);
                Vector2 center = floor.transform.TransformPoint(floor.offset);
                float left = center.x-size.x*.5f;
                float top = center.y+size.y*.5f;
                int count = Mathf.Max(1,Mathf.RoundToInt(size.x/referenceSize.x));
                float width = size.x/count;
                for (int i = 0; i < count; i++)
                    AddPiece(reflection.transform,$"Reflection_{i+1:00}",reference.sprite,
                        floor.GetComponent<SpriteRenderer>().sharedMaterial,
                        new Vector2(left+(i+.5f)*width,top-referenceSize.y*.5f),
                        new Vector2(width,referenceSize.y),reference.sortingOrder,false);
                // Keep the solid floor and its surface strip; only the old opaque artwork is hidden.
                floor.GetComponent<SpriteRenderer>().enabled = false;
                if (PhysicsSnapshot(root) != physicsBefore)
                    throw new InvalidOperationException("Reflection dressing changed floor physics.");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LV Reflection] Added Combat 02 reflection art below all five LV floors. Physics preserved.");
    }

    [MenuItem("Tools/Project/Level Design/Use Layout 02 Building Art On Existing LV Ground")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        Dictionary<string,Sprite> pieces = ImportPieces();
        for (int level = 1; level <= 5; level++)
        {
            string path = $"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            if (!File.Exists(path)) throw new FileNotFoundException("Create the layout before dressing it.",path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string physicsBefore = PhysicsSnapshot(root);
                Transform terrain = root.transform.Find("Terrain");
                foreach (Transform block in terrain)
                {
                    if (block.name == "Ground_01") continue;
                    BoxCollider2D collider = block.GetComponent<BoxCollider2D>();
                    Vector2 size = collider.size;
                    Transform old = block.Find("BuildingFacade");
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    var facade = new GameObject("BuildingFacade");
                    facade.transform.SetParent(block,false);
                    Material material = block.GetComponent<SpriteRenderer>().sharedMaterial;
                    AddPiece(facade.transform,"BuildingBody",pieces["Layout02_BuildingFace"],material,
                        Vector2.zero,size,12,true);
                    float edgeWidth = Mathf.Min(.5f,size.x*.2f);
                    AddPiece(facade.transform,"LeftEdge",pieces["Layout02_WallEdge"],material,
                        new Vector2((-size.x+edgeWidth)*.5f,0),new Vector2(edgeWidth,size.y),13,false);
                    AddPiece(facade.transform,"RightEdge",pieces["Layout02_WallEdge"],material,
                        new Vector2((size.x-edgeWidth)*.5f,0),new Vector2(edgeWidth,size.y),13,false);
                    facade.transform.Find("RightEdge").GetComponent<SpriteRenderer>().flipX = true;
                    foreach (Transform child in block)
                    {
                        if (child.name != "WalkingSurface") continue;
                        SpriteRenderer roof = child.GetComponent<SpriteRenderer>();
                        Vector2 roofSize = roof.size;
                        roof.sprite = pieces["Layout02_RoofEdge"];
                        roof.drawMode = SpriteDrawMode.Tiled;
                        roof.size = roofSize;
                        roof.sortingOrder = 14;
                    }
                }
                if (PhysicsSnapshot(root) != physicsBefore)
                    throw new InvalidOperationException("Building dressing must not change collision or layout transforms.");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LV Buildings] Replaced window facades with Combat 02 Ground (1) art in five existing layouts. Physics preserved.");
    }

    private static void AddPiece(Transform parent,string name,Sprite sprite,Material material,
        Vector2 center,Vector2 size,int order,bool tiled)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent,false);
        part.transform.localPosition = center;
        var image = part.AddComponent<SpriteRenderer>();
        image.sprite = sprite; image.sharedMaterial = material; image.sortingOrder = order;
        image.color = Color.white;
        if (tiled) { image.drawMode = SpriteDrawMode.Tiled; image.size = size; }
        else part.transform.localScale = new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1f);
    }

    private static string PhysicsSnapshot(GameObject root)
    {
        return string.Join("\n",root.GetComponentsInChildren<Collider2D>(true).Select(c=>
            AnimationUtility.CalculateTransformPath(c.transform,root.transform)+"|"+
            c.transform.position+"|"+c.transform.lossyScale+"|"+EditorJsonUtility.ToJson(c)));
    }

    private static Dictionary<string,Sprite> ImportPieces()
    {
        if (!File.Exists(Sheet) && !AssetDatabase.CopyAsset(Source,Sheet))
            throw new IOException("Could not copy the existing B1 building sheet.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        EditorUtility.SetDirty(importer); importer.SaveAndReimport();
        importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        SpriteRect[] previous = provider.GetSpriteRects();
        var slices = new[] {
            Slice("Layout02_BuildingFace",new Rect(96,96,448,448),previous),
            Slice("Layout02_WallEdge",new Rect(16,96,64,512),previous),
            Slice("Layout02_RoofEdge",new Rect(744,864,336,52),previous)
        };
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            slices.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply(); EditorUtility.SetDirty(provider.targetObject);
        AssetDatabase.WriteImportSettingsIfDirty(Sheet);
        AssetDatabase.ImportAsset(Sheet,ImportAssetOptions.ForceUpdate);
        var result = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToDictionary(s=>s.name);
        foreach (SpriteRect slice in slices)
            if (result[slice.name].rect != slice.rect) throw new InvalidOperationException("Building slice was not retained.");
        return result;
    }

    private static SpriteRect Slice(string name,Rect rect,SpriteRect[] previous)
    {
        var old = previous.FirstOrDefault(s=>s.name==name);
        return new SpriteRect { name=name,rect=rect,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,
            spriteID=old != null ? old.spriteID : GUID.Generate() };
    }
}
