#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class LevelDesignLightingTests
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void ExistingSceneLightCoversEveryLayoutWithoutChangingItsLook(int sceneIndex)
    {
        Scene scene=EditorSceneManager.OpenScene(LevelDesignLightingRepair.ScenePaths[sceneIndex],OpenSceneMode.Additive);
        GameObject probe=new GameObject("Light Coverage Polygon");
        try
        {
            var light=LevelDesignLightingRepair.FindSceneLight(scene);
            Assert.That(light.lightType,Is.EqualTo(Light2D.LightType.Freeform));
            Assert.That(light.color,Is.EqualTo(Color.white));
            Assert.That(light.intensity,Is.EqualTo(1f));
            Assert.That(light.blendStyleIndex,Is.EqualTo(0));
            Assert.That(light.shadowsEnabled,Is.False);
            var polygon=probe.AddComponent<PolygonCollider2D>();
            polygon.points=light.shapePath.Select(p=>(Vector2)light.transform.TransformPoint(p)).ToArray();
            Physics2D.SyncTransforms();
            for (int level=1;level<=5;level++)
            {
                var plan=LevelDesignLayoutBuilder.GetPlan(level);
                foreach (Rect rect in plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)))
                    foreach (Vector2 p in new[] {rect.min,rect.max,new Vector2(rect.xMin,rect.yMax),new Vector2(rect.xMax,rect.yMin)})
                        foreach (Vector2 offset in new[] {new Vector2(-18,-10),new Vector2(18,10)})
                            Assert.That(polygon.OverlapPoint(p+offset),Is.True,$"Scene {sceneIndex}, LV{level}: darkness at {p+offset}");
            }
        }
        finally { Object.DestroyImmediate(probe); EditorSceneManager.CloseScene(scene,true); }
    }

    [UnityTest]
    public IEnumerator LitSpritesRenderEquallyBrightAtTheStartMiddleAndFarEnd()
    {
        Scene source=EditorSceneManager.OpenScene(LevelDesignLightingRepair.CombatScene,OpenSceneMode.Additive);
        var light=Object.Instantiate(LevelDesignLightingRepair.FindSceneLight(source).gameObject);
        Scene preview=EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(light,preview);
        EditorSceneManager.CloseScene(source,true);
        var texture=new Texture2D(2,2); texture.SetPixels(Enumerable.Repeat(Color.white,4).ToArray()); texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2);
        var material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
        var target=new RenderTexture(64,64,24); target.Create();
        try
        {
            var tile=new GameObject("Lit Surface Probe"); SceneManager.MoveGameObjectToScene(tile,preview);
            var image=tile.AddComponent<SpriteRenderer>(); image.sprite=sprite; image.sharedMaterial=material;
            var cameraObject=new GameObject("Lighting Verification Camera"); SceneManager.MoveGameObjectToScene(cameraObject,preview);
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false; camera.scene=preview;
            camera.orthographic=true; camera.orthographicSize=2; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black; camera.GetUniversalAdditionalCameraData();
            // Light2D builds its mesh and culling sphere in the editor player loop, not OnEnable.
            for (int i=0;i<3;i++) { EditorApplication.QueuePlayerLoopUpdate(); yield return null; }
            float first=-1;
            foreach (float x in new[] {-35f,70f,150f,220f,1000f})
            {
                tile.transform.position=new Vector3(x,4,0); camera.transform.position=new Vector3(x,4,-10);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);
                RenderTexture previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,64,64),0,0); pixels.Apply();
                    float brightness=pixels.GetPixel(32,32).grayscale;
                    if (x>900f)
                    {
                        Assert.That(brightness,Is.LessThan(.1f),"Probe outside the Freeform light must stay dark");
                        continue;
                    }
                    Assert.That(brightness,Is.GreaterThan(.7f),$"Lit sprite is dark at X={x}");
                    if (first>=0) Assert.That(brightness,Is.EqualTo(first).Within(.03f));
                    first=brightness;
                }
                finally { RenderTexture.active=previous; Object.DestroyImmediate(pixels); }
            }
        }
        finally
        {
            Object.DestroyImmediate(target); Object.DestroyImmediate(material); Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture); EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
#endif
