using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ForegroundGroundDressing
{
    public const string First = LV1BuildingAuthoring.KitFolder + "/LV_ForegroundGround1.png";
    public const string Second = LV1BuildingAuthoring.KitFolder + "/LV_ForegroundGround2.png";
    public const float PixelsPerUnit = 100f;
    public static bool IsPrepared => AssetDatabase.LoadAllAssetsAtPath(First).OfType<Sprite>().Any(s=>s.name=="WholeA");

    [MenuItem("Tools/Project/Level Design/Use Foreground Artwork For LV1 To LV5 Ground")]
    public static void ApplyAndCapture()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        PrepareArt();
        var untouched=Enumerable.Range(6,5).ToDictionary(BiomeLayoutAuthoring.LayoutPath,p=>File.ReadAllBytes(BiomeLayoutAuthoring.LayoutPath(p)));
        var audit=new List<string>();
        for(int level=1;level<=5;level++)
        {
            string path=BiomeLayoutAuthoring.LayoutPath(level);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before=PlayableBuildingDressing.GameplaySnapshot(root);
                Apply(root,level);
                Validate(root,level);
                if(before!=PlayableBuildingDressing.GameplaySnapshot(root)) throw new InvalidOperationException("Foreground art changed gameplay: "+path);
                LevelDesignLayoutBuilder.ValidateGeometry(root,LevelDesignLayoutBuilder.GetPlan(level));
                PrefabUtility.SaveAsPrefabAsset(root,path);
                audit.Add($"LV{level:00}: foreground slices fitted to original solids; collision, platforms, encounters, doors and gameplay bindings unchanged.");
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var entry in untouched)
            if(!entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key))) throw new InvalidOperationException("Biome-two layout changed: "+entry.Key);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/ForegroundGround");
        File.WriteAllLines("Logs/ForegroundGround/gameplay-audit.txt",audit);
        BiomeLayoutAuthoring.CaptureAll(); LV1BuildingAuthoring.Capture();
        Debug.Log("[Foreground Ground] Baked original B1 foreground pixels into LV1-5 ground and platforms. Biome two, roads, reflections and gameplay preserved.");
    }

    public static void Apply(GameObject root,int level)
    {
        if(level<1 || level>5) throw new ArgumentOutOfRangeException(nameof(level));
        if(level==1 && root.GetComponentsInChildren<Project.Art.SpriteCropFrame>(true).Length>0) return;
        var sprites=new[]{First,Second}.SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>()).ToDictionary(s=>s.name);
        var material=AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        var blocks=root.transform.Find("Terrain").Cast<Transform>().ToArray();
        var rects=blocks.Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
        for(int index=0;index<blocks.Length;index++)
        {
            var block=blocks[index]; var rect=rects[index];
            var old=block.Find("BuildingFacade"); if(old!=null) Object.DestroyImmediate(old.gameObject);
            if(index==0) continue;
            var facade=new GameObject("BuildingFacade").transform; facade.SetParent(block,false);
            var sorting=facade.gameObject.AddComponent<SortingGroup>(); sorting.sortingOrder=13;
            block.GetComponent<SpriteRenderer>().enabled=false;
            foreach(Transform child in block)
                if(child.name=="WalkingSurface") child.GetComponent<SpriteRenderer>().enabled=false;
            bool useA=(index+level)%2==0;
            var wall=sprites[useA ? "WallA" : "WallB"];
            // Leave the silhouette margins empty, otherwise a rectangular backing hides the painted corners.
            Tile(facade,"BuildingBody",wall,new Rect(rect.xMin+.4f,rect.yMin,Mathf.Max(.01f,rect.width-.8f),rect.height-.16f),material,12);
            BuildingCornerAuthoring.AddCoveredJoints(facade,rect,rects,wall,.16f,1);
            var maskHost=new GameObject("FacadeClip"); maskHost.transform.SetParent(facade,false);
            maskHost.transform.position=rect.center;
            maskHost.transform.localScale=new Vector3(rect.width/wall.bounds.size.x,rect.height/wall.bounds.size.y,1);
            var mask=maskHost.AddComponent<SpriteMask>(); mask.sprite=wall; mask.isCustomRangeActive=true;
            mask.frontSortingOrder=15; mask.backSortingOrder=12;
            int edge=0;
            foreach(var span in LevelDesignLayoutBuilder.ExposedTop(rect,rects))
            {
                Sprite cap=sprites[useA ? "CapA" : "CapB"];
                float height=Mathf.Min(.16f,rect.height);
                var roof=new GameObject(edge==0 ? "Roofline" : "Roofline_"+edge).transform; roof.SetParent(facade,false);
                float corner=Mathf.Min(.64f,(span.y-span.x)*.25f);
                float depth=Mathf.Min(.76f,rect.height);
                Tile(roof,"LeftCorner",sprites["CornerA"],new Rect(span.x,rect.yMax-depth,corner,depth),material,15);
                Tile(roof,"Middle",cap,new Rect(span.x+corner,rect.yMax-height,span.y-span.x-corner*2,height),material,15);
                Tile(roof,"RightCorner",sprites["CornerB"],new Rect(span.y-corner,rect.yMax-depth,corner,depth),material,15);
                edge++;
            }
            float side=Mathf.Min(.4f,rect.width*.25f);
            Tile(facade,"LeftWallEdge",sprites["EdgeA"],new Rect(rect.xMin,rect.yMin,side,Mathf.Max(.01f,rect.height-.76f)),material,15);
            Tile(facade,"RightWallEdge",sprites["EdgeB"],new Rect(rect.xMax-side,rect.yMin,side,Mathf.Max(.01f,rect.height-.76f)),material,15);
            Tile(facade,"FoundationSkirt",sprites[useA ? "FootA" : "FootB"],new Rect(rect.xMin+side,rect.yMin,rect.width-side*2,Mathf.Min(.16f,rect.height)),material,15);
        }
        foreach(var image in root.transform.Find("OneWayPlatforms").GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.GetComponent<PlatformEffector2D>()!=null))
        {
            var scale=image.transform.localScale; var size=image.size;
            image.sprite=sprites[level%2==0 ? "CapA" : "CapB"];
            image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous; image.size=size;
            image.transform.localScale=scale; image.color=Color.white;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image.transform);
        }
        BiomeMaterialAuthoring.ApplyIfPrepared(root);
    }

    public static void Validate(GameObject root,int level)
    {
        if(level==1 && root.GetComponentsInChildren<Project.Art.SpriteCropFrame>(true).Length>0)
        {
            foreach(float x in new[]{6.425f,53.975f,110f,170f})
            {
                float visible=LV1TraversalAudit.VisibleCropTop(root,x);
                float floor=root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>().Select(LayoutRoadAuthoring.WorldRect)
                    .Where(r=>x>=r.xMin && x<=r.xMax).Max(r=>r.yMax);
                if(float.IsNegativeInfinity(visible) || Mathf.Abs(visible-floor)>.12f)
                    throw new InvalidOperationException("Saved LV1 crop roof has no matching physical support at X="+x);
            }
            return;
        }
        foreach(Transform block in root.transform.Find("Terrain"))
        {
            if(block.name=="Ground_01") continue;
            Rect rect=LayoutRoadAuthoring.WorldRect(block.GetComponent<BoxCollider2D>());
            var art=block.Find("BuildingFacade");
            if(art==null || art.GetComponentsInChildren<Collider2D>(true).Length!=0 || art.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)
                throw new InvalidOperationException("Foreground scenery must remain visual-only.");
            foreach(var image in art.GetComponentsInChildren<SpriteRenderer>(true))
                if(!IsForeground(image.sprite) || image.transform.localScale!=Vector3.one || image.color!=Color.white)
                    throw new InvalidOperationException("Foreground pixels were replaced, stretched or tinted: "+image.name);
            var all=root.transform.Find("Terrain").Cast<Transform>().Select(t=>LayoutRoadAuthoring.WorldRect(t.GetComponent<BoxCollider2D>())).ToArray();
            var spans=LevelDesignLayoutBuilder.ExposedTop(rect,all).ToArray();
            var edges=art.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Middle").ToArray();
            if(edges.Length!=spans.Length) throw new InvalidOperationException("Missing walkable foreground edge.");
            for(int i=0;i<edges.Length;i++)
                if(Mathf.Abs(edges[i].bounds.max.y-rect.yMax)>.001f || edges[i].bounds.min.x<spans[i].x-.001f || edges[i].bounds.max.x>spans[i].y+.001f)
                    throw new InvalidOperationException("Foreground edge does not match the collision surface.");
        }
    }

    public static bool IsForeground(Sprite sprite) => sprite!=null && new[]{First,Second}.Contains(AssetDatabase.GetAssetPath(sprite));

    private static SpriteRenderer Image(Transform parent,string name,Sprite sprite,Material material,int order)
    {
        var host=new GameObject(name); host.transform.SetParent(parent,false);
        var image=host.AddComponent<SpriteRenderer>(); image.sprite=sprite; image.sharedMaterial=material; image.sortingOrder=order; return image;
    }
    private static SpriteRenderer Tile(Transform parent,string name,Sprite sprite,Rect rect,Material material,int order)
    {
        var image=Image(parent,name,sprite,material,order); image.transform.position=rect.center;
        image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous; image.size=rect.size; image.transform.localScale=Vector3.one; return image;
    }

    public static void PrepareArt()
    {
        BiomeMaterialAuthoring.PrepareMaterials();
        Import("Assets/Project/Art/BG/B1/B1_Parallax_Foreground1.PNG",First,true);
        Import("Assets/Project/Art/BG/B1/B1_Parallax_Foreground2.PNG",Second,false);
    }
    private static void Import(string source,string path,bool first)
    {
        if(File.Exists(path)) return;
        if(!AssetDatabase.CopyAsset(source,path)) throw new IOException("Cannot copy supplied foreground artwork.");
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.spriteImportMode=SpriteImportMode.Multiple; importer.spritePixelsPerUnit=PixelsPerUnit;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=4096; importer.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape=false; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path); float width=texture.width,height=texture.height;
        string suffix=first ? "A" : "B"; float x=first ? 64 : 736;
        var definitions=new[]{("Whole"+suffix,new Rect(0,0,width,height)),("Wall"+suffix,new Rect(x,328,320,320)),
            ("Cap"+suffix,new Rect(x,height-16,320,16)),("Corner"+suffix,new Rect(first ? 0 : width-64,height-76,64,76)),
            ("Edge"+suffix,new Rect(first ? 0 : width-40,64,40,512)),("Foot"+suffix,new Rect(x,0,320,16))};
        var factories=new SpriteDataProviderFactories(); factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var slices=definitions.Select(d=>new SpriteRect{name=d.Item1,rect=d.Item2,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,spriteID=GUID.Generate()}).ToArray();
        provider.SetSpriteRects(slices);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply(); AssetDatabase.WriteImportSettingsIfDirty(path); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    }
}
