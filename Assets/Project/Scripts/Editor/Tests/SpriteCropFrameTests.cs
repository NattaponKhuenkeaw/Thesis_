#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.IO;
using NUnit.Framework;
using Project.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class SpriteCropFrameTests
{
    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void PrefabsAreStandaloneVisualToolsWithOriginalSpritesAndNoCollision(int index)
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(SpriteCropAuthoring.PrefabPath(index));
        Assert.That(root,Is.Not.Null);
        var frame=root.GetComponent<SpriteCropFrame>();
        Assert.That(frame,Is.Not.Null);
        Assert.That(frame.SourceSprite,Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(frame.SourceSprite),Is.EqualTo($"Assets/Project/Art/BG/B1/B1_Parallax_Foreground{index}.PNG"));
        Assert.That(frame.Artwork.drawMode,Is.EqualTo(SpriteDrawMode.Simple));
        Assert.That(frame.Artwork.maskInteraction,Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
        Assert.That(frame.Artwork.sharedMaterial,Is.Not.Null);
        Assert.That(root.GetComponent<SortingGroup>(),Is.Not.Null);
        Assert.That(frame.ClippingMask.isCustomRangeActive,Is.True);
        Assert.That(root.GetComponentsInChildren<Collider2D>(true),Is.Empty);
        Assert.That(root.GetComponentsInChildren<Rigidbody2D>(true),Is.Empty);
        Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).Length,Is.EqualTo(1));
    }

    [TestCase(.5f,.25f)] [TestCase(2f,3f)] [TestCase(-2f,1.5f)]
    public void FrameAndParentScaleCannotStretchTheSourceImage(float x,float y)
    {
        WithFixture((scene,camera,frame)=>
        {
            Vector3 size=frame.Artwork.bounds.size;
            var parent=new GameObject("Scaled art parent"); SceneManager.MoveGameObjectToScene(parent,scene);
            frame.transform.SetParent(parent.transform,false); parent.transform.localScale=new Vector3(2,3,1);
            frame.transform.localScale=new Vector3(x,y,1); frame.Refresh();
            Assert.That(frame.Artwork.bounds.size.x,Is.EqualTo(size.x).Within(.001f));
            Assert.That(frame.Artwork.bounds.size.y,Is.EqualTo(size.y).Within(.001f));
            Assert.That(float.IsNaN(frame.Artwork.transform.localScale.x),Is.False);
        });
    }

    [Test]
    public void MovingOneCropEdgeDoesNotMoveOrResizeTheSource()
    {
        WithFixture((scene,camera,frame)=>
        {
            frame.transform.localScale=new Vector3(2,3,1); frame.Refresh();
            Vector3 center=frame.Artwork.bounds.center, size=frame.Artwork.bounds.size;
            frame.ResizeLocalRect(new Rect(-2,-2,2,4));
            Assert.That(Vector3.Distance(frame.Artwork.bounds.center,center),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(frame.Artwork.bounds.size,size),Is.LessThan(.001f));
            Assert.That(frame.CropSize,Is.EqualTo(new Vector2(2,4)));
            Assert.That(frame.transform.position.x,Is.EqualTo(-2f).Within(.001f));
            Vector3 imageBefore=frame.Artwork.bounds.center;
            frame.transform.position+=Vector3.right*3; frame.Refresh();
            Assert.That(frame.Artwork.bounds.center.x,Is.EqualTo(imageBefore.x+3).Within(.001f),"Moving the placed piece must still move its artwork normally.");
        });
    }

    [Test]
    public void RenderedPixelsStayFixedWhileCroppingAndNeverRepeatPastTheSource()
    {
        WithFixture((scene,camera,frame)=>
        {
            var full=Render(camera);
            frame.CropSize=new Vector2(2,2);
            var cropped=Render(camera);
            try
            {
                foreach(float x in new[]{-.75f,-.25f,.25f,.75f})
                    Assert.That(Pixel(cropped,x,0),Is.EqualTo(Pixel(full,x,0)),"Cropping must not remap the original pixels.");
                Assert.That(Pixel(cropped,1.5f,0).a,Is.LessThan(.01f));
                frame.CropSize=new Vector2(10,10);
                var expanded=Render(camera);
                try
                {
                    Assert.That(Pixel(expanded,0,0).a,Is.GreaterThan(.9f));
                    Assert.That(Pixel(expanded,3,0).a,Is.LessThan(.01f),"Window larger than source must be transparent, not tiled.");
                    Assert.That(Pixel(expanded,-3,0).a,Is.LessThan(.01f));
                }
                finally {Object.DestroyImmediate(expanded);}
                frame.CropSize=new Vector2(2,2); frame.transform.localScale=new Vector3(2,.5f,1); frame.Refresh();
                var scaled=Render(camera);
                try
                {
                    Assert.That(Pixel(scaled,.75f,0),Is.EqualTo(Pixel(full,.75f,0)));
                    Assert.That(Pixel(scaled,0,.75f).a,Is.LessThan(.01f),"Scale must resize only the crop window.");
                }
                finally {Object.DestroyImmediate(scaled);}
            }
            finally {Object.DestroyImmediate(full); Object.DestroyImmediate(cropped);}
        });
    }

    [Test]
    public void SeparateCropFramesDoNotMaskEachOther()
    {
        WithFixture((scene,camera,frame)=>
        {
            frame.CropSize=Vector2.one;
            var other=SpriteCropAuthoring.Create(frame.SourceSprite,SpriteCropAuthoring.LoadMask());
            SceneManager.MoveGameObjectToScene(other,scene); other.transform.position=Vector3.right*2.5f;
            other.GetComponent<SpriteCropFrame>().CropSize=Vector2.one;
            var image=Render(camera);
            try
            {
                Assert.That(Pixel(image,0,0).a,Is.GreaterThan(.9f));
                Assert.That(Pixel(image,2.5f,0).a,Is.GreaterThan(.9f));
                Assert.That(Pixel(image,1,0).a,Is.LessThan(.01f));
            }
            finally {Object.DestroyImmediate(image);}
        });
    }

    [Test]
    public void ToolIsNotAutomaticallyAddedToAnyExistingLayout()
    {
        var saved=Enumerable.Range(1,10).ToDictionary(BiomeLayoutAuthoring.LayoutPath,i=>File.ReadAllBytes(BiomeLayoutAuthoring.LayoutPath(i)));
        SpriteCropAuthoring.Prepare();
        for(int level=1;level<=10;level++)
        {
            string path=BiomeLayoutAuthoring.LayoutPath(level);
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(saved[path]),"Preparing tools must neither add crops nor remove manually placed crops.");
        }
    }

    private static void WithFixture(System.Action<Scene,Camera,SpriteCropFrame> test)
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var texture=new Texture2D(8,8,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var colors=Enumerable.Range(0,64).Select(i=>new[]{Color.red,Color.green,Color.blue,Color.yellow}[i%4]).ToArray();
        texture.SetPixels(colors); texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f,2,0,SpriteMeshType.FullRect);
        try
        {
            var root=SpriteCropAuthoring.Create(sprite,SpriteCropAuthoring.LoadMask()); SceneManager.MoveGameObjectToScene(root,scene);
            var host=new GameObject("Crop render test camera"); SceneManager.MoveGameObjectToScene(host,scene);
            var camera=host.AddComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.orthographic=true; camera.orthographicSize=4;
            camera.transform.position=new Vector3(0,0,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear;
            camera.GetUniversalAdditionalCameraData();
            test(scene,camera,root.GetComponent<SpriteCropFrame>());
        }
        finally {EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);}
    }

    private static Texture2D Render(Camera camera)
    {
        var target=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32); target.Create();
        var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;
            var image=new Texture2D(128,128,TextureFormat.RGBA32,false); image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply(); return image;
        }
        finally {RenderTexture.active=previous; target.Release(); Object.DestroyImmediate(target);}
    }
    private static Color Pixel(Texture2D image,float x,float y) => image.GetPixel(Mathf.FloorToInt((x+4)*16),Mathf.FloorToInt((y+4)*16));
}
#endif
