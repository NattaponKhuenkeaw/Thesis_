using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.Art;
using UnityEditor;
using UnityEngine;

public static class LV1TraversalAudit
{
    public const string Folder = "Logs/LV1TraversalAudit";

    public static void InspectAndCapture()
    {
        Directory.CreateDirectory(Folder);
        string path=BiomeLayoutAuthoring.LayoutPath(1);
        byte[] original=File.ReadAllBytes(path);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var report=new List<string>();
            report.Add("Current saved LV1; read-only inspection, no reauthoring.");
            foreach(var box in root.GetComponentsInChildren<BoxCollider2D>(true))
            {
                var rect=LayoutRoadAuthoring.WorldRect(box);
                report.Add($"COLLIDER {AnimationUtility.CalculateTransformPath(box.transform,root.transform)} | {rect.ToString("F4")} | enabled={box.enabled} trigger={box.isTrigger} layer={LayerMask.LayerToName(box.gameObject.layer)} composite={box.compositeOperation}");
            }
            foreach(var image in root.transform.Find("Terrain").GetComponentsInChildren<SpriteRenderer>(true))
                report.Add($"ART {AnimationUtility.CalculateTransformPath(image.transform,root.transform)} | enabled={image.enabled} active={image.gameObject.activeInHierarchy} | sprite={AssetDatabase.GetAssetPath(image.sprite)}:{image.sprite?.name} | bounds={image.bounds.ToString("F4")} scale={image.transform.lossyScale.ToString("F4")}");
            foreach(var frame in root.GetComponentsInChildren<SpriteCropFrame>(true))
                report.Add($"CROP {AnimationUtility.CalculateTransformPath(frame.transform,root.transform)} | size={frame.CropSize} offset={frame.ImageOffset}");
            var solids=root.transform.Find("Terrain").Cast<Transform>().Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
            foreach(var rect in solids)
                foreach(var span in LevelDesignLayoutBuilder.ExposedTop(rect,solids))
                    report.Add($"WALKABLE x={span.x:F4}..{span.y:F4} feetY={rect.yMax:F4}");
            report.Add("Entrance="+root.transform.Find("Entrance_Default").position.ToString("F4"));
            report.Add("Exit="+root.transform.Find("StageExitTrigger").position.ToString("F4"));
            foreach(float x in new[]{6.425f,53.975f,110f,130f,170f,191f})
            {
                float artTop=VisibleCropTop(root,x);
                var ground=solids.Where(r=>x>=r.xMin && x<=r.xMax).ToArray();
                float floor=ground.Length>0 ? ground.Max(r=>r.yMax) : float.NaN;
                report.Add($"VISUAL_SUPPORT x={x:F3} visibleTop={artTop:F4} physicalTop={floor:F4} difference={artTop-floor:F4}");
            }
            File.WriteAllLines(Folder+"/inspection.txt",report);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        LV1BuildingAuthoring.Capture();
        foreach(string name in new[]{"RaisedBuildings","TallBuilding","RooftopStreet","Overview"})
            File.Copy("Logs/LV1Buildings/"+name+".png",Folder+"/"+name+".png",true);
        if(!original.SequenceEqual(File.ReadAllBytes(path))) throw new InvalidOperationException("Read-only audit changed the LV1 prefab.");
        Debug.Log("[LV1 Audit] Current saved layout inspected and photographed without modifying the prefab.");
    }

    public static float VisibleCropTop(GameObject root,float x)
    {
        if(EditorUtility.IsPersistent(root))
        {
            var loaded=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(root));
            try {return VisibleCropTop(loaded,x);}
            finally {PrefabUtility.UnloadPrefabContents(loaded);}
        }
        float top=float.NegativeInfinity;
        foreach(var frame in root.GetComponentsInChildren<SpriteCropFrame>(true))
        {
            frame.Refresh();
            var image=frame.Artwork; var sprite=frame.SourceSprite;
            if(image==null || sprite==null || !image.enabled || !image.gameObject.activeInHierarchy) continue;
            if(x<image.bounds.min.x || x>image.bounds.max.x) continue;
            var mask=frame.ClippingMask;
            var low=mask.transform.TransformPoint(mask.sprite.bounds.min);
            var high=mask.transform.TransformPoint(mask.sprite.bounds.max);
            float min=Mathf.Max(image.bounds.min.y,Mathf.Min(low.y,high.y));
            float max=Mathf.Min(image.bounds.max.y,Mathf.Max(low.y,high.y));
            var texture=new Texture2D(2,2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var pixels=texture.GetPixels32();
                for(float y=max-.002f;y>=min;y-=.005f)
                {
                    Vector3 world=new Vector3(x,y,image.transform.position.z);
                    Vector3 clipped=mask.transform.InverseTransformPoint(world);
                    if(!mask.sprite.bounds.Contains(new Vector3(clipped.x,clipped.y,0))) continue;
                    Vector3 point=image.transform.InverseTransformPoint(world);
                    if(image.flipX) point.x=-point.x; if(image.flipY) point.y=-point.y;
                    int px=Mathf.FloorToInt(point.x*sprite.pixelsPerUnit+sprite.pivot.x+sprite.rect.x);
                    int py=Mathf.FloorToInt(point.y*sprite.pixelsPerUnit+sprite.pivot.y+sprite.rect.y);
                    if(px<sprite.rect.xMin || px>=sprite.rect.xMax || py<sprite.rect.yMin || py>=sprite.rect.yMax) continue;
                    if(px>=0 && px<texture.width && py>=0 && py<texture.height && pixels[py*texture.width+px].a>128)
                    {top=Mathf.Max(top,y); break;}
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(texture);}
        }
        return top;
    }
}
