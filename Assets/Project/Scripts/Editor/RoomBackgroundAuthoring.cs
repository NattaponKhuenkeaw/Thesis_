using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RoomBackgroundAuthoring
{
    public const string BossScene = "Assets/Project/Scenes/Test_Boss.unity";
    public const string SceneGroup = "RoomBiomeScenery";
    public static string EnvironmentPath(string kind, int biome) => $"Assets/Project/Prefab/Environment/{kind}Environment_B{biome}.prefab";
    public static Transform BiomeOneRoot(GameObject root) => root.GetComponentInChildren<RoomBiomeEnvironment>(true)?.BiomeOneEnvironment?.transform ?? root.transform;
    public const string InvertedCityGroup = "InvertedCityCeiling";

    [MenuItem("Tools/Project/Room Backgrounds/Add Biome 2 Boss Inverted City")]
    public static void AddBossInvertedCityAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        string path=EnvironmentPath("Boss",2);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var first=AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath("Boss",1));
            var images=root.GetComponentsInChildren<SpriteRenderer>(true);
            var states=images.Select(r=>EditorJsonUtility.ToJson(r)).ToArray();
            string physics=BiomeLayoutAuthoring.PhysicsSnapshot(root);
            AddBossInvertedCity(root,first);
            if(physics!=BiomeLayoutAuthoring.PhysicsSnapshot(root) || !states.SequenceEqual(images.Select(r=>EditorJsonUtility.ToJson(r))))
                throw new InvalidOperationException("The inverted skyline changed existing scenery or collision.");
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        CaptureAll();
        Debug.Log("[Boss Background] Added a native-scale inverted biome-two skyline matching biome one's hanging-city positions. Existing art, arena and gameplay unchanged.");
    }

    public static SpriteRenderer[] InvertedCityReference(GameObject firstBiome)
    {
        return firstBiome.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled && r.sprite!=null
            && Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(r.sprite))=="B1_Parallax_Far"
            && r.transform.localToWorldMatrix.MultiplyVector(Vector3.up).y*(r.flipY ? -1f : 1f)<0).ToArray();
    }

    public static void AddBossInvertedCity(GameObject market, GameObject firstBiome)
    {
        if(market.transform.Find(InvertedCityGroup)!=null) return;
        var templates=InvertedCityReference(firstBiome);
        if(templates.Length==0) throw new InvalidOperationException("No inverted city reference found in the biome-one boss room.");
        var near=market.transform.Find("CombatBackground_B2/NearCity").GetComponent<SpriteRenderer>();
        var group=new GameObject(InvertedCityGroup).transform; group.SetParent(market.transform,false);
        float scale=Mathf.Abs(near.transform.lossyScale.x);
        for(int i=0;i<templates.Length;i++)
        {
            var reference=templates[i];
            var host=new GameObject($"InvertedSkyline_{i+1:00}"); host.transform.SetParent(group,false);
            host.layer=near.gameObject.layer;
            var image=host.AddComponent<SpriteRenderer>();
            image.sprite=near.sprite; image.sharedMaterial=near.sharedMaterial;
            image.color=near.color; image.sortingLayerID=near.sortingLayerID; image.sortingOrder=near.sortingOrder;
            image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous;
            image.size=new Vector2(reference.bounds.size.x/scale,image.sprite.bounds.size.y);
            // Align the hanging edge; tile the width while preserving native pixel proportions.
            host.transform.localScale=new Vector3(scale,-scale,1f);
            host.transform.position=new Vector3(reference.bounds.center.x,reference.bounds.min.y+image.sprite.bounds.max.y*scale,near.transform.position.z);
            var originalLayer=reference.GetComponent<ParallaxBackground>();
            if(originalLayer!=null)
            {
                var layer=host.AddComponent<ParallaxBackground>(); EditorUtility.CopySerialized(originalLayer,layer);
                layer.targetCamera=null;
            }
        }
    }

    [MenuItem("Tools/Project/Room Backgrounds/Separate And Connect Both Biomes")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        BiomeMaterialAuthoring.PrepareMaterials();
        var audit = new List<string>();
        foreach (string kind in new[] { "Rest", "Level", "Boss" })
        {
            string path = kind == "Boss" ? LayoutRoadAuthoring.BossPath : SafeRoomBiomeAuthoring.LayoutPath(kind);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string physics = BiomeLayoutAuthoring.PhysicsSnapshot(root);
                var gameplay = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b=>!(b is RoomBiomeEnvironment) && !(b is ParallaxBackground)).ToArray();
                var bindings = gameplay.Select(b=>EditorJsonUtility.ToJson(b)).ToArray();
                if (kind == "Boss") ConfigureBoss(root);
                else ConfigureSafeRoom(root, kind);
                if (physics != BiomeLayoutAuthoring.PhysicsSnapshot(root) || !bindings.SequenceEqual(gameplay.Select(b=>EditorJsonUtility.ToJson(b))))
                    throw new InvalidOperationException("Separating artwork changed gameplay: " + kind);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                audit.Add(kind + ": independent B1/B2 scenery, unchanged collision, gameplay and UI bindings.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        ConfigureScene("Rest", "Assets/Project/Scenes/Test_Rest.unity");
        ConfigureScene("Level", "Assets/Project/Scenes/Test_Level.unity");
        if (!File.Exists(BossScene)) AssetDatabase.CopyAsset("Assets/Project/Scenes/combat scene/combat_1.unity", BossScene);
        ConfigureScene("Boss", BossScene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/RoomBackgrounds");
        File.WriteAllLines("Logs/RoomBackgrounds/gameplay-audit.txt", audit);
        CaptureAll();
        Debug.Log("[Room Backgrounds] Boss/Rest/Level have independent B1/B2 scenery prefabs and editor preview controls. Production switching still follows run progression.");
    }

    private static void ConfigureSafeRoom(GameObject root, string kind)
    {
        var controller = root.GetComponentsInChildren<RoomBiomeEnvironment>(true).Single();
        if (controller.BiomeOneEnvironment == null)
        {
            var group = new GameObject(kind + "Environment_B1"); group.transform.SetParent(root.transform, false);
            root.transform.Find(SafeRoomBackgroundAuthoring.BackgroundName).SetParent(group.transform, true);
            root.transform.Find(SafeRoomFloorAuthoring.BiomeOneGroup).SetParent(group.transform, true);
            SaveAndReplace(group, EnvironmentPath(kind, 1), controller.transform);
        }
        Bind(controller, controller.transform.Find(kind + "Environment_B1").gameObject,
            controller.transform.Find(kind + "Environment_B2").gameObject, root);
    }

    private static void ConfigureBoss(GameObject root)
    {
        var controller = root.GetComponentInChildren<RoomBiomeEnvironment>(true);
        if (controller == null)
        {
            var host = new GameObject("RoomBiomeEnvironment"); host.transform.SetParent(root.transform, false);
            controller = host.AddComponent<RoomBiomeEnvironment>();
        }
        if (controller.BiomeOneEnvironment == null || controller.BiomeOneEnvironment.transform.Find("SourceHierarchy") == null)
        {
            var previous = controller.BiomeOneEnvironment;
            var previousImages = previous == null ? Array.Empty<SpriteRenderer>() : previous.GetComponentsInChildren<SpriteRenderer>(true);
            var group = new GameObject("BossEnvironment_B1"); group.transform.SetParent(root.transform, false);
            var hierarchy = new GameObject("SourceHierarchy").transform; hierarchy.SetParent(group.transform, false);
            var transforms = new Dictionary<Transform, Transform> { [root.transform] = hierarchy };
            var sources = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>!r.transform.IsChildOf(controller.transform)
                && AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Project/Art/BG/B1/",StringComparison.Ordinal)).ToArray();
            foreach (var image in sources)
            {
                var oldImage=previousImages.FirstOrDefault(r=>r.sprite==image.sprite && r.name.StartsWith(image.name+"_",StringComparison.Ordinal));
                if(previous!=null ? oldImage==null : !image.enabled) continue;
                var host=CloneTransform(image.transform,transforms);
                var copy = host.gameObject.AddComponent<SpriteRenderer>(); EditorUtility.CopySerialized(image, copy); copy.enabled=true;
                // Preserve reflected/non-uniform parent matrices, not decomposed world rotation/scale.
                for(int i=0;i<16;i++)
                    if(Mathf.Abs(host.localToWorldMatrix[i]-image.transform.localToWorldMatrix[i])>.0001f)
                        throw new InvalidOperationException("Boss scenery transform changed: "+image.name);
                var parallax = image.GetComponent<ParallaxBackground>();
                if (parallax != null)
                {
                    var layer = host.gameObject.AddComponent<ParallaxBackground>(); EditorUtility.CopySerialized(parallax, layer);
                    layer.enabled=oldImage!=null ? oldImage.GetComponent<ParallaxBackground>().enabled : parallax.enabled;
                    layer.targetCamera = null; parallax.enabled = false;
                }
                image.enabled = false;
            }
            // Neutral collider/arena guide sprites are shared geometry, not biome scenery.
            foreach(var image in root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>!r.transform.IsChildOf(controller.transform)
                && AssetDatabase.GetAssetPath(r.sprite)=="Assets/Project/Art/BG/background5.png"))
                if(previousImages.Any(r=>r.sprite==image.sprite && r.name.StartsWith(image.name+"_",StringComparison.Ordinal))) image.enabled=true;
            if(previous!=null) Object.DestroyImmediate(previous);
            SaveAndReplace(group, EnvironmentPath("Boss", 1), controller.transform);
        }
        if (controller.transform.Find("BossEnvironment_B2") == null)
        {
            var market = BuildBossMarket(root);
            SaveAndReplace(market, EnvironmentPath("Boss", 2), controller.transform);
        }
        Bind(controller, controller.transform.Find("BossEnvironment_B1").gameObject,
            controller.transform.Find("BossEnvironment_B2").gameObject, root);
    }

    private static Transform CloneTransform(Transform source, Dictionary<Transform,Transform> copies)
    {
        if(copies.TryGetValue(source,out var found)) return found;
        var parent=CloneTransform(source.parent,copies);
        var copy=new GameObject(source.name).transform; copy.SetParent(parent,false);
        copy.localPosition=source.localPosition; copy.localRotation=source.localRotation; copy.localScale=source.localScale;
        copy.gameObject.layer=source.gameObject.layer; copy.gameObject.SetActive(source.gameObject.activeSelf);
        copies[source]=copy; return copy;
    }

    private static GameObject BuildBossMarket(GameObject room)
    {
        var root = new GameObject("BossEnvironment_B2");
        var roads = room.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.transform.parent != null && r.transform.parent.name == "Road"
            && r.sprite == LayoutRoadAuthoring.BossRoadSprite()).ToArray();
        float left = roads.Min(r=>r.bounds.min.x), right = roads.Max(r=>r.bounds.max.x);
        var source = roads[0];
        float floor = source.transform.position.y + (LayoutRoadAuthoring.OpaqueTop(source.sprite)-source.sprite.pivot.y)/source.sprite.pixelsPerUnit*source.transform.lossyScale.y;
        float center = (left+right)*.5f;
        var background = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.B2Background), root.transform);
        var near = background.transform.Find("NearCity").GetComponent<SpriteRenderer>();
        background.transform.position += new Vector3(center, floor-near.bounds.min.y, 0);
        foreach (var layer in background.GetComponentsInChildren<ParallaxBackground>(true))
        {
            if (SkyBackgroundRepair.IsSky(layer)) SkyBackgroundRepair.Configure(layer, (right-left)*2+100);
            else
            {
                var image = layer.GetComponent<SpriteRenderer>();
                image.size = new Vector2(((right-left)*2+100)/image.transform.lossyScale.x, image.size.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(background.transform);
        var road = LayoutRoadAuthoring.RoadSprite(6);
        float top = LayoutRoadAuthoring.OpaqueTop(road);
        LobbyBiomeAuthoring.Tile(root.transform, "Road_B2", road, new Vector2(center, floor+(road.pivot.y-top)/road.pixelsPerUnit),
            new Vector2(right-left,road.rect.height/road.pixelsPerUnit),18);
        var water = BiomeLayoutAuthoring.Load("B2_Parallax_Ground");
        LobbyBiomeAuthoring.Tile(root.transform,"StreetReflection_B2",water,
            new Vector2(center,floor-top/road.pixelsPerUnit-water.bounds.size.y*.7f),
            new Vector2((right-left)/1.4f,water.bounds.size.y),17).transform.localScale=Vector3.one*1.4f;
        LobbyBiomeAuthoring.Add(root.transform,"ArenaShopLeft","IMG_4781",new Vector2(left+7,floor),1.35f,2);
        LobbyBiomeAuthoring.Add(root.transform,"ArenaShutterRight","IMG_4780",new Vector2(right-7,floor),1.35f,2);
        LobbyBiomeAuthoring.Tile(root.transform,"RearArenaBeam",BiomeLayoutAuthoring.Load("IMG_4789","B2_Beam"),
            new Vector2(center,floor+17),new Vector2(right-left+20,2.16f),-3);
        for (int i=0;i<4;i++)
        {
            float x=Mathf.Lerp(left+3,right-3,i/3f);
            LobbyBiomeAuthoring.Add(root.transform,"ArenaLantern_"+i,i%2==0 ? "IMG_4776" : "IMG_4778",new Vector2(x,floor+12),1.1f,-2);
        }
        AddBossInvertedCity(root,AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath("Boss",1)));
        BiomeMaterialAuthoring.Apply(root);
        return root;
    }

    private static void SaveAndReplace(GameObject group, string path, Transform parent)
    {
        BiomeMaterialAuthoring.Apply(group);
        PrefabUtility.SaveAsPrefabAsset(group, path);
        Object.DestroyImmediate(group);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
    }

    private static void Bind(RoomBiomeEnvironment controller, GameObject first, GameObject second, GameObject root)
    {
        first.SetActive(true); second.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(first);
        PrefabUtility.RecordPrefabInstancePropertyModifications(second);
        var images = root.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(r=>AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/Project/Art/BG/B1/",StringComparison.Ordinal)).ToArray();
        using (var data = new SerializedObject(controller))
        {
            data.FindProperty("biomeOneEnvironment").objectReferenceValue=first;
            data.FindProperty("biomeTwoEnvironment").objectReferenceValue=second;
            var art = data.FindProperty("biomeOneArtwork"); art.arraySize=images.Length;
            for (int i=0;i<images.Length;i++) art.GetArrayElementAtIndex(i).objectReferenceValue=images[i];
            var layers=images.Select(r=>r.GetComponent<ParallaxBackground>()).Where(p=>p!=null).ToArray();
            var scripts=data.FindProperty("biomeOneParallax"); scripts.arraySize=layers.Length;
            for (int i=0;i<layers.Length;i++) scripts.GetArrayElementAtIndex(i).objectReferenceValue=layers[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureScene(string kind, string path)
    {
        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        var roots=scene.GetRootGameObjects();
        string physics=LobbyBiomeAuthoring.PhysicsSnapshot(scene);
        var gameplay=roots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>!(b is RoomBiomeEnvironment) && !(b is ParallaxBackground)).ToArray();
        var bindings=gameplay.Select(b=>EditorJsonUtility.ToJson(b)).ToArray();
        var old=roots.FirstOrDefault(r=>r.name==SceneGroup);
        if (old!=null) Object.DestroyImmediate(old);
        roots=roots.Where(r=>r!=null).ToArray();
        foreach (var image in roots.Where(r=>r!=old).SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(true)).Where(SafeRoomBiomeAuthoring.IsOriginalArtwork))
        {
            image.enabled=false; PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            var layer=image.GetComponent<ParallaxBackground>();
            if(layer!=null) { layer.enabled=false; PrefabUtility.RecordPrefabInstancePropertyModifications(layer); }
        }
        var group=new GameObject(SceneGroup);
        var controller=group.AddComponent<RoomBiomeEnvironment>();
        var first=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath(kind,1)),group.transform);
        var second=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath(kind,2)),group.transform);
        if(kind!="Boss")
        {
            // Scene shells and production layouts can have different floor origins.
            var layout=AssetDatabase.LoadAssetAtPath<GameObject>(SafeRoomBiomeAuthoring.LayoutPath(kind));
            float layoutFloor=SafeRoomBiomeAuthoring.FloorHeight(new[]{layout});
            Physics2D.SyncTransforms();
            var floors=roots.SelectMany(r=>r.GetComponentsInChildren<Collider2D>(true))
                .Where(c=>!c.isTrigger && (c.gameObject.layer==LayerMask.NameToLayer("Ground") || c.name.IndexOf("Ground",StringComparison.OrdinalIgnoreCase)>=0))
                .Select(c=>c is BoxCollider2D box ? LayoutRoadAuthoring.WorldRect(box) : new Rect(c.bounds.min,c.bounds.size))
                .OrderByDescending(r=>r.width).ToArray();
            float sceneFloor=floors.Length>0 ? floors[0].yMax : layoutFloor;
            first.transform.position+=Vector3.up*(sceneFloor-layoutFloor);
            var production=layout.GetComponentInChildren<RoomBiomeEnvironment>(true);
            second.transform.position=production.BiomeTwoEnvironment.transform.position+Vector3.up*(sceneFloor-layoutFloor);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(first.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(second.transform);
        Bind(controller,first,second,group);
        if(physics!=LobbyBiomeAuthoring.PhysicsSnapshot(scene) || !bindings.SequenceEqual(gameplay.Select(b=>EditorJsonUtility.ToJson(b))))
            throw new InvalidOperationException("Scene background wiring changed gameplay: "+path);
        var host=roots.SelectMany(r=>r.GetComponentsInChildren<RoomLayoutHost>(true)).FirstOrDefault();
        if(host!=null)
        {
            using(var data=new SerializedObject(host))
            {
                var baked=data.FindProperty("bakedSceneContent");
                for(int i=baked.arraySize-1;i>=0;i--)
                    if(baked.GetArrayElementAtIndex(i).objectReferenceValue==null) { baked.GetArrayElementAtIndex(i).objectReferenceValue=null; baked.DeleteArrayElementAtIndex(i); }
                int next=baked.arraySize; baked.InsertArrayElementAtIndex(next); baked.GetArrayElementAtIndex(next).objectReferenceValue=group;
                if(kind=="Boss") data.FindProperty("bootstrapRoom").objectReferenceValue=Resources.Load<RoomData>("MapSO/RoomData_Boss_01");
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorSceneManager.SaveScene(scene);
    }

    public static void CaptureAll()
    {
        Directory.CreateDirectory("Logs/RoomBackgrounds");
        foreach(string kind in new[]{"Boss","Rest","Level"})
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var target=new RenderTexture(1280,720,24); target.Create();
            try
            {
                string path=kind=="Boss" ? LayoutRoadAuthoring.BossPath : SafeRoomBiomeAuthoring.LayoutPath(kind);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
                var controller=root.GetComponentInChildren<RoomBiomeEnvironment>(true);
                var cameraHost=new GameObject("Room art capture"); SceneManager.MoveGameObjectToScene(cameraHost,scene);
                var camera=cameraHost.AddComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.orthographic=true; camera.orthographicSize=9;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.02f,.03f,.08f); camera.GetUniversalAdditionalCameraData();
                float floor=kind=="Boss" ? controller.BiomeTwoEnvironment.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Road_B2").bounds.max.y
                    : SafeRoomBiomeAuthoring.FloorHeight(new[]{root});
                float center=kind=="Boss" ? controller.BiomeTwoEnvironment.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Road_B2").bounds.center.x : 0;
                cameraHost.transform.position=new Vector3(center,floor+6,-100);
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled=false;
                for(int biome=0;biome<2;biome++)
                {
                    controller.Apply(Resources.Load<MapSO>("MapSO/Test_MapSO"),biome);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var previous=RenderTexture.active; var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    try { RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); File.WriteAllBytes($"Logs/RoomBackgrounds/{kind}_B{biome+1}.png",image.EncodeToPNG()); }
                    finally {RenderTexture.active=previous; Object.DestroyImmediate(image);}
                }
            }
            finally {Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
