#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public class PlatformGlowTests
{
    [TestCase(9)] [TestCase(10)]
    public void RemovingPlatformGlowDoesNotChangeTheSavedBuildingArrangement(int level)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            var facades=root.transform.Find("Terrain").Cast<Transform>().Select(t=>t.Find("BuildingFacade")).Where(t=>t!=null).ToArray();
            var transforms=facades.SelectMany(f=>f.GetComponentsInChildren<Transform>(true)).ToArray();
            var images=facades.SelectMany(f=>f.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            var geometry=transforms.Select(EditorJsonUtility.ToJson).ToArray();
            var artwork=images.Select(EditorJsonUtility.ToJson).ToArray();
            string physics=BiomeLayoutAuthoring.PhysicsSnapshot(root);
            var platforms=root.transform.Find("OneWayPlatforms");
            foreach(var halo in platforms.GetComponentsInChildren<Transform>(true).Where(t=>t.name==PlatformGlowAuthoring.HaloName).ToArray())
                Object.DestroyImmediate(halo.gameObject);
            foreach(var image in platforms.GetComponentsInChildren<SpriteRenderer>(true))
                image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(2,"WalkableEdge"));
            Assert.That(transforms.Select(EditorJsonUtility.ToJson),Is.EqualTo(geometry));
            Assert.That(images.Select(EditorJsonUtility.ToJson),Is.EqualTo(artwork));
            Assert.That(BiomeLayoutAuthoring.PhysicsSnapshot(root),Is.EqualTo(physics));
            var distorted=images.Where(i=>i.name.StartsWith("ArchitectureBay_") && Mathf.Abs(i.transform.lossyScale.x-i.transform.lossyScale.y)>.001f).ToArray();
            Debug.Log($"[Platform isolation] LV{level}: removing all platform glow leaves building geometry exactly unchanged; existing non-uniform architecture bays={distorted.Length}.");
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
    public void PlatformsGlowInTheirOwnBiomeWithoutAffectingOtherArtworkOrPhysics(int level)
    {
        var root=PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
        try
        {
            var platforms=root.transform.Find("OneWayPlatforms");
            var images=platforms.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.GetComponent<PlatformEffector2D>()!=null).ToArray();
            Assert.That(images.Length,Is.EqualTo(LevelDesignLayoutBuilder.GetPlan(level).platforms.Length));
            foreach(var image in images)
            {
                Assert.That(image.sharedMaterial,Is.SameAs(PlatformGlowAuthoring.Material(level<=5 ? 1 : 2)));
                Assert.That(image.GetComponent<PlatformEffector2D>().useOneWay,Is.True);
                Assert.That(image.GetComponent<BoxCollider2D>().usedByEffector,Is.True);
                var halo=image.transform.Find(PlatformGlowAuthoring.HaloName);
                Assert.That(halo,Is.Not.Null);
                Assert.That(halo.GetComponentsInChildren<Collider2D>(),Is.Empty);
                Assert.That(halo.GetComponentsInChildren<MonoBehaviour>(),Is.Empty);
                Assert.That(halo.GetComponent<SpriteRenderer>().sharedMaterial,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(PlatformGlowAuthoring.HaloMaterialPath(level<=5 ? 1 : 2))));
                var bounds=halo.GetComponent<SpriteRenderer>().bounds;
                var floor=LayoutRoadAuthoring.WorldRect(image.GetComponent<BoxCollider2D>());
                Assert.That(bounds.center.y,Is.EqualTo(floor.yMax).Within(.001f));
                Assert.That(bounds.size.y,Is.EqualTo(.8f).Within(.001f));
            }
            var others=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>!r.transform.IsChildOf(platforms)).ToArray();
            var materials=others.Select(r=>r.sharedMaterial).ToArray();
            string before=BiomeMaterialAuthoring.GeometrySnapshot(root);
            PlatformGlowAuthoring.Apply(root); PlatformGlowAuthoring.Apply(root);
            Assert.That(BiomeMaterialAuthoring.GeometrySnapshot(root),Is.EqualTo(before));
            Assert.That(others.Select(r=>r.sharedMaterial),Is.EqualTo(materials));
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }

    [TestCase(1)] [TestCase(2)]
    public void PlatformBrightnessIsIndependentOfBuildingRoofs(int biome)
    {
        var glow=PlatformGlowAuthoring.Material(biome);
        var roof=AssetDatabase.LoadAssetAtPath<Material>(BiomeMaterialAuthoring.MaterialPath(biome,"WalkableEdge"));
        Assert.That(glow,Is.Not.Null); Assert.That(glow,Is.Not.SameAs(roof));
        Assert.That(glow.IsKeywordEnabled("_SURFACE_RIM"),Is.True);
        Assert.That(glow.GetFloat("_EdgeGlowStrength"),Is.GreaterThan(.2f));
        Assert.That(roof.GetFloat("_EdgeGlowStrength"),Is.Zero);
        Assert.That(glow.GetColor("_RimColor").maxColorComponent,Is.EqualTo(1f));
    }

    [TestCase(1)] [TestCase(2)]
    public void HaloRendersASoftFadingLightInsteadOfAnOpaqueRectangle(int biome)
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32); target.Create();
        var previous=RenderTexture.active; Texture2D pixels=null;
        try
        {
            var host=new GameObject("Halo render test"); SceneManager.MoveGameObjectToScene(host,scene);
            var image=host.AddComponent<SpriteRenderer>(); image.sprite=SpriteCropAuthoring.LoadMask();
            image.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(PlatformGlowAuthoring.HaloMaterialPath(biome));
            Assert.That(ShaderUtil.ShaderHasError(image.sharedMaterial.shader),Is.False);
            var cameraHost=new GameObject("Halo camera"); SceneManager.MoveGameObjectToScene(cameraHost,scene);
            var camera=cameraHost.AddComponent<Camera>(); camera.enabled=false; camera.scene=scene;
            camera.orthographic=true; camera.orthographicSize=.5f; camera.transform.position=Vector3.back*10;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear; camera.GetUniversalAdditionalCameraData();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target; pixels=new Texture2D(64,64,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,64,64),0,0); pixels.Apply();
            float core=pixels.GetPixel(32,32).a,soft=pixels.GetPixel(32,44).a,edge=pixels.GetPixel(32,63).a;
            Assert.That(core,Is.GreaterThan(.4f),"The halo must actually render.");
            Assert.That(soft,Is.GreaterThan(.005f)); Assert.That(soft,Is.LessThan(core*.3f));
            Assert.That(edge,Is.LessThan(.01f),"The outer edge must fade away, not show a rectangular panel.");
        }
        finally
        {
            RenderTexture.active=previous; if(pixels!=null) Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
