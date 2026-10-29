using System;
using System.Linq;
using Project.Parallax;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RevisedFifthLayoutAuthoring
{
    [MenuItem("Tools/Project/Level Design/Apply Revised LV5 In Both Themes")]
    public static void ApplyAndCapture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (int level in new[] { 5, 10 })
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var plan = LevelDesignLayoutBuilder.GetPlan(level);
                ReplaceTerrain(root,plan);
                ReplaceBarriers(root,plan);
                var steps = root.transform.Find("OneWayPlatforms");
                Clear(steps);
                LevelDesignLayoutBuilder.PopulatePlatforms(steps,plan);
                root.transform.Find("Entrance_Default").localPosition = LevelDesignLayoutBuilder.ToWorld(plan,plan.start)+Vector2.up*.04f;
                LevelDesignSpawnBuilder.Configure(root,level,Resources.Load<Project.Core.ZoneState.EncounterSO>("EncounterSO/Encounter_Easy"));
                BiomeLayoutDressing.Apply(root,level);
                if (level == 5)
                {
                    FitReflection(root);
                    B1ForegroundDressing.RefreshLayout(root);
                }
                // Keep the shared background composition and scale; only extend the sky row if needed.
                float width = LevelDesignLayoutBuilder.WorldX(plan,1812)-LevelDesignLayoutBuilder.WorldX(plan,108);
                foreach (var layer in root.GetComponentsInChildren<ParallaxBackground>(true))
                    if (SkyBackgroundRepair.IsSky(layer)) SkyBackgroundRepair.Configure(layer,width*2f+100f);
                root.GetComponentInChildren<CompositeCollider2D>().GenerateGeometry();
                LevelDesignLayoutBuilder.ValidateGeometry(root,plan);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        BiomeLayoutAuthoring.CaptureAll();
        Debug.Log("[Revised LV5] Updated only layouts 05 and 10 from the revised two-theme design. Five forward-only encounters per theme; standard platforms, biome art and thick barriers retained.");
    }

    private static void ReplaceTerrain(GameObject root,LevelDesignLayoutBuilder.Plan plan)
    {
        var terrain = root.transform.Find("Terrain");
        var previous = terrain.GetChild(0).GetComponent<SpriteRenderer>();
        Sprite fill = previous.sprite;
        var material = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        var roof = plan.level == 5 ? AssetDatabase.LoadAllAssetsAtPath(LevelDesignBuildingDress.Sheet).OfType<Sprite>().Single(s=>s.name=="Layout02_RoofEdge")
            : BiomeLayoutAuthoring.Load("IMG_4787","B2_Walkway");
        Clear(terrain);
        var rects = plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
        for (int i=0;i<rects.Length;i++)
        {
            var block = new GameObject($"Ground_{i+1:00}"); block.transform.SetParent(terrain,false);
            block.layer = LayerMask.NameToLayer("Ground"); block.transform.localPosition = rects[i].center;
            var box = block.AddComponent<BoxCollider2D>(); box.size = rects[i].size;
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
            var image = Image(block,fill,material,10); image.drawMode=SpriteDrawMode.Tiled; image.size=rects[i].size;
            image.enabled = i != 0;
            foreach (var span in LevelDesignLayoutBuilder.ExposedTop(rects[i],rects))
            {
                var cap = new GameObject("WalkingSurface"); cap.transform.SetParent(block.transform,false);
                cap.transform.position = new Vector3((span.x+span.y)*.5f,rects[i].yMax-.22f,0);
                var surface = Image(cap,roof,material,14); surface.drawMode=SpriteDrawMode.Tiled;
                surface.size=new Vector2(span.y-span.x,.44f);
            }
        }
        Physics2D.SyncTransforms();
        terrain.GetComponent<CompositeCollider2D>().GenerateGeometry();
    }

    private static void ReplaceBarriers(GameObject root,LevelDesignLayoutBuilder.Plan plan)
    {
        var bounds = root.transform.Find("Boundaries");
        var rects = plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
        float left=rects.Min(r=>r.xMin),right=rects.Max(r=>r.xMax),bottom=rects.Min(r=>r.yMin),top=rects.Max(r=>r.yMax)+11f;
        SetWall(bounds.Find("StageLeftWall").GetComponent<BoxCollider2D>(),new Rect(left-6f,bottom,6f,top-bottom));
        SetWall(bounds.Find("StageRightWall").GetComponent<BoxCollider2D>(),new Rect(right,bottom,6f,top-bottom));
        SetWall(bounds.Find("Ceiling").GetComponent<BoxCollider2D>(),new Rect(left-6f,top,right-left+12f,6f));
        var seal = bounds.Find("UpperExitSeal");
        if (seal != null) Object.DestroyImmediate(seal.gameObject);
        var gates = bounds.Find("ChamberGates"); Clear(gates);
        for (int i=0;i<plan.gates.Length;i++)
        {
            var gate = new GameObject($"ChamberGate_{i+1:00}"); gate.transform.SetParent(gates,false); gate.layer=LayerMask.NameToLayer("Ground");
            SetWall(gate.AddComponent<BoxCollider2D>(),new Rect(LevelDesignLayoutBuilder.WorldX(plan,plan.gates[i].x)-3f,bottom,6f,top-bottom));
        }
    }

    private static void SetWall(BoxCollider2D wall,Rect rect)
    {
        wall.transform.localPosition=rect.center; wall.size=rect.size; wall.enabled=true; wall.isTrigger=false;
    }

    private static void DressCityTerrain(GameObject root)
    {
        var pieces = AssetDatabase.LoadAllAssetsAtPath(LevelDesignBuildingDress.Sheet).OfType<Sprite>().ToDictionary(s=>s.name);
        var material = AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        foreach (Transform block in root.transform.Find("Terrain"))
        {
            if (block.name=="Ground_01") continue;
            var box = block.GetComponent<BoxCollider2D>();
            var group = new GameObject("BuildingFacade"); group.transform.SetParent(block,false);
            var body = new GameObject("BuildingBody"); body.transform.SetParent(group.transform,false);
            var image = Image(body,pieces["Layout02_BuildingFace"],material,12);
            image.drawMode=SpriteDrawMode.Tiled; image.size=box.size;
            float width=Mathf.Min(.5f,box.size.x*.2f);
            foreach (bool right in new[] {false,true})
            {
                var edge = new GameObject(right ? "RightEdge" : "LeftEdge"); edge.transform.SetParent(group.transform,false);
                edge.transform.localPosition=new Vector3((right ? 1 : -1)*(box.size.x-width)*.5f,0,0);
                var border=Image(edge,pieces["Layout02_WallEdge"],material,13); border.flipX=right;
                edge.transform.localScale=new Vector3(width/border.sprite.bounds.size.x,box.size.y/border.sprite.bounds.size.y,1);
            }
        }
    }

    private static void FitReflection(GameObject root)
    {
        var group = root.transform.Find("StreetReflection");
        var old = group.GetComponentInChildren<SpriteRenderer>();
        Sprite sprite=old.sprite; Material material=old.sharedMaterial; float height=old.bounds.size.y,width=old.bounds.size.x; int order=old.sortingOrder;
        Clear(group);
        Rect rect=LayoutRoadAuthoring.WorldRect(root.transform.Find("Terrain/Ground_01").GetComponent<BoxCollider2D>());
        int count=Mathf.Max(1,Mathf.RoundToInt(rect.width/width)); width=rect.width/count;
        for (int i=0;i<count;i++)
        {
            var part=new GameObject($"Reflection_{i+1:00}"); part.transform.SetParent(group,false);
            part.transform.position=new Vector3(rect.xMin+(i+.5f)*width,rect.yMax-height*.5f,0);
            part.transform.localScale=new Vector3(width/sprite.bounds.size.x,height/sprite.bounds.size.y,1);
            Image(part,sprite,material,order);
        }
    }

    private static SpriteRenderer Image(GameObject go,Sprite sprite,Material material,int order)
    {
        var image=go.AddComponent<SpriteRenderer>(); image.sprite=sprite; image.sharedMaterial=material; image.sortingOrder=order;
        return image;
    }

    private static void Clear(Transform parent)
    {
        for (int i=parent.childCount-1;i>=0;i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
