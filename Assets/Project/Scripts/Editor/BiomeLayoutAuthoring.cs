using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BiomeLayoutAuthoring
{
    public const string B2Art = "Assets/Project/Art/BG/B2/";
    public const string CityModules = "Assets/Project/Art/BG/B1/LevelDesignTiles/LV_CityModules.png";
    public const string B2Background = "Assets/Project/Prefab/Environment/CombatBackground_B2.prefab";
    public const string MaterialPath = "Assets/Project/Art/BG/B1/LevelDesignTiles/LV_TerrainUnlit.mat";
    public const string PreviewFolder = "Logs/BiomeLayouts";

    [MenuItem("Tools/Project/Level Design/Build And Dress Both Biomes")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        ImportArt();
        CreateBackground();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var fill = AssetDatabase.LoadAllAssetsAtPath("Assets/Project/Art/BG/B1/LevelDesignTiles/LV_GroundFill.png").OfType<Sprite>().First();
        var cap = Load("IMG_4787", "B2_Walkway");
        var palette = Resources.Load<EncounterSO>(CombatRouteContent.EasyResourcePath);
        for (int level = 6; level <= 10; level++)
        {
            string path = LayoutPath(level);
            if (!File.Exists(path))
                LevelDesignLayoutBuilder.BuildLayout(LevelDesignLayoutBuilder.GetPlan(level),path,fill,cap,cap,material);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                LevelDesignSpawnBuilder.Configure(root,level,palette);
                ReplaceBackground(root,level);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        for (int level = 1; level <= 10; level++)
        {
            string path = LayoutPath(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before = PhysicsSnapshot(root);
                BiomeLayoutDressing.Apply(root,level);
                if (PhysicsSnapshot(root) != before)
                {
                    File.WriteAllText("Logs/biome-physics-before.txt",before);
                    File.WriteAllText("Logs/biome-physics-after.txt",PhysicsSnapshot(root));
                    throw new InvalidOperationException("Dressing changed collision: " + path);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        WireCatalog();
        AssetDatabase.SaveAssets();
        CaptureAll();
        Debug.Log("[Biome Layouts] Authored ten distinct layouts, five per biome; production catalog and boss progression connected.");
    }

    public static string LayoutPath(int level) => $"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";

    public static Sprite Load(string name,string slice=null)
    {
        string path = B2Art + name + ".PNG";
        return slice == null ? AssetDatabase.LoadAssetAtPath<Sprite>(path)
            : AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name==slice);
    }

    private static void ImportArt()
    {
        foreach (string path in Directory.GetFiles(B2Art,"*.PNG")) Import(path);
        if (!File.Exists(CityModules)) AssetDatabase.CopyAsset("Assets/Project/Art/BG/B1/B1_Parallax_Mid.PNG",CityModules);
        Import(CityModules);
        Slice(CityModules,new[] {
            ("Apartment",new Rect(0,0,456,920)),("Garage",new Rect(460,0,750,680)),
            ("Stairhouse",new Rect(1360,0,880,840)),("CornerShop",new Rect(2688,0,852,650)),
            ("Window",new Rect(60,650,160,190)),("NeonSign",new Rect(1430,340,150,360))
        });
        Slice(B2Art+"IMG_4787.PNG",new[] { ("B2_Walkway",OpaqueBounds(B2Art+"IMG_4787.PNG")) });
        Slice(B2Art+"IMG_4789.PNG",new[] { ("B2_WallFill",new Rect(64,40,128,128)),("B2_Beam",new Rect(0,0,1516,216)) });
    }

    private static void Import(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;
        if (importer.spriteImportMode!=SpriteImportMode.Multiple) importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100f;
        importer.maxTextureSize=4096;
        importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency=true;
        importer.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape=false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void Slice(string path,(string name,Rect rect)[] definitions)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories(); factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous=provider.GetSpriteRects();
        var slices=definitions.Select(d=>new SpriteRect { name=d.name,rect=d.rect,pivot=Vector2.one*.5f,
            alignment=SpriteAlignment.Center,spriteID=previous.FirstOrDefault(p=>p.name==d.name)?.spriteID ?? GUID.Generate() }).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply(); AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    }

    public static Rect OpaqueBounds(string path)
    {
        var texture=new Texture2D(2,2);
        try
        {
            texture.LoadImage(File.ReadAllBytes(path));
            var pixels=texture.GetPixels32();
            int left=texture.width,right=0,bottom=texture.height,top=0;
            for (int y=0;y<texture.height;y++) for (int x=0;x<texture.width;x++)
                if (pixels[y*texture.width+x].a>32) { left=Math.Min(left,x); right=Math.Max(right,x+1); bottom=Math.Min(bottom,y); top=Math.Max(top,y+1); }
            return new Rect(left,bottom,right-left,top-bottom);
        }
        finally { Object.DestroyImmediate(texture); }
    }

    private static void CreateBackground()
    {
        bool loaded=File.Exists(B2Background);
        var root = loaded ? PrefabUtility.LoadPrefabContents(B2Background) : new GameObject("CombatBackground_B2");
        try
        {
            Layer(root,"Sky",Load("B2_Parallax_BG"),new Vector3(0,15,0),SkyBackgroundRepair.SkyScale,-6,Vector2.one,140f);
            SkyBackgroundRepair.Configure(root.transform.Find("Sky").GetComponent<ParallaxBackground>());
            Layer(root,"DistantCity",Load("IMG_4771"),new Vector3(0,8.3f,0),1.45f,-5,new Vector2(.7f,.1f),100f);
            Layer(root,"NearCity",Load("IMG_4773"),new Vector3(0,4.7f,0),1.6f,-4,new Vector2(.3f,.025f),100f);
            BiomeMaterialAuthoring.ApplyIfPrepared(root);
            PrefabUtility.SaveAsPrefabAsset(root,B2Background);
        }
        finally { if (loaded) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    private static void Layer(GameObject root,string name,Sprite sprite,Vector3 position,float scale,int order,Vector2 speed,float width)
    {
        Transform layer=root.transform.Find(name);
        if (layer==null) { layer=new GameObject(name).transform; layer.SetParent(root.transform,false); }
        layer.localPosition=position; layer.localScale=Vector3.one*scale;
        var image=layer.GetComponent<SpriteRenderer>();
        if (image==null) image=layer.gameObject.AddComponent<SpriteRenderer>();
        image.sprite=sprite; image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        image.sortingOrder=order; image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous;
        image.size=new Vector2(width,sprite.bounds.size.y);
        layer.localScale=Vector3.one*scale;
        var parallax=layer.GetComponent<ParallaxBackground>();
        if (parallax==null) parallax=layer.gameObject.AddComponent<ParallaxBackground>();
        parallax.parallaxEffectMultiplier=speed; parallax.infiniteHorizontal=false; parallax.infiniteVertical=false; parallax.targetCamera=null;
    }

    private static void ReplaceBackground(GameObject root,int level)
    {
        var previous=root.transform.Find("CombatBackground_B1");
        if (previous!=null) Object.DestroyImmediate(previous.gameObject);
        var background=root.transform.Find("CombatBackground_B2");
        if (background==null) background=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(B2Background),root.transform)).transform;
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        float width=LevelDesignLayoutBuilder.WorldX(plan,1812)-LevelDesignLayoutBuilder.WorldX(plan,108);
        foreach (var image in background.GetComponentsInChildren<SpriteRenderer>())
        {
            var sky = image.GetComponent<ParallaxBackground>();
            if (sky != null && SkyBackgroundRepair.IsSky(sky)) { SkyBackgroundRepair.Configure(sky,width*2f+100f); continue; }
            image.size=new Vector2((width*2f+100f)/image.transform.lossyScale.x,image.sprite.bounds.size.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
    }

    private static void WireCatalog()
    {
        var map=Resources.Load<MapSO>("MapSO/Test_MapSO");
        var template=Resources.Load<RoomData>("MapSO/RoomData_Combat_01");
        for (int level=1;level<=10;level++)
        {
            string path=$"Assets/Project/Resources/MapSO/RoomData_Combat_{level:00}.asset";
            var room=AssetDatabase.LoadAssetAtPath<RoomData>(path);
            if (room==null) { room=Object.Instantiate(template); room.name=$"RoomData_Combat_{level:00}"; AssetDatabase.CreateAsset(room,path); }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath(level));
            using (var data=new SerializedObject(room))
            {
                data.FindProperty("roomId").stringValue=$"Combat_{level:00}";
                data.FindProperty("sceneName").stringValue="combat_1";
                data.FindProperty("layoutPrefab").objectReferenceValue=prefab;
                data.FindProperty("biomeIndex").intValue=level<=5 ? 0 : 1;
                data.FindProperty("displayName").stringValue=$"Biome {(level<=5 ? 1 : 2)} - Layout {(level-1)%5+1}";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!map.Rooms.Contains(room)) map.Rooms.Add(room);
            if (level>5)
            {
                var draft=LevelDesignLayoutBuilder.BuildRoomData(level,prefab);
                LevelDesignLayoutBuilder.BuildTestScene(level,draft);
            }
        }
        EditorUtility.SetDirty(map);
    }

    public static string PhysicsSnapshot(GameObject root)
    {
        return string.Join("\n",root.GetComponentsInChildren<Collider2D>(true).Select(c=>
        {
            var json=JObject.Parse(EditorJsonUtility.ToJson(c));
            // Unity refreshes this sprite cache even when auto-tiling cannot affect collision.
            if (c is BoxCollider2D box && !box.autoTiling)
                ((JObject)json.Properties().Single().Value).Remove("m_SpriteTilingProperty");
            return AnimationUtility.CalculateTransformPath(c.transform,root.transform)+"|"+
                c.transform.position.ToString("F6")+"|"+c.transform.lossyScale.ToString("F6")+"|"+json.ToString(Formatting.None);
        }));
    }

    [MenuItem("Tools/Project/Level Design/Capture Both Biome Layouts")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(PreviewFolder);
        for (int level=1;level<=10;level++)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var target=new RenderTexture(1600,900,24); target.Create();
            try
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath(level)),scene);
                var host=new GameObject("Biome Preview Camera"); SceneManager.MoveGameObjectToScene(host,scene);
                var camera=host.AddComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.orthographic=true;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=level<=5 ? new Color(.025f,.035f,.12f) : new Color(.23f,.035f,.15f);
                camera.GetUniversalAdditionalCameraData();
                var plan=LevelDesignLayoutBuilder.GetPlan(level);
                float left=LevelDesignLayoutBuilder.WorldX(plan,108),right=LevelDesignLayoutBuilder.WorldX(plan,1812);
                float high=plan.solids.Max(r=>LevelDesignLayoutBuilder.ToWorld(plan,r).yMax);
                camera.transform.position=new Vector3((left+right)*.5f,high*.5f,-100f);
                camera.orthographicSize=(right-left)/(2f*1600f/900f)+3f;
                Capture(camera,target,$"{PreviewFolder}/LV_{level:00}_Overview.png");
                camera.transform.position=new Vector3(left+12f,6f,-100f);
                var layers=root.GetComponentsInChildren<ParallaxBackground>(true).Where(p=>p.enabled).ToArray();
                const System.Reflection.BindingFlags methods=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                foreach(var layer in layers)
                {
                    layer.targetCamera=camera.transform;
                    typeof(ParallaxBackground).GetMethod("Start",methods).Invoke(layer,null);
                }
                var rects=plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
                for (int i=0;i<5;i++)
                {
                    float x=Mathf.Lerp(left+12f,right-12f,i*.5f);
                    var solids=rects.Where(r=>x>=r.xMin && x<=r.xMax).ToArray();
                    float floor=solids.Length>0 ? solids.Max(r=>r.yMax) : 0f;
                    camera.transform.position=new Vector3(x,floor+6f,-100f); camera.orthographicSize=9f;
                    string label=$"View_{i+1}";
                    if(i==3)
                    {
                        var building=rects.Skip(1).OrderByDescending(r=>r.height).First();
                        camera.transform.position=new Vector3(building.center.x,Mathf.Max(6f,building.yMax-1.5f),-100f);
                        label="Building";
                    }
                    else if(i==4)
                    {
                        var bridge=rects.Skip(1).Where(r=>r.height<3.5f && r.yMin>1f).OrderByDescending(r=>r.width).ToArray();
                        if(bridge.Length==0) continue;
                        camera.transform.position=new Vector3(bridge[0].center.x,bridge[0].yMin-3f,-100f);
                        label="Underpass";
                    }
                    foreach(var layer in layers) typeof(ParallaxBackground).GetMethod("LateUpdate",methods).Invoke(layer,null);
                    Capture(camera,target,$"{PreviewFolder}/LV_{level:00}_{label}.png");
                }
            }
            finally { Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    private static void Capture(Camera camera,RenderTexture target,string path)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
        var previous=RenderTexture.active;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try { RenderTexture.active=target; image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
        finally { RenderTexture.active=previous; Object.DestroyImmediate(image); }
    }
}
