using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LevelDesignLayoutPreview
{
    public static void FinalizeAndCapture()
    {
        LevelDesignLayoutBuilder.RefreshDraftTileReferences();
        LevelDesignLayoutBuilder.ResetTestPlayerStarts();
        CaptureAll();
    }

    [MenuItem("Tools/Project/Level Design/Capture LV Overview Images")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory("Library/LevelDesignPreviews");
        for (int level = 1; level <= 5; level++)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            var cameraObject = new GameObject("LV Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.045f,.075f);
            camera.GetUniversalAdditionalCameraData().renderShadows = false;
            var plan = LevelDesignLayoutBuilder.GetPlan(level);
            float highest = plan.solids.Max(r=>LevelDesignLayoutBuilder.ToWorld(plan,r).yMax);
            var target = new RenderTexture(1920,960,24);
            target.Create();
            try
            {
                // Preview scenes render only their own objects, independent of the current editor scene.
                camera.scene = scene;
                float left=LevelDesignLayoutBuilder.WorldX(plan,108);
                float right=LevelDesignLayoutBuilder.WorldX(plan,1812);
                camera.transform.position = new Vector3((left+right)*.5f,highest*.5f,-100);
                camera.orthographicSize = Mathf.Max(24f,(right-left)/4f+3f);
                Capture(camera,target,$"Library/LevelDesignPreviews/LV_{level:00}.png");
                camera.transform.position = (Vector3)LevelDesignLayoutBuilder.ToWorld(plan,plan.start)+new Vector3(6,4,-100);
                camera.orthographicSize = 8f;
                Capture(camera,target,$"Library/LevelDesignPreviews/LV_{level:00}_Start.png");
            }
            finally
            {
                Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        Debug.Log("[LV Layouts] Captured five overviews and five start-area views in Library/LevelDesignPreviews.");
    }

    private static void Capture(Camera camera,RenderTexture target,string path)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera,request);
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
            image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
    }
}
