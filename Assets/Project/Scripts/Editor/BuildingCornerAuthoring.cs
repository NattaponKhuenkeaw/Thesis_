using System;
using System.IO;
using System.Linq;
using Project.Parallax;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BuildingCornerAuthoring
{
    public static void RepairSavedReference()
    {
        string path=BiomeLayoutAuthoring.LayoutPath(1);
        var root=PrefabUtility.LoadPrefabContents(path);
        try {RepairReferenceFloor(root); PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally {PrefabUtility.UnloadPrefabContents(root);}
        CaptureVerification();
    }

    public static void CaptureVerification()
    {
        LV1TraversalAudit.InspectAndCapture();
        Directory.CreateDirectory("Logs/BuildingCorners");
        foreach(int level in new[]{3,8})
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var target=new RenderTexture(1600,900,24); target.Create();
            try
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)),scene);
                var rect=root.transform.Find("Terrain").Cast<Transform>().Skip(1).Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>()))
                    .First(r=>r.height>=4f && r.width>5f);
                var host=new GameObject("Corner verification camera"); SceneManager.MoveGameObjectToScene(host,scene);
                var camera=host.AddComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.orthographic=true;
                camera.orthographicSize=6.8f; camera.backgroundColor=Color.black; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.transform.position=new Vector3(rect.xMin+5f,rect.yMax+2f,-100);
                camera.GetUniversalAdditionalCameraData();
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                foreach(var layer in root.GetComponentsInChildren<ParallaxBackground>(true).Where(p=>p.enabled))
                {
                    layer.targetCamera=camera.transform;
                    typeof(ParallaxBackground).GetMethod("Start",flags).Invoke(layer,null);
                    typeof(ParallaxBackground).GetMethod("LateUpdate",flags).Invoke(layer,null);
                }
                var marker=new GameObject("PreviewOnly_Player"); SceneManager.MoveGameObjectToScene(marker,scene);
                marker.transform.position=new Vector3(rect.xMin+2f,rect.yMax,0);
                var image=marker.AddComponent<SpriteRenderer>(); image.sortingLayerName="Player";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Project/Character/Hero/test_0_Main/animation/main_Idle.anim");
                image.sprite=(Sprite)AnimationUtility.GetObjectReferenceCurve(clip,AnimationUtility.GetObjectReferenceCurveBindings(clip).First())[0].value;
                image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                var previous=RenderTexture.active; var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply();
                    File.WriteAllBytes($"Logs/BuildingCorners/B{(level<=5 ? 1 : 2)}_Corner.png",pixels.EncodeToPNG());
                }
                finally {RenderTexture.active=previous; Object.DestroyImmediate(pixels);}
            }
            finally {Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene);}
        }
    }

    [MenuItem("Tools/Project/Level Design/Apply Native Building Corners To All Layouts")]
    public static void ApplyAndCapture()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        ForegroundGroundDressing.PrepareArt();
        PlayableBuildingDressing.PrepareArt();
        for(int level=1;level<=10;level++)
        {
            string path=BiomeLayoutAuthoring.LayoutPath(level);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(level==1) RepairReferenceFloor(root);
                else
                {
                    string before=PlayableBuildingDressing.GameplaySnapshot(root);
                    PlayableBuildingDressing.Apply(root,level);
                    if(before!=PlayableBuildingDressing.GameplaySnapshot(root))
                        throw new InvalidOperationException("Building corners changed gameplay: "+path);
                }
                LevelDesignLayoutBuilder.ValidateGeometry(root,LevelDesignLayoutBuilder.GetPlan(level));
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log($"[Building Corners] LV{level:00}: saved; "+(level==1 ? "manual artwork preserved, roof support repaired." : "gameplay snapshot unchanged."));
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        BiomeLayoutAuthoring.CaptureAll();
        LV1TraversalAudit.InspectAndCapture();
    }

    public static void RepairReferenceFloor(GameObject root)
    {
        // Only collision changes here. Never regenerate or reposition the artist's crop arrangement.
        var images=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sprite!=null).ToArray();
        string[] art=images.Select(EditorJsonUtility.ToJson).ToArray();
        var transforms=root.GetComponentsInChildren<Transform>(true);
        var positions=transforms.Select(t=>t.localPosition).ToArray();
        var plan=LevelDesignLayoutBuilder.GetPlan(1);
        var terrain=root.transform.Find("Terrain");
        for(int index=1;index<plan.solids.Length;index++)
        {
            Rect target=LevelDesignLayoutBuilder.ToWorld(plan,plan.solids[index]);
            string name=$"Ground_{index+1:00}";
            var block=terrain.Find(name);
            if(block==null)
            {
                block=new GameObject(name).transform; block.SetParent(terrain,false);
                block.gameObject.layer=terrain.gameObject.layer;
                block.gameObject.AddComponent<SpriteRenderer>().enabled=false;
                block.gameObject.AddComponent<BoxCollider2D>();
            }
            var collider=block.GetComponent<BoxCollider2D>();
            var renderer=block.GetComponent<SpriteRenderer>();
            if(renderer.sprite==null)
            {
                var template=terrain.Find("Ground_01").GetComponent<SpriteRenderer>();
                renderer.sprite=template.sprite; renderer.sharedMaterial=template.sharedMaterial;
            }
            collider.autoTiling=false;
            collider.offset=block.InverseTransformPoint(target.center);
            collider.size=new Vector2(target.width/Mathf.Abs(block.lossyScale.x),target.height/Mathf.Abs(block.lossyScale.y));
            collider.compositeOperation=Collider2D.CompositeOperation.Merge;
        }
        terrain.GetComponent<CompositeCollider2D>().GenerateGeometry();
        if(!art.SequenceEqual(images.Select(EditorJsonUtility.ToJson)) || !positions.SequenceEqual(transforms.Select(t=>t.localPosition)))
            throw new InvalidOperationException("Collision repair changed existing LV1 artwork or transforms.");
        var floors=terrain.GetComponentsInChildren<BoxCollider2D>().Select(LayoutRoadAuthoring.WorldRect).ToArray();
        foreach(var slot in root.GetComponentInChildren<CombatStageController>().Chambers.SelectMany(c=>c.spawnSlots).Distinct())
        {
            Vector3 point=slot.position;
            var floor=floors.Where(r=>point.x>=r.xMin && point.x<=r.xMax && r.yMax<=point.y+.1f).OrderByDescending(r=>r.yMax).First();
            point.y=floor.yMax+.05f; slot.position=point;
        }
    }

    public static void ApplyMarketCorners(GameObject root)
    {
        var sprites=AssetDatabase.LoadAllAssetsAtPath(PlayableBuildingDressing.MarketDetails).OfType<Sprite>().ToDictionary(s=>s.name);
        if(!sprites.ContainsKey("RoofLeft")) throw new InvalidOperationException("Prepare market corner slices first.");
        var blocks=root.transform.Find("Terrain").Cast<Transform>().ToArray();
        var rects=blocks.Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        for(int i=1;i<blocks.Length;i++)
        {
            var facade=blocks[i].Find("BuildingFacade"); Rect rect=rects[i];
            foreach(var old in facade.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("WalkableEdge_")).ToArray())
                Object.DestroyImmediate(old.gameObject);
            // The upper half-unit belongs to the painted roof, not to a rectangular wall mask.
            foreach(var mask in facade.GetComponentsInChildren<SpriteMask>())
            {
                var clip=new Rect(rect.xMin+.4f,rect.yMin,rect.width-.8f,Mathf.Max(.01f,rect.height-.52f));
                mask.transform.position=clip.center;
                mask.transform.localScale=new Vector3(clip.width/mask.sprite.bounds.size.x,clip.height/mask.sprite.bounds.size.y,1);
            }
            foreach(var image in facade.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="StructuralBeam" || r.name=="StructuralColumn"))
            {
                image.transform.position=new Vector2(rect.center.x,rect.center.y-.26f);
                image.transform.localScale=Vector3.one;
                image.size=new Vector2(rect.width-.8f,Mathf.Max(.01f,rect.height-.52f));
            }
            var wall=BiomeLayoutAuthoring.Load("IMG_4789","B2_WallFill");
            AddCoveredJoints(facade,rect,rects,wall,.52f,2);
            Tile(facade,"LeftWallEdge",wall,new Rect(rect.xMin,rect.yMin,.4f,Mathf.Max(.01f,rect.height-.52f)),15);
            Tile(facade,"RightWallEdge",wall,new Rect(rect.xMax-.4f,rect.yMin,.4f,Mathf.Max(.01f,rect.height-.52f)),15);
            int n=0;
            foreach(var span in LevelDesignLayoutBuilder.ExposedTop(rect,rects))
            {
                var roof=new GameObject(n==0 ? "Roofline" : "Roofline_"+n).transform; roof.SetParent(facade,false); n++;
                float corner=Mathf.Min(.64f,(span.y-span.x)*.25f),depth=Mathf.Min(.52f,rect.height);
                Tile(roof,"LeftCorner",sprites["RoofLeft"],new Rect(span.x,rect.yMax-depth,corner,depth),16);
                Tile(roof,"Middle",sprites["RoofMiddle"],new Rect(span.x+corner,rect.yMax-depth,span.y-span.x-corner*2,depth),16);
                Tile(roof,"RightCorner",sprites["RoofRight"],new Rect(span.y-corner,rect.yMax-depth,corner,depth),16);
            }
        }
    }

    public static void AddCoveredJoints(Transform parent,Rect rect,Rect[] all,Sprite wall,float depth,int biome)
    {
        int n=0;
        foreach(var cover in all.Where(r=>r.yMax>rect.yMax+.001f && r.yMin<=rect.yMax+.001f && r.xMax>rect.xMin && r.xMin<rect.xMax))
        {
            float left=Mathf.Max(rect.xMin+.4f,cover.xMin),right=Mathf.Min(rect.xMax-.4f,cover.xMax);
            if(right<=left) continue;
            var host=new GameObject("CoveredJoint_"+n++); host.transform.SetParent(parent,false);
            host.transform.position=new Vector2((left+right)*.5f,rect.yMax-depth*.5f);
            var image=host.AddComponent<SpriteRenderer>(); image.sprite=wall;
            image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous;
            image.size=new Vector2(right-left,depth); image.transform.localScale=Vector3.one; image.sortingOrder=14;
            image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(biome,"Foreground"));
        }
    }

    private static void Tile(Transform parent,string name,Sprite sprite,Rect rect,int order)
    {
        var host=new GameObject(name); host.transform.SetParent(parent,false); host.transform.position=rect.center;
        var image=host.AddComponent<SpriteRenderer>(); image.sprite=sprite;
        image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous; image.size=rect.size;
        image.transform.localScale=Vector3.one; image.sortingOrder=order;
        image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(2,"Facade"));
    }
}
