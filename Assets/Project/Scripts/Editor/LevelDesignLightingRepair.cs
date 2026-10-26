using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LevelDesignLightingRepair
{
    public const string CombatScene="Assets/Project/Scenes/combat scene/combat_1.unity";
    public const float CoveragePadding=24f;

    public static string[] ScenePaths => new[] { CombatScene }.Concat(Enumerable.Range(1,5)
        .Select(i=>$"{LevelDesignLayoutBuilder.SceneFolder}/Test_LV_{i:00}.unity")).ToArray();

    [MenuItem("Tools/Project/Level Design/Expand Existing Scene Light To Cover LV Layouts")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (string path in ScenePaths)
        {
            Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                Light2D light=FindSceneLight(scene);
                if (light.lightType!=Light2D.LightType.Freeform)
                    throw new InvalidOperationException($"Expected the existing Freeform scene light in {path}.");
                Bounds target=RequiredCoverage();
                Vector3[] points=light.shapePath;
                Vector3 min=points.Select(light.transform.TransformPoint).Aggregate(Vector3.Min);
                Vector3 max=points.Select(light.transform.TransformPoint).Aggregate(Vector3.Max);
                // Keep the old coverage as well: the shell can still host the smaller legacy rooms.
                target.Encapsulate(min); target.Encapsulate(max);
                float xMin=points.Min(p=>p.x),xMax=points.Max(p=>p.x);
                float yMin=points.Min(p=>p.y),yMax=points.Max(p=>p.y);
                Vector3 scale=light.transform.localScale;
                scale.x=target.size.x/(xMax-xMin); scale.y=target.size.y/(yMax-yMin);
                light.transform.localScale=scale;
                light.transform.position=new Vector3(target.center.x-(xMin+xMax)*.5f*scale.x,
                    target.center.y-(yMin+yMax)*.5f*scale.y,light.transform.position.z);
                EditorUtility.SetDirty(light.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save "+path);
                Debug.Log($"[LV Lighting] Expanded existing light in {path}; preserved color, intensity, sorting layers and shape.");
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }

    public static Bounds RequiredCoverage()
    {
        var result=new Bounds(); bool first=true;
        for (int level=1;level<=5;level++)
        {
            var plan=LevelDesignLayoutBuilder.GetPlan(level);
            foreach (Rect rect in plan.solids.Select(r=>LevelDesignLayoutBuilder.ToWorld(plan,r)))
            {
                if (first) { result=new Bounds(rect.center,Vector3.zero); first=false; }
                result.Encapsulate(new Vector3(rect.xMin,rect.yMin,0));
                result.Encapsulate(new Vector3(rect.xMax,rect.yMax,0));
            }
        }
        result.Expand(new Vector3(CoveragePadding*2,CoveragePadding*2,0));
        return result;
    }

    public static Light2D FindSceneLight(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(g=>g.GetComponentsInChildren<Light2D>(true)).Single(l=>l.name=="Global Light 2D");
}
