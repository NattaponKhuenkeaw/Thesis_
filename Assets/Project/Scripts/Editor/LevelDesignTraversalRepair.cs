using System;
using System.Linq;
using Project.Parallax;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class LevelDesignTraversalRepair
{
    public static void ApplyAndCapture()
    {
        ApplyAll();
        LevelDesignLayoutPreview.CaptureAll();
    }

    [MenuItem("Tools/Project/Level Design/Expand LV Combat Space And Restore Standard Platforms")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (int level=1;level<=5;level++)
        {
            string path=$"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var plan=LevelDesignLayoutBuilder.GetPlan(level);
                ResizeTerrain(root,plan);
                ResizeBoundaries(root,plan);
                var steps=root.transform.Find("OneWayPlatforms");
                for (int n=steps.childCount-1;n>=0;n--) Object.DestroyImmediate(steps.GetChild(n).gameObject);
                LevelDesignLayoutBuilder.PopulatePlatforms(steps,plan);
                root.transform.Find("Entrance_Default").localPosition=LevelDesignLayoutBuilder.ToWorld(plan,plan.start)+Vector2.up*.04f;
                root.transform.Find("ExitGuide").localPosition=LevelDesignLayoutBuilder.ToWorld(plan,plan.exit);
                ExtendBackground(root,plan);
                root.GetComponentInChildren<CompositeCollider2D>().GenerateGeometry();
                LevelDesignLayoutBuilder.ValidateGeometry(root,plan);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        LevelDesignBuildingDress.ApplyAll();
        LevelDesignBuildingDress.ApplyReflections();
        LevelDesignSpawnBuilder.BuildAll();
        AssetDatabase.SaveAssets();
        Debug.Log("[LV Traversal] Expanded flat combat space; kept jump gaps and heights; restored standard platform prefab in LV1-LV5.");
    }

    private static void ResizeTerrain(GameObject root,LevelDesignLayoutBuilder.Plan plan)
    {
        Rect[] rects=plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
        var roof=AssetDatabase.LoadAllAssetsAtPath(LevelDesignBuildingDress.Sheet).OfType<Sprite>()
            .Single(s=>s.name=="Layout02_RoofEdge");
        for (int i=0;i<rects.Length;i++)
        {
            Transform block=root.transform.Find($"Terrain/Ground_{i+1:00}");
            var box=block.GetComponent<BoxCollider2D>();
            block.localPosition=rects[i].center;
            box.size=rects[i].size;
            var body=block.GetComponent<SpriteRenderer>(); body.size=box.size;
            for (int n=block.childCount-1;n>=0;n--)
                if (block.GetChild(n).name=="WalkingSurface") Object.DestroyImmediate(block.GetChild(n).gameObject);
            foreach (Vector2 span in LevelDesignLayoutBuilder.ExposedTop(rects[i],rects))
            {
                var surface=new GameObject("WalkingSurface"); surface.transform.SetParent(block,false);
                surface.transform.position=new Vector3((span.x+span.y)*.5f,rects[i].yMax-.22f,0);
                var image=surface.AddComponent<SpriteRenderer>(); image.sprite=roof;
                image.sharedMaterial=body.sharedMaterial; image.sortingOrder=14;
                image.drawMode=SpriteDrawMode.Tiled; image.size=new Vector2(span.y-span.x,.44f);
            }
        }
    }

    private static void ResizeBoundaries(GameObject root,LevelDesignLayoutBuilder.Plan plan)
    {
        Transform bounds=root.transform.Find("Boundaries");
        float left=LevelDesignLayoutBuilder.WorldX(plan,108);
        float right=LevelDesignLayoutBuilder.WorldX(plan,1812);
        var entrance=bounds.Find("StageLeftWall").GetComponent<BoxCollider2D>();
        var exit=bounds.Find("StageRightWall").GetComponent<BoxCollider2D>();
        entrance.transform.localPosition=new Vector3(left-entrance.size.x*.5f,entrance.transform.localPosition.y,0);
        exit.transform.localPosition=new Vector3(right+exit.size.x*.5f,exit.transform.localPosition.y,0);
        var ceiling=bounds.Find("Ceiling").GetComponent<BoxCollider2D>();
        ceiling.transform.localPosition=new Vector3((left+right)*.5f,ceiling.transform.localPosition.y,0);
        ceiling.size=new Vector2(right-left+12f,ceiling.size.y);
        Transform gates=bounds.Find("ChamberGates");
        for (int i=0;i<plan.gates.Length;i++)
        {
            Transform door=gates.Find($"ChamberGate_{i+1:00}");
            door.localPosition=new Vector3(LevelDesignLayoutBuilder.WorldX(plan,plan.gates[i].x),door.localPosition.y,0);
        }
    }

    private static void ExtendBackground(GameObject root,LevelDesignLayoutBuilder.Plan plan)
    {
        float width=LevelDesignLayoutBuilder.WorldX(plan,1812)-LevelDesignLayoutBuilder.WorldX(plan,108);
        // Each distant layer covers the whole stage plus parallax travel; repeat pixels, never stretch the buildings.
        foreach (var layer in root.transform.Find("CombatBackground_B1").GetComponentsInChildren<ParallaxBackground>(true))
        {
            var image=layer.GetComponent<SpriteRenderer>();
            bool duplicate=layer.name.Contains("(1)");
            image.enabled=!duplicate; layer.enabled=!duplicate;
            if (!duplicate)
            {
                image.drawMode=SpriteDrawMode.Tiled;
                image.size=new Vector2((width*2f+80f)/Mathf.Abs(layer.transform.lossyScale.x),image.sprite.bounds.size.y);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
        }
    }
}
