using System;
using System.IO;
using System.Linq;
using Project.Parallax;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class PlatformGlowAuthoring
{
    public const string HaloName="PlatformLandingGlow";
    public static string HaloMaterialPath(int biome)=>BiomeMaterialAuthoring.Folder+$"/B{biome}_PlatformHalo.mat";
    [MenuItem("Tools/Project/Level Design/Highlight Walkable Platforms")]
    public static void ApplyAndCapture()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        BiomeMaterialAuthoring.PrepareMaterials();
        PrepareHalos();
        Capture(1,"Before"); Capture(7,"Before");
        var paths=new[]{LevelDesignLayoutBuilder.PlatformPrefabPath}.Concat(Enumerable.Range(1,10).Select(BiomeLayoutAuthoring.LayoutPath));
        foreach(string path in paths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before=BiomeMaterialAuthoring.GeometrySnapshot(root);
                if(path==LevelDesignLayoutBuilder.PlatformPrefabPath)
                    root.GetComponent<SpriteRenderer>().sharedMaterial=Material(1);
                else Apply(root);
                if(before!=BiomeMaterialAuthoring.GeometrySnapshot(root))
                    throw new InvalidOperationException("Platform highlighting changed geometry or gameplay: "+path);
                string physics=BiomeLayoutAuthoring.PhysicsSnapshot(root);
                if(path==LevelDesignLayoutBuilder.PlatformPrefabPath) EnsureHalo(root.GetComponent<SpriteRenderer>(),1);
                else
                    foreach(var image in root.transform.Find("OneWayPlatforms").GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.GetComponent<PlatformEffector2D>()!=null))
                        EnsureHalo(image,Biome(image));
                if(physics!=BiomeLayoutAuthoring.PhysicsSnapshot(root)) throw new InvalidOperationException("Platform halo changed collision.");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        Capture(1,"After"); Capture(7,"After");
        Debug.Log("[Platform Glow] Separate cyan/jade materials and visual-only halos saved to the shared prefab and LV1-10. Base sprites, gameplay transforms, colliders and effectors unchanged.");
    }

    public static Material Material(int biome) => AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(biome,"Platform"));

    public static void Apply(GameObject root)
    {
        var platforms=root.transform.Find("OneWayPlatforms");
        if(platforms==null) return;
        foreach(var image in platforms.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.GetComponent<PlatformEffector2D>()!=null))
        {
            int biome=Biome(image);
            image.sharedMaterial=Material(biome);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
    }

    private static int Biome(SpriteRenderer image)=>AssetDatabase.GetAssetPath(image.sprite).StartsWith(BiomeLayoutAuthoring.B2Art,StringComparison.Ordinal) ? 2 : 1;

    private static void PrepareHalos()
    {
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Project/Material/PlatformLandingGlow.shader");
        if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Platform halo shader failed to compile.");
        for(int biome=1;biome<=2;biome++)
        {
            if(AssetDatabase.LoadAssetAtPath<Material>(HaloMaterialPath(biome))!=null) continue;
            var material=new Material(shader){name=$"B{biome}_PlatformHalo"};
            material.SetColor("_Color",biome==1 ? new Color(.1f,.8f,1f,.55f) : new Color(.1f,1f,.65f,.55f));
            AssetDatabase.CreateAsset(material,HaloMaterialPath(biome));
        }
    }

    private static void EnsureHalo(SpriteRenderer platform,int biome)
    {
        var host=platform.transform.Find(HaloName);
        if(host==null) {host=new GameObject(HaloName).transform; host.SetParent(platform.transform,false);}
        var image=host.GetComponent<SpriteRenderer>();
        if(image==null) image=host.gameObject.AddComponent<SpriteRenderer>();
        image.sprite=SpriteCropAuthoring.LoadMask(); image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(HaloMaterialPath(biome));
        var box=platform.GetComponent<BoxCollider2D>(); var scale=platform.transform.lossyScale;
        host.localPosition=box.offset+Vector2.up*box.size.y*.5f;
        host.localScale=new Vector3((box.size.x*Mathf.Abs(scale.x)+.35f)/Mathf.Abs(scale.x),.8f/Mathf.Abs(scale.y),1);
        image.sortingLayerID=platform.sortingLayerID; image.sortingOrder=platform.sortingOrder-1;
        PrefabUtility.RecordPrefabInstancePropertyModifications(image); PrefabUtility.RecordPrefabInstancePropertyModifications(host);
    }

    private static void Capture(int level,string label)
    {
        Directory.CreateDirectory("Logs/PlatformGlow");
        var scene=EditorSceneManager.NewPreviewScene();
        var target=new RenderTexture(1280,720,24); target.Create();
        try
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level)),scene);
            var platforms=root.transform.Find("OneWayPlatforms").GetComponentsInChildren<BoxCollider2D>();
            var first=platforms.OrderBy(b=>b.transform.position.x).ThenBy(b=>LayoutRoadAuthoring.WorldRect(b).yMax).First();
            var rect=LayoutRoadAuthoring.WorldRect(first);
            var host=new GameObject("Platform visibility camera"); SceneManager.MoveGameObjectToScene(host,scene);
            var camera=host.AddComponent<Camera>(); camera.scene=scene; camera.enabled=false; camera.orthographic=true;
            camera.orthographicSize=6.8f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.transform.position=new Vector3(rect.center.x+1f,rect.yMax+2f,-100); camera.GetUniversalAdditionalCameraData();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(var layer in root.GetComponentsInChildren<ParallaxBackground>(true).Where(p=>p.enabled))
            {
                layer.targetCamera=camera.transform;
                typeof(ParallaxBackground).GetMethod("Start",flags).Invoke(layer,null);
                typeof(ParallaxBackground).GetMethod("LateUpdate",flags).Invoke(layer,null);
            }
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active; var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                File.WriteAllBytes($"Logs/PlatformGlow/B{(level<=5 ? 1 : 2)}_{label}.png",pixels.EncodeToPNG());
            }
            finally {RenderTexture.active=previous; Object.DestroyImmediate(pixels);}
        }
        finally {Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene);}
    }
}
