using System;
using System.IO;
using System.Linq;
using Project.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class SpriteCropAuthoring
{
    public const string Folder = "Assets/Project/Prefab/Environment/CropBuildings";
    public const string MaskPath = "Assets/Project/Art/Tools/SpriteCropMask.asset";
    public static string PrefabPath(int index) => $"{Folder}/CropBuilding_B1_{index:00}.prefab";

    [MenuItem("Tools/Project/Art/Prepare Sprite Crop Prefabs")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var maps = Directory.GetFiles("Assets/Project/Resources/RoomLayouts", "*.prefab", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/Project/Scenes", "*.unity", SearchOption.AllDirectories))
            .ToDictionary(p=>p,p=>File.ReadAllBytes(p));
        EnsureFolder(Folder); EnsureFolder("Assets/Project/Art/Tools");
        Sprite mask = LoadMask();
        for (int i=1;i<=3;i++)
        {
            string path=PrefabPath(i);
            if (File.Exists(path)) continue;
            var sprite=AssetDatabase.LoadAllAssetsAtPath($"Assets/Project/Art/BG/B1/B1_Parallax_Foreground{i}.PNG").OfType<Sprite>().First();
            var root=Create(sprite,mask);
            try {root.name=$"CropBuilding_B1_{i:00}"; PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally {Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();
        foreach (var entry in maps)
            if (!entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key))) throw new InvalidOperationException("Crop tools modified a map or scene: "+entry.Key);
        Debug.Log("[Sprite Crop] Prepared three standalone crop prefabs. No scenes, layouts or source images changed.");
    }

    [MenuItem("GameObject/2D Object/Sprite Crop Frame", false, 20)]
    public static void CreateInScene(MenuCommand command)
    {
        var source = Selection.activeObject as Sprite;
        if (source == null && Selection.activeObject is Texture2D texture)
            source=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>().FirstOrDefault();
        if (source == null)
            source=AssetDatabase.LoadAllAssetsAtPath("Assets/Project/Art/BG/B1/B1_Parallax_Foreground1.PNG").OfType<Sprite>().First();
        var root=Create(source,LoadMask());
        var parent=command.context as GameObject;
        if(parent!=null && !EditorUtility.IsPersistent(parent)) GameObjectUtility.SetParentAndAlign(root,parent);
        root.GetComponent<SpriteCropFrame>().Refresh();
        Undo.RegisterCreatedObjectUndo(root,"Create sprite crop frame"); Selection.activeGameObject=root;
    }

    public static GameObject Create(Sprite sprite, Sprite maskSprite)
    {
        var root=new GameObject("Sprite Crop Frame",typeof(SortingGroup));
        root.GetComponent<SortingGroup>().sortingOrder=13;
        var imageHost=new GameObject("Artwork"); imageHost.transform.SetParent(root.transform,false);
        var image=imageHost.AddComponent<SpriteRenderer>(); image.sortingOrder=1;
        image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(1,"Foreground"))
            ?? AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        image.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;
        var maskHost=new GameObject("Crop Window"); maskHost.transform.SetParent(root.transform,false);
        var mask=maskHost.AddComponent<SpriteMask>(); mask.sprite=maskSprite; mask.alphaCutoff=.5f;
        mask.isCustomRangeActive=true; mask.frontSortingOrder=2; mask.backSortingOrder=0;
        root.AddComponent<SpriteCropFrame>().Configure(sprite,image,mask);
        return root;
    }

    public static Sprite LoadMask()
    {
        var existing=AssetDatabase.LoadAllAssetsAtPath(MaskPath).OfType<Sprite>().FirstOrDefault();
        if (existing != null) return existing;
        EnsureFolder("Assets/Project/Art/Tools");
        var texture=new Texture2D(1,1,TextureFormat.RGBA32,false) {name="SpriteCropMask",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixel(0,0,Color.white); texture.Apply();
        AssetDatabase.CreateAsset(texture,MaskPath);
        var sprite=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f,1,0,SpriteMeshType.FullRect);
        sprite.name="OpaqueRectangle"; AssetDatabase.AddObjectToAsset(sprite,texture); AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MaskPath);
        return AssetDatabase.LoadAllAssetsAtPath(MaskPath).OfType<Sprite>().Single();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
}
