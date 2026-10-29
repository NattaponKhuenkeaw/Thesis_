using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public static class SpawnSafetyRepair
{
    public const float Separation=2.4f;

    public static int TargetRoom(int level)=>level==1 ? 1 : 0;
    public static bool HasTarget(int level)=>level==1 || level==2 || level==5 || level==10;

    public static void Apply()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var scenes=Directory.GetFiles("Assets/Project/Scenes","*.unity",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);
        int adjusted=0;
        foreach(string path in Directory.GetFiles("Assets/Project/Resources/RoomLayouts","*.prefab",SearchOption.AllDirectories))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var managers=root.GetComponentsInChildren<RoomWaveManager>(true);
                if(managers.Length==0) continue;
                var stage=root.GetComponentInChildren<CombatStageController>(true);
                int level=0;
                string filename=Path.GetFileNameWithoutExtension(path);
                if(filename.StartsWith("RoomLayout_LV_",StringComparison.Ordinal))
                    int.TryParse(filename.Substring("RoomLayout_LV_".Length),out level);
                var target=HasTarget(level) ? stage.Chambers[TargetRoom(level)].spawnSlots[4] : null;
                string before=ProtectedSnapshot(root,target);
                foreach(var waves in managers)
                {
                    using(var data=new SerializedObject(waves))
                    {
                        var gap=data.FindProperty("minSpawnSeparation");
                        gap.floatValue=Mathf.Max(gap.floatValue,Separation);
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(waves);
                }
                if(target!=null)
                {
                    Vector3 position=target.position;
                    float x=LevelDesignSpawnBuilder.SpawnPosition(level,TargetRoom(level),4).x;
                    if(Mathf.Abs(position.x-x)>.001f && Mathf.Abs(position.x-(x+.5f))>.001f)
                        throw new InvalidOperationException("Spawn moved since audit; refusing to overwrite it: "+path);
                    position.x=x; target.position=position;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    adjusted++;
                    Debug.Log($"[Spawn repair] LV{level} Room{TargetRoom(level)+1} Spawn_4 X={x:F3}; Y/Z preserved.");
                }
                if(before!=ProtectedSnapshot(root,target))
                    throw new InvalidOperationException("Spawn repair touched protected artwork, collision or gameplay: "+path);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        if(adjusted!=4) throw new InvalidOperationException("Expected exactly four adjusted spawn markers.");
        foreach(var scene in scenes)
            if(!scene.Value.SequenceEqual(File.ReadAllBytes(scene.Key))) throw new InvalidOperationException("Spawn repair changed a scene: "+scene.Key);
        Debug.Log("[Spawn repair] Four markers and minimum spawn separation updated. Every other component, artwork transform, collider and scene preserved.");
        DecoratedSpawnAudit.Run();
    }

    private static string ProtectedSnapshot(GameObject root,Transform target)
    {
        return string.Join("\n",root.GetComponentsInChildren<Component>(true).Select(component=>
        {
            if(component==null) return "Missing component";
            var json=JObject.Parse(EditorJsonUtility.ToJson(component));
            var fields=(JObject)json.Properties().Single().Value;
            if(component is RoomWaveManager) fields.Remove("minSpawnSeparation");
            if(component==target) ((JObject)fields["m_LocalPosition"])["x"]=0;
            return AnimationUtility.CalculateTransformPath(component.transform,root.transform)+"|"+component.GetType().FullName+"|"+json.ToString(Formatting.None);
        }));
    }
}
