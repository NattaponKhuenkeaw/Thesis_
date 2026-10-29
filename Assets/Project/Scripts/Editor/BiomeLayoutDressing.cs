using System;
using System.Linq;
using Project.Parallax;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BiomeLayoutDressing
{
    public const string GroupName="AuthoredSetDressing";
    private static readonly string[] Districts={ "ResidentialAlley","ServiceStreet","StairDistrict","TransitUnderpass","RooflineCrossroads",
        "MarketEntrance","ShutterStreet","FreightCourtyard","LanternViaduct","SplitLevelMarket" };

    public static void Apply(GameObject root,int level)
    {
        var old=root.transform.Find(GroupName);
        if (old!=null) Object.DestroyImmediate(old.gameObject);
        var group=new GameObject(GroupName); group.transform.SetParent(root.transform,false);
        var district=new GameObject(Districts[level-1]); district.transform.SetParent(group.transform,false);
        if (level<=5) DressCity(root,district.transform,level);
        else DressMarket(root,district.transform,level);
        LayoutRoadAuthoring.ApplyToLayout(root,level);
        if (level == 1) LV1BuildingAuthoring.Apply(root);
        else
        {
            PlayableBuildingDressing.PrepareArt();
            PlayableBuildingDressing.Apply(root,level);
        }
        BiomeMaterialAuthoring.ApplyIfPrepared(root);
    }

    private static void DressCity(GameObject root,Transform group,int level)
    {
        var background=root.transform.Find("CombatBackground_B1");
        foreach (var layer in background.GetComponentsInChildren<ParallaxBackground>(true))
        {
            if (!layer.name.StartsWith("place2_0",StringComparison.Ordinal)) continue;
            layer.enabled=false;
            layer.GetComponent<SpriteRenderer>().enabled=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(layer.GetComponent<SpriteRenderer>());
        }
        var modules=AssetDatabase.LoadAllAssetsAtPath(BiomeLayoutAuthoring.CityModules).OfType<Sprite>().ToDictionary(s=>s.name);
        DressPlatforms(root,AssetDatabase.LoadAllAssetsAtPath("Assets/Project/Art/BG/B1/LevelDesignTiles/LV_PlatformCap.png").OfType<Sprite>().First());
        string[][] sequences={
            new[] {"Apartment","CornerShop","Apartment","Stairhouse"},
            new[] {"Garage","Garage","CornerShop","Apartment"},
            new[] {"Stairhouse","Apartment","Stairhouse","CornerShop"},
            new[] {"Garage","Stairhouse","CornerShop"},
            new[] {"CornerShop","Apartment","Stairhouse","Garage","Apartment"}
        };
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        float left=LevelDesignLayoutBuilder.WorldX(plan,108),right=LevelDesignLayoutBuilder.WorldX(plan,1812);
        float x=left+3f;
        int n=0;
        while (x<right-3f)
        {
            string name=sequences[level-1][n%sequences[level-1].Length];
            Sprite sprite=modules[name];
            float scale=name=="Stairhouse" ? 1.16f : 1.3f;
            float width=sprite.bounds.size.x*scale;
            x+=width*.5f;
            Add(group,$"{name}_{n:00}",sprite,new Vector2(x,0),scale,2,n%5==3 && name=="Apartment");
            x+=width*.5f+(level==4 ? 5f : 1.7f+(n%3)*1.15f);
            n++;
        }
        // Dress the solid facades themselves; windows are artwork, never new collision.
        foreach (Transform block in root.transform.Find("Terrain"))
        {
            if (block.name=="Ground_01") continue;
            var collider=block.GetComponent<BoxCollider2D>();
            Vector2 center=block.TransformPoint(collider.offset);
            Vector2 size=Vector2.Scale(collider.size,block.lossyScale);
            var bounds=new Bounds(center,new Vector3(size.x,size.y,0f));
            for (float y=bounds.min.y+1.6f;y<bounds.max.y-1.25f;y+=3.1f)
                for (float wx=bounds.min.x+2.4f;wx<bounds.max.x-1.8f;wx+=level==2 ? 6.5f : 4.8f)
                    Add(group,$"FacadeWindow_{block.name}_{wx:0.0}_{y:0.0}",modules["Window"],new Vector2(wx,y-1f),1.05f,13,false);
            if (bounds.size.y>5f && bounds.size.x>7f && (block.GetSiblingIndex()+level)%2==0)
                Add(group,"CornerNeon_"+block.name,modules["NeonSign"],new Vector2(bounds.min.x+1.3f,bounds.max.y-4.6f),.8f,13,false);
        }
        var solids=plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
        for (int i=1;i<solids.Length;i++)
            foreach (Vector2 span in LevelDesignLayoutBuilder.ExposedTop(solids[i],solids))
            {
                float width=span.y-span.x;
                if (width<9f) continue;
                int count=Mathf.Max(1,Mathf.FloorToInt(width/15f));
                for (int k=0;k<count;k++)
                {
                    string module=sequences[level-1][(i+k+level)%sequences[level-1].Length];
                    Add(group,$"RoofDistrict_{i}_{k}",modules[module],
                        new Vector2(Mathf.Lerp(span.x+4f,span.y-4f,(k+.5f)/count),solids[i].yMax),.82f,2,false);
                }
            }
    }

    private static void DressMarket(GameObject root,Transform group,int level)
    {
        int pattern=level-6;
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        var wall=BiomeLayoutAuthoring.Load("IMG_4789","B2_WallFill");
        var cap=BiomeLayoutAuthoring.Load("IMG_4787","B2_Walkway");
        foreach (Transform block in root.transform.Find("Terrain"))
        {
            var image=block.GetComponent<SpriteRenderer>();
            image.sprite=wall; image.drawMode=SpriteDrawMode.Tiled;
            image.size=block.GetComponent<BoxCollider2D>().size;
            if (block.name=="Ground_01") image.enabled=false;
            foreach (Transform child in block)
                if (child.name=="WalkingSurface")
                {
                    var top=child.GetComponent<SpriteRenderer>();
                    Vector2 size=top.size;
                    top.sprite=cap; top.drawMode=SpriteDrawMode.Tiled; top.size=size; top.sortingOrder=14;
                }
        }
        DressPlatforms(root,cap);

        float left=LevelDesignLayoutBuilder.WorldX(plan,108),right=LevelDesignLayoutBuilder.WorldX(plan,1812);
        string[][] rows={
            new[] {"IMG_4779","IMG_4783","IMG_4780","IMG_4781","IMG_4779"},
            new[] {"IMG_4780","IMG_4781","IMG_4780","IMG_4779"},
            new[] {"IMG_4781","IMG_4780","IMG_4783","IMG_4780"},
            new[] {"IMG_4783","IMG_4779","IMG_4781"},
            new[] {"IMG_4781","IMG_4783","IMG_4780","IMG_4779","IMG_4783"}
        };
        float cursor=left+4f;
        int n=0;
        while (cursor<right-6f)
        {
            var sprite=BiomeLayoutAuthoring.Load(rows[pattern][n%rows[pattern].Length]);
            float scale=pattern==2 ? 1.12f : 1.25f;
            float width=sprite.bounds.size.x*scale;
            cursor+=width*.5f;
            Add(group,$"MarketUnit_{n:00}_{sprite.name}",sprite,new Vector2(cursor,0),scale,2,false);
            if (n%3==pattern%3)
                Add(group,$"Cargo_{n:00}",BiomeLayoutAuthoring.Load(pattern%2==0 ? "IMG_4782" : "IMG_4784"),
                    new Vector2(cursor+width*.5f+1.1f,0),.7f,3,n%2==1);
            cursor+=width*.5f+(pattern==2 ? 5f : 2.2f+(n%3)*1.3f);
            n++;
        }
        // Each district has a different bridge height, bay spacing and lantern rhythm.
        float beamHeight=new[] {13f,17f,14f,23f,19f}[pattern];
        float bay=new[] {24f,32f,20f,27f,35f}[pattern];
        var beam=BiomeLayoutAuthoring.Load("IMG_4789","B2_Beam");
        Tiled(group,"RearViaduct",beam,new Vector2((left+right)*.5f,beamHeight),new Vector2(right-left+12f,2.16f),-3);
        for (float px=left+6f+pattern;px<right;px+=bay)
        {
            Tiled(group,"ViaductColumn_"+px.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture),
                BiomeLayoutAuthoring.Load("IMG_4790"),new Vector2(px,beamHeight*.5f),new Vector2(2.48f,beamHeight),-3);
            string lantern=pattern==3 ? "IMG_4776" : (n++%2==0 ? "IMG_4777" : "IMG_4778");
            Add(group,"LanternBay_"+n,BiomeLayoutAuthoring.Load(lantern),new Vector2(px+bay*.45f,beamHeight-4f),1.05f,-2,false);
        }
        // Sparse rooftop props make the upper route readable without hiding characters or the edge.
        foreach (Rect solid in plan.solids.Skip(1).Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)))
            if (solid.width>7f)
            {
                Add(group,"RoofCargo_"+n++,BiomeLayoutAuthoring.Load(pattern%2==0 ? "IMG_4784" : "IMG_4782"),
                    new Vector2(solid.xMin+2f,solid.yMax),.65f,13,false);
                if (solid.width>15f && pattern!=2)
                    Add(group,"RoofLantern_"+n++,BiomeLayoutAuthoring.Load("IMG_4776"),
                        new Vector2(solid.center.x,solid.yMax+4f),.95f,2,false);
            }
        var solids=plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)).ToArray();
        for (int i=1;i<solids.Length;i++)
            foreach (Vector2 span in LevelDesignLayoutBuilder.ExposedTop(solids[i],solids))
                if (span.y-span.x>10f)
                {
                    string shop=rows[pattern][(i+pattern)%rows[pattern].Length];
                    Add(group,$"RoofShop_{i}",BiomeLayoutAuthoring.Load(shop),
                        new Vector2((span.x+span.y)*.5f,solids[i].yMax),pattern==2 ? .9f : 1.12f,2,false);
                }
        var reflection=root.transform.Find("StreetReflection");
        if (reflection==null) { reflection=new GameObject("StreetReflection").transform; reflection.SetParent(root.transform,false); }
        for (int i=reflection.childCount-1;i>=0;i--) Object.DestroyImmediate(reflection.GetChild(i).gameObject);
        var water=BiomeLayoutAuthoring.Load("B2_Parallax_Ground");
        Tiled(reflection,"MarketStreetReflection",water,new Vector2((left+right)*.5f,-2.24f),
            new Vector2((right-left)/1.4f,water.bounds.size.y),17).transform.localScale=Vector3.one*1.4f;
    }

    private static void DressPlatforms(GameObject root,Sprite cap)
    {
        foreach (var image in root.transform.Find("OneWayPlatforms").GetComponentsInChildren<SpriteRenderer>().Where(r=>r.GetComponent<PlatformEffector2D>()!=null))
        {
            var box=image.GetComponent<BoxCollider2D>();
            if (box==null) box=image.GetComponentInParent<BoxCollider2D>();
            Vector3 scale=image.transform.localScale;
            image.sprite=cap; image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
            image.drawMode=SpriteDrawMode.Tiled; image.size=box.size;
            // Switching Simple to Tiled rescales a SpriteRenderer's transform in the editor.
            image.transform.localScale=scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image.transform);
        }
    }

    private static SpriteRenderer Add(Transform parent,string name,Sprite sprite,Vector2 feet,float scale,int order,bool flip)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        go.transform.position=new Vector3(feet.x,feet.y+sprite.bounds.size.y*scale*.5f,0f);
        go.transform.localScale=Vector3.one*scale;
        var image=go.AddComponent<SpriteRenderer>();
        image.sprite=sprite; image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeLayoutAuthoring.MaterialPath);
        image.sortingOrder=order; image.flipX=flip;
        return image;
    }

    private static SpriteRenderer Tiled(Transform parent,string name,Sprite sprite,Vector2 center,Vector2 size,int order)
    {
        var image=Add(parent,name,sprite,Vector2.zero,1f,order,false);
        image.transform.position=new Vector3(center.x,center.y,0f);
        image.drawMode=SpriteDrawMode.Tiled; image.tileMode=SpriteTileMode.Continuous; image.size=size;
        image.transform.localScale=Vector3.one;
        return image;
    }
}
