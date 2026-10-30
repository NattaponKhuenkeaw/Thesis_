using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LevelDesignLayoutBuilder
{
    public const string LayoutFolder = "Assets/Project/Resources/RoomLayouts/LV";
    public const string RoomFolder = "Assets/Project/Resources/MapSO/LV";
    public const string SceneFolder = "Assets/Project/Scenes/LevelDesign";
    public const float WallThickness = 6f;
    public const float HorizontalScale = .05f;
    public const float VerticalScale = .04f;
    private const string ArtFolder = "Assets/Project/Art/BG/B1/LevelDesignTiles";
    private const string ArtSource = "Assets/Project/Art/BG/B1/";
    private const string ShellScene = "Assets/Project/Scenes/combat scene/combat_1.unity";
    private const string BackgroundPath = "Assets/Project/Prefab/Environment/CombatBackground_B1.prefab";

    public sealed class Plan
    {
        public int level;
        public float baseline;
        public Vector2 start;
        public Vector2 exit;
        public Rect[] solids;
        public Vector3[] platforms;
        public Vector3[] gates;
    }

    // Pixel coordinates come from the five approved diagrams. Rects contain solid space only.
    public static Plan GetPlan(int level)
    {
        switch (level)
        {
            case 1: return new Plan {
                level = 1, baseline = 848, start = new Vector2(155, 848), exit = new Vector2(1812, 706),
                solids = new[] { R(108,848,1812,972), R(432,688.75f,545,848), R(790,635.125f,889,848), R(1192,707.5f,1812,848) },
                platforms = new[] { P(762,769.25f,72), P(762,690.5f,72) },
                gates = new[] { G(428), G(791), G(1188), G(1526) }
            };
            case 2: return new Plan {
                level = 2, baseline = 867, start = new Vector2(155,747), exit = new Vector2(1812,515),
                solids = new[] { R(108,867,1812,972), R(108,747,401,867), R(708,695,1055,867), R(1397,691,1812,867), R(1505,515,1812,691) },
                platforms = new[] { P(679,788.25f,78), P(679,709.5f,78), P(1362,788.25f,90), P(1362,709.5f,90), P(1482,612.25f,72), P(1482,533.5f,72) },
                gates = new[] { G(401), G(708), G(1054), G(1401) }
            };
            case 3: return new Plan {
                level = 3, baseline = 861, start = new Vector2(155,861), exit = new Vector2(1812,861),
                solids = new[] { R(108,861,1812,972), R(415,737,729,861), R(729,671,1484,861), R(1018,496,1393,671) },
                platforms = new[] { P(991,592.25f,90), P(991,513.5f,90) },
                gates = new[] { G(415), G(729), G(1018), G(1393) }
            };
            case 4: return new Plan {
                level = 4, baseline = 917, start = new Vector2(155,578), exit = new Vector2(1812,917),
                solids = new[] { R(108,917,1812,972), R(108,762,1136,917), R(108,578,409,762), R(409,639,714,762),
                    R(1049,484,1136,762), R(1136,484,1562,550), R(1562,484,1628,641), R(1301,641,1432,816), R(1432,762,1812,816) },
                platforms = new[] { P(1024,683.25f,90), P(1024,604.5f,90), P(1024,525.75f,90) },
                gates = new[] { G(407), G(714), G(1049), G(1386,484), G(1556,917,816) }
            };
            case 5: return new Plan {
                level = 5, baseline = 865.4f, start = new Vector2(155,865.4f), exit = new Vector2(1812,865.4f),
                solids = new[] { R(108,865.4f,1812,961.4f), R(390.7f,662.9f,661.1f,865.4f),
                    R(661.1f,596.5f,805.9f,865.4f), R(805.9f,359.4f,1161.1f,865.4f), R(1161.1f,425.2f,1486.8f,865.4f) },
                platforms = new[] { P(369,827.9f,90), P(369,749.15f,90), P(369,670.4f,90),
                    P(784.25f,529f,90), P(784.25f,450.25f,90), P(784.25f,371.5f,90) },
                gates = new[] { G(388), G(664), G(1162), G(1487) }
            };
            default: return Biome2LevelPlans.GetPlan(level);
        }
    }

    private static Rect R(float left, float top, float right, float bottom) => new Rect(left, top, right-left, bottom-top);
    private static Vector3 P(float x, float top, float width) => new Vector3(x, top, width);
    // Missing vertical limits mean a full-height gate. Explicit tops seal underpasses against their solid ceiling.
    private static Vector3 G(float x, float bottom = -1, float top = -1) => new Vector3(x, bottom, top);
    // Stretch flat fighting space only. Jump gaps, stair offsets and vertical travel stay unchanged.
    public static Vector2[] CombatExpansionSpans(int level)
    {
        switch (level)
        {
            case 1: return new[] { new Vector2(180,340),new Vector2(570,710),new Vector2(930,1090),new Vector2(1280,1410),new Vector2(1600,1750) };
            case 2: return new[] { new Vector2(200,320),new Vector2(480,620),new Vector2(800,950),new Vector2(1140,1290),new Vector2(1550,1760) };
            case 3: return new[] { new Vector2(200,330),new Vector2(490,640),new Vector2(800,920),new Vector2(1100,1290),new Vector2(1520,1760) };
            case 4: return new[] { new Vector2(200,330),new Vector2(480,630),new Vector2(800,950),new Vector2(1160,1300),new Vector2(1460,1530),new Vector2(1640,1770) };
            case 5: return new[] { new Vector2(200,300),new Vector2(470,580),new Vector2(900,1080),new Vector2(1250,1390),new Vector2(1570,1750) };
            default: return Biome2LevelPlans.ExpansionSpans(level);
        }
    }

    public static float WorldX(Plan plan,float pixelX)
    {
        float x=(pixelX-960f)*HorizontalScale;
        Vector2[] spans=CombatExpansionSpans(plan.level);
        for (int i=0;i<spans.Length;i++)
            x+=((plan.level==4 || plan.level==9) && i==4 ? 35f : 30f)*Mathf.Clamp01((pixelX-spans[i].x)/(spans[i].y-spans[i].x));
        return x;
    }

    public static Vector2 ToWorld(Plan plan, Vector2 pixel) => new Vector2(WorldX(plan,pixel.x), (plan.baseline-pixel.y)*(plan.level == 10 ? .035f : VerticalScale));
    public static Rect ToWorld(Plan plan, Rect pixel)
    {
        Vector2 lower = ToWorld(plan, new Vector2(pixel.xMin, pixel.yMax));
        return new Rect(lower, new Vector2(WorldX(plan,pixel.xMax)-WorldX(plan,pixel.xMin), pixel.height*(plan.level == 10 ? .035f : VerticalScale)));
    }

    [MenuItem("Tools/Project/Level Design/Create Missing LV1-LV5 Layouts")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating layouts.");
        EnsureFolder(LayoutFolder); EnsureFolder(RoomFolder); EnsureFolder(SceneFolder); EnsureFolder(ArtFolder);
        Material material = TerrainMaterial();
        Sprite fill = ImportPiece("LV_GroundFill", "B1_Parallax_Foreground1.PNG", new Rect(128,128,128,128), 64f);
        Sprite cap = ImportPiece("LV_FloorCap", "B1_Parallax_Tilemap.PNG", OpaqueBounds(ArtSource+"B1_Parallax_Tilemap.PNG"), 128f);
        Sprite platform = ImportPiece("LV_PlatformCap", "B1_Asset_LongPlatform.PNG", OpaqueBounds(ArtSource+"B1_Asset_LongPlatform.PNG"), 128f);
        for (int level = 1; level <= 5; level++)
        {
            string path = $"{LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) prefab = BuildLayout(GetPlan(level), path, fill, cap, platform, material);
            RoomData room = BuildRoomData(level, prefab);
            BuildTestScene(level, room);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LV Layouts] Created LV1-LV5 drafts. No enemy spawns, route changes or Build Settings changes.");
    }

    public static GameObject BuildLayout(Plan plan, string path, Sprite fill, Sprite cap, Sprite platform, Material material)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject($"RoomLayout_LV_{plan.level:00}");
        SceneManager.MoveGameObjectToScene(root, preview);
        try
        {
            GameObject terrain = Child(root.transform, "Terrain");
            terrain.layer = LayerMask.NameToLayer("Ground");
            terrain.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var composite = terrain.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            composite.vertexDistance = .01f;
            composite.offsetDistance = .002f;
            var rects = plan.solids.Select(r => ToWorld(plan,r)).ToArray();
            for (int i = 0; i < rects.Length; i++)
            {
                Rect rect = rects[i];
                GameObject block = Child(terrain.transform, $"Ground_{i+1:00}");
                block.layer = terrain.layer;
                block.transform.localPosition = rect.center;
                var collider = block.AddComponent<BoxCollider2D>();
                collider.size = rect.size;
                collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                SpriteRenderer image = Renderer(block, fill, material, 10);
                image.drawMode = SpriteDrawMode.Tiled;
                image.size = rect.size;
                foreach (Vector2 interval in ExposedTop(rect, rects))
                {
                    GameObject top = Child(block.transform, "WalkingSurface");
                    top.transform.position = new Vector3((interval.x+interval.y)*.5f, rect.yMax-.22f, 0);
                    var surface = Renderer(top, cap, material, 11);
                    surface.drawMode = SpriteDrawMode.Tiled;
                    surface.size = new Vector2(interval.y-interval.x, .44f);
                }
            }
            GameObject steps = Child(root.transform, "OneWayPlatforms");
            PopulatePlatforms(steps.transform,plan);

            GameObject boundaryRoot = Child(root.transform, "Boundaries");
            float bottom = rects.Min(r=>r.yMin);
            float high = rects.Max(r=>r.yMax);
            float playTop = high + 11f;
            float left = ToWorld(plan,new Vector2(108,0)).x;
            float right = ToWorld(plan,new Vector2(1812,0)).x;
            var entranceWall = Wall(boundaryRoot.transform,"StageLeftWall", new Rect(left-WallThickness,bottom,WallThickness,playTop-bottom),true);
            var exitWall = Wall(boundaryRoot.transform,"StageRightWall", new Rect(right,bottom,WallThickness,playTop-bottom),true);
            Wall(boundaryRoot.transform,"Ceiling",new Rect(left-WallThickness,playTop,right-left+2*WallThickness,WallThickness),true);
            var dividers = Child(boundaryRoot.transform,"ChamberGates_OpenForTraversal");
            for (int i = 0; i < plan.gates.Length; i++)
            {
                Vector3 g = plan.gates[i];
                float x = ToWorld(plan,new Vector2(g.x,0)).x;
                float min = g.y < 0 ? bottom : ToWorld(plan,new Vector2(0,g.y)).y;
                float max = g.z < 0 ? playTop : ToWorld(plan,new Vector2(0,g.z)).y;
                Wall(dividers.transform,$"ChamberGate_{i+1:00}",new Rect(x-WallThickness*.5f,min,WallThickness,max-min),false);
            }
            var binder = boundaryRoot.AddComponent<SceneBoundaryController>();
            using (var data = new SerializedObject(binder))
            {
                data.FindProperty("_entranceWall").objectReferenceValue = entranceWall;
                data.FindProperty("_exitWall").objectReferenceValue = exitWall;
                data.FindProperty("_verticalPadding").floatValue = 1f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            GameObject entry = Child(root.transform,"Entrance_Default");
            entry.transform.localPosition = ToWorld(plan,plan.start)+Vector2.up*.04f;
            var stop = Child(entry.transform,"StopPoint"); stop.transform.localPosition = Vector3.right;
            using (var data = new SerializedObject(entry.AddComponent<EntrancePoint>()))
            {
                data.FindProperty("entranceId").stringValue = "Default";
                data.FindProperty("stopPoint").objectReferenceValue = stop.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Child(root.transform,"ExitGuide").transform.localPosition = ToWorld(plan,plan.exit);
            GameObject background = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BackgroundPath), root.transform);
            background.name = "CombatBackground_B1";
            Transform sky = background.transform.Find("GameObject/background");
            if (sky != null) SkyBackgroundRepair.Configure(sky.GetComponent<Project.Parallax.ParallaxBackground>());
            B1VeryFarBackground.FitDraft(root, plan);
            composite.GenerateGeometry();
            ValidateGeometry(root, plan);
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    internal static IEnumerable<Vector2> ExposedTop(Rect rect, Rect[] all)
    {
        var intervals = new List<Vector2> { new Vector2(rect.xMin,rect.xMax) };
        foreach (Rect cover in all)
        {
            if (cover.yMin > rect.yMax+.001f || cover.yMax <= rect.yMax+.001f) continue;
            for (int i = intervals.Count-1; i >= 0; i--)
            {
                Vector2 span = intervals[i];
                if (cover.xMax <= span.x || cover.xMin >= span.y) continue;
                intervals.RemoveAt(i);
                if (cover.xMin > span.x) intervals.Insert(i,new Vector2(span.x,Mathf.Min(span.y,cover.xMin)));
                if (cover.xMax < span.y) intervals.Insert(i,new Vector2(Mathf.Max(span.x,cover.xMax),span.y));
            }
        }
        return intervals.Where(x=>x.y-x.x>.01f);
    }

    public const string PlatformPrefabPath="Assets/Project/Prefab/Environment/platform.prefab";

    internal static void PopulatePlatforms(Transform parent,Plan plan)
    {
        var template=AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPrefabPath);
        var box=template.GetComponent<BoxCollider2D>();
        float height=box.size.y*template.transform.localScale.y;
        for (int i=0;i<plan.platforms.Length;i++)
        {
            var step=(GameObject)PrefabUtility.InstantiatePrefab(template,parent);
            step.name=$"Platform_{i+1:00}";
            Vector3 p=plan.platforms[i];
            float width=Mathf.Max(3.6f,p.z*HorizontalScale);
            step.transform.localScale=new Vector3(width/box.size.x,height/box.size.y,1);
            step.transform.localPosition=ToWorld(plan,p)+Vector2.down*height*.5f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(step.transform);
        }
    }

    private static BoxCollider2D Wall(Transform parent,string name,Rect bounds,bool closed)
    {
        GameObject wall = Child(parent,name); wall.layer = LayerMask.NameToLayer("Ground");
        wall.transform.localPosition = bounds.center;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>(); collider.size = bounds.size; collider.enabled = closed;
        return collider;
    }

    public static void ValidateGeometry(GameObject root, Plan plan)
    {
        var composite = root.GetComponentInChildren<CompositeCollider2D>();
        if (composite == null || composite.pathCount == 0) throw new InvalidOperationException($"LV{plan.level}: Missing ground geometry.");
        if (root.GetComponentsInChildren<EnemyController>(true).Length != 0)
            throw new InvalidOperationException("Enemy instances must be spawned at runtime, not baked into layouts.");
        if (root.GetComponentInChildren<CombatStageController>(true) == null &&
            root.GetComponentInChildren<RoomWaveManager>(true) != null)
            throw new InvalidOperationException("LV wave managers need authored chambers.");
        foreach (BoxCollider2D wall in root.transform.Find("Boundaries").GetComponentsInChildren<BoxCollider2D>(true))
            if (Mathf.Min(wall.size.x,wall.size.y) < WallThickness-.01f && wall.name != "Ceiling")
            {
                // Low tunnel gates are intentionally shorter than the wall width, but still six units thick.
                if (wall.size.x < WallThickness-.01f) throw new InvalidOperationException("Thin chamber wall.");
            }
    }

    public static RoomData BuildRoomData(int level, GameObject prefab)
    {
        string path = $"{RoomFolder}/RoomData_LV_{level:00}.asset";
        RoomData room = AssetDatabase.LoadAssetAtPath<RoomData>(path);
        if (room != null) return room;
        room = ScriptableObject.CreateInstance<RoomData>(); room.name=$"RoomData_LV_{level:00}";
        var reference = AssetDatabase.LoadAssetAtPath<RoomData>("Assets/Project/Resources/MapSO/RoomData_Combat_02.asset");
        using (var data = new SerializedObject(room))
        {
            data.FindProperty("roomId").stringValue=$"LV_{level:00}";
            data.FindProperty("sceneName").stringValue=$"Test_LV_{level:00}";
            data.FindProperty("displayName").stringValue=$"LV {level}";
            data.FindProperty("layoutPrefab").objectReferenceValue=prefab;
            data.FindProperty("includeInRandomOffers").boolValue=false;
            data.FindProperty("backgroundMusic").objectReferenceValue=reference.BackgroundMusic;
            data.FindProperty("ambience").objectReferenceValue=reference.Ambience;
            data.FindProperty("ambienceVolume").floatValue=reference.AmbienceVolume;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.CreateAsset(room,path); return room;
    }

    public static void BuildTestScene(int level, RoomData room)
    {
        string path = $"{SceneFolder}/Test_LV_{level:00}.unity";
        if (File.Exists(path)) return;
        Scene scene = EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Additive);
        try
        {
            var host = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RoomLayoutHost>(true)).Single();
            using (var data = new SerializedObject(host))
            {
                data.FindProperty("bootstrapRoom").objectReferenceValue=room;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var player = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            player.transform.position = ToWorld(GetPlan(level),GetPlan(level).start)+Vector2.up*.04f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            if (!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save "+path);
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
    }

    [MenuItem("Tools/Project/Level Design/Reset LV Test Player Starts")]
    public static void ResetTestPlayerStarts()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (int level = 1; level <= 5; level++)
        {
            string path = $"{SceneFolder}/Test_LV_{level:00}.unity";
            if (!File.Exists(path)) continue;
            Scene scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var player = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacter>(true)).Single();
                var plan = GetPlan(level);
                player.transform.position = ToWorld(plan,plan.start)+Vector2.up*.04f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }

    private static Sprite ImportPiece(string name,string source,Rect rect,float ppu)
    {
        string path=$"{ArtFolder}/{name}.png";
        if (!File.Exists(path)) AssetDatabase.CopyAsset(ArtSource+source,path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=ppu; importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=4096;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var factories=new SpriteDataProviderFactories(); factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var existing=provider.GetSpriteRects().FirstOrDefault(s=>s.name==name);
        GUID id=existing != null ? existing.spriteID : GUID.Generate();
        provider.SetSpriteRects(new[] { new SpriteRect { name=name,rect=rect,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,spriteID=id } });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name,id) });
        provider.Apply();
        EditorUtility.SetDirty(provider.targetObject);
        AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        Sprite result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name==name);
        if (result.rect != rect || Mathf.Abs(result.pixelsPerUnit-ppu)>.001f)
            throw new InvalidOperationException("Tile import did not retain its slice: "+name);
        return result;
    }

    public static void RefreshDraftTileReferences()
    {
        BuildAll();
        foreach (string path in AssetDatabase.FindAssets("t:Prefab",new[] { LayoutFolder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
                {
                    string name=renderer.name.StartsWith("Ground_",StringComparison.Ordinal) ? "LV_GroundFill" :
                        renderer.name=="WalkingSurface" ? "LV_FloorCap" :
                        renderer.name=="Art" && renderer.transform.parent.name.StartsWith("Platform_",StringComparison.Ordinal) ? "LV_PlatformCap" : null;
                    if (name!=null)
                    {
                        renderer.sprite=AssetDatabase.LoadAllAssetsAtPath($"{ArtFolder}/{name}.png").OfType<Sprite>().Single(s=>s.name==name);
                        renderer.drawMode=SpriteDrawMode.Tiled;
                        if (name=="LV_GroundFill") renderer.size=renderer.GetComponent<BoxCollider2D>().size;
                        else if (name=="LV_PlatformCap") renderer.size=new Vector2(renderer.transform.parent.GetComponent<BoxCollider2D>().size.x,.5f);
                        else
                        {
                            var boxes=root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>();
                            Rect[] rects=boxes.Select(b=>new Rect((Vector2)b.transform.position-b.size*.5f,b.size)).ToArray();
                            var owner=renderer.transform.parent.GetComponent<BoxCollider2D>();
                            var rect=new Rect((Vector2)owner.transform.position-owner.size*.5f,owner.size);
                            Vector2 span=ExposedTop(rect,rects).OrderBy(s=>Mathf.Abs((s.x+s.y)*.5f-renderer.transform.position.x)).First();
                            renderer.size=new Vector2(span.y-span.x,.44f);
                        }
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static Rect OpaqueBounds(string path)
    {
        var texture=new Texture2D(2,2);
        try
        {
            ImageConversion.LoadImage(texture,File.ReadAllBytes(path));
            Color32[] pixels=texture.GetPixels32();
            int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
            for (int y=0;y<texture.height;y++) for (int x=0;x<texture.width;x++)
                if (pixels[y*texture.width+x].a>128) { minX=Mathf.Min(minX,x); maxX=Mathf.Max(maxX,x); minY=Mathf.Min(minY,y); maxY=Mathf.Max(maxY,y); }
            return new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
        }
        finally { Object.DestroyImmediate(texture); }
    }

    private static Material TerrainMaterial()
    {
        string path=ArtFolder+"/LV_TerrainUnlit.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material!=null) return material;
        material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        AssetDatabase.CreateAsset(material,path); return material;
    }
    private static SpriteRenderer Renderer(GameObject host,Sprite sprite,Material material,int order)
    {
        var renderer=host.AddComponent<SpriteRenderer>(); renderer.sprite=sprite;
        renderer.sharedMaterial=material; renderer.sortingOrder=order; return renderer;
    }
    private static GameObject Child(Transform parent,string name)
    {
        var result=new GameObject(name); result.transform.SetParent(parent,false); return result;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
}
