using System;
using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LevelDesignSpawnBuilder
{
    public sealed class RoomPlan
    {
        public Vector2 entry;
        public Vector2[] slots;
    }

    private static RoomPlan Row(float entryX,float entryY,float floorY,params float[] xs) =>
        new RoomPlan { entry=new Vector2(entryX,entryY),slots=xs.Select(x=>new Vector2(x,floorY)).ToArray() };
    private static Vector2 S(float x,float y) => new Vector2(x,y);

    public static Vector3 SpawnPosition(int level,int room,int slot)
    {
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        Vector3 position=LevelDesignLayoutBuilder.ToWorld(plan,Rooms(level)[room].slots[slot])+Vector2.up*.05f;
        return position;
    }

    public static RoomPlan[] Rooms(int level)
    {
        switch (level)
        {
            case 1: return new[] {
                Row(166,848,848,208,242,276,310,344), Row(500,706,848,574,608,642,676,710),
                Row(860,640,848,930,970,1010,1050,1090), Row(1266,706,707.5f,1270,1310,1350,1390,1430),
                Row(1600,706,707.5f,1610,1650,1690,1730,1770) };
            case 2: return new[] {
                Row(166,747,747,220,245,270,295,320), Row(475,867,867,480,515,550,585,620),
                Row(780,695,695,790,830,870,910,950), Row(1128,867,867,1140,1180,1220,1260,1300),
                Row(1476,691,515,1540,1600,1660,1720,1770) };
            case 3: return new[] {
                Row(166,861,861,218,246,274,302,330), Row(490,737,737,500,535,570,605,640),
                Row(800,671,671,810,840,870,900,930), Row(1090,496,496,1100,1150,1200,1250,1300),
                Row(1470,671,861,1520,1580,1640,1700,1760) };
            case 4: return new[] {
                Row(166,578,578,220,247,274,301,328), Row(480,639,639,490,525,560,595,630),
                Row(790,762,762,800,840,880,920,960), Row(1125,484,484,1158,1188,1220,1260,1300),
                new RoomPlan { entry=S(1468,484),slots=new[] { S(1490,484),S(1540,484),S(1360,641),S(1230,917),S(1430,917) } },
                Row(1634,917,917,1640,1675,1710,1745,1780) };
            case 5: return new[] {
                Row(166,865.4f,865.4f,210,234,258,282,306), Row(440,662.9f,662.9f,470,498,526,554,582),
                Row(715,596.5f,359.4f,900,945,990,1035,1080),Row(1220,425.2f,425.2f,1250,1285,1320,1355,1390),
                Row(1530,865.4f,865.4f,1570,1615,1660,1705,1750) };
            default: return Biome2LevelPlans.Rooms(level);
        }
    }

    public static void BuildAndCapture()
    {
        BuildAll();
        LevelDesignLayoutPreview.CaptureAll();
    }

    [MenuItem("Tools/Project/Level Design/Rebuild LV Chamber Spawns And Gates")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var palette=AssetDatabase.LoadAssetAtPath<EncounterSO>("Assets/Project/Resources/EncounterSO/Encounter_Easy.asset");
        for (int level=1;level<=5;level++)
        {
            string path=$"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { Configure(root,level,palette); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LV Spawns] Configured 27 chambers, at least five spawn slots each, thick gates and difficulty-card palettes.");
    }

    public static void Configure(GameObject root,int level,EncounterSO palette)
    {
        var plan=LevelDesignLayoutBuilder.GetPlan(level);
        var rooms=Rooms(level);
        Transform boundaries=root.transform.Find("Boundaries");
        Transform gates=boundaries.Find("ChamberGates_OpenForTraversal") ?? boundaries.Find("ChamberGates");
        gates.name="ChamberGates";
        BoxCollider2D[] doors=Enumerable.Range(1,plan.gates.Length)
            .Select(i=>gates.Find($"ChamberGate_{i:00}").GetComponent<BoxCollider2D>()).ToArray();
        foreach (var wall in boundaries.GetComponentsInChildren<BoxCollider2D>())
        {
            Vector2 size=wall.size;
            if (wall.name=="Ceiling") size.y=Mathf.Max(size.y,6f);
            else size.x=Mathf.Max(size.x,6f);
            wall.size=size;
        }
        foreach (var door in doors) { door.enabled=true; door.isTrigger=false; }
        var old=root.transform.Find("CombatChambers");
        if (old!=null) Object.DestroyImmediate(old.gameObject);
        var content=Child(root.transform,"CombatChambers");
        var stage=boundaries.GetComponent<CombatStageController>();
        if (stage==null) stage=boundaries.gameObject.AddComponent<CombatStageController>();
        var authored=new CombatStageController.Chamber[rooms.Length];
        Collider2D extra=null;
        if (level==9)
        {
            Transform seal=boundaries.Find("UpperExitSeal");
            if (seal==null) seal=Child(boundaries,"UpperExitSeal").transform;
            var wall=seal.GetComponent<BoxCollider2D>();
            if (wall==null) wall=seal.gameObject.AddComponent<BoxCollider2D>();
            seal.gameObject.layer=LayerMask.NameToLayer("Ground");
            float min=LevelDesignLayoutBuilder.ToWorld(plan,S(0,level==9 ? 762 : 679)).y;
            float max=boundaries.Find("Ceiling").GetComponent<BoxCollider2D>().bounds.min.y;
            seal.localPosition=new Vector3(LevelDesignLayoutBuilder.ToWorld(plan,S(level==9 ? 1595 : 1651,0)).x,(min+max)*.5f,0);
            wall.size=new Vector2(6f,max-min); wall.enabled=false; extra=wall;
        }
        RoomWaveManager waves=null;
        for (int i=0;i<rooms.Length;i++)
        {
            GameObject room=Child(content.transform,$"Room_{i+1:00}");
            GameObject spawns=Child(room.transform,"SpawnPoints");
            Transform[] slots=rooms[i].slots.Select((p,n)=> {
                var slot=Child(spawns.transform,$"Spawn_{n}").transform;
                slot.localPosition=SpawnPosition(level,i,n);
                return slot;
            }).ToArray();
            var trigger=Child(room.transform,i==0 ? "EncounterStart" : "EnterTrigger");
            trigger.transform.localPosition=LevelDesignLayoutBuilder.ToWorld(plan,rooms[i].entry)+Vector2.up*1.4f;
            var sensor=trigger.AddComponent<BoxCollider2D>(); sensor.isTrigger=true; sensor.size=new Vector2(1.6f,2.8f);
            if (i==0) waves=trigger.AddComponent<RoomWaveManager>();
            authored[i]=new CombatStageController.Chamber {
                name=$"LV{level} Room {i+1}",encounter=palette,enemyCount=4,
                entranceWall=i==0 ? boundaries.Find("StageLeftWall").GetComponent<Collider2D>() : doors[i-1],
                exitWall=i==rooms.Length-1 ? boundaries.Find("StageRightWall").GetComponent<Collider2D>() : doors[i],
                enterTrigger=i==0 ? null : sensor,spawnRoot=spawns.transform,spawnSlots=slots,
                additionalExitWalls=level==9 && (i==3 || i==4) ? new[] { extra } : new Collider2D[0]
            };
        }
        using (var data=new SerializedObject(stage))
        {
            var entries=data.FindProperty("chambers"); entries.arraySize=authored.Length;
            for (int i=0;i<authored.Length;i++)
            {
                var item=entries.GetArrayElementAtIndex(i); var room=authored[i];
                item.FindPropertyRelative("name").stringValue=room.name;
                item.FindPropertyRelative("encounter").objectReferenceValue=room.encounter;
                item.FindPropertyRelative("enemyCount").intValue=room.enemyCount;
                item.FindPropertyRelative("entranceWall").objectReferenceValue=room.entranceWall;
                item.FindPropertyRelative("exitWall").objectReferenceValue=room.exitWall;
                item.FindPropertyRelative("enterTrigger").objectReferenceValue=room.enterTrigger;
                item.FindPropertyRelative("spawnRoot").objectReferenceValue=room.spawnRoot;
                SetReferences(item.FindPropertyRelative("spawnSlots"),room.spawnSlots);
                SetReferences(item.FindPropertyRelative("additionalExitWalls"),room.additionalExitWalls);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        using (var data=new SerializedObject(waves))
        {
            SetReferences(data.FindProperty("spawnSlots"),authored[0].spawnSlots);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        ConfigureExit(root,plan);
        ResizeEntranceSensors(root);
    }

    public static void ResizeEntranceSensors(GameObject root)
    {
        var ceiling=root.transform.Find("Boundaries/Ceiling").GetComponent<BoxCollider2D>();
        var stage=root.GetComponentInChildren<CombatStageController>(true);
        var first=root.GetComponentInChildren<RoomWaveManager>(true).GetComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        foreach (var sensor in stage.Chambers.Select(c=>c.enterTrigger as BoxCollider2D)
            .Append(first).Where(c=>c!=null).Distinct())
        {
            // Keep the authored floor and horizontal crossing; cover airborne entries up to the ceiling.
            float bottom=sensor.offset.y-sensor.size.y*.5f;
            float top=sensor.transform.InverseTransformPoint(new Vector3(sensor.bounds.center.x,ceiling.bounds.min.y,0)).y;
            float height=Mathf.Max(2.8f,top-bottom);
            sensor.size=new Vector2(sensor.size.x,height);
            sensor.offset=new Vector2(sensor.offset.x,bottom+height*.5f);
        }
    }

    [MenuItem("Tools/Project/Level Design/Extend LV Entry Triggers To Ceiling")]
    public static void ExtendAllEntryTriggers()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (int level=1;level<=5;level++)
        {
            string path=$"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { ResizeEntranceSensors(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Project/Level Design/Align LV Exits To Map Edge")]
    public static void AlignAllExits()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (int level=1;level<=5;level++)
        {
            string path=$"{LevelDesignLayoutBuilder.LayoutFolder}/RoomLayout_LV_{level:00}.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureExit(root,LevelDesignLayoutBuilder.GetPlan(level));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LV Exits] Aligned LV1-LV5 exits to the inside edge of the final wall; preserved terrain, chambers and spawn points.");
    }

    private static void ConfigureExit(GameObject root,LevelDesignLayoutBuilder.Plan plan)
    {
        Transform boundaries=root.transform.Find("Boundaries");
        Vector2 edge=LevelDesignLayoutBuilder.ToWorld(plan,plan.exit);
        root.transform.Find("ExitGuide").localPosition=edge;
        Transform exit=root.transform.Find("StageExitTrigger");
        if (exit==null) exit=Child(root.transform,"StageExitTrigger").transform;
        var exitBox=exit.GetComponent<BoxCollider2D>();
        if (exitBox==null) exitBox=exit.gameObject.AddComponent<BoxCollider2D>();
        exitBox.isTrigger=true; exitBox.size=new Vector2(1.8f,2.8f);
        // Keep the entire activation zone on walkable ground, flush against the final wall.
        exit.localPosition=edge+new Vector2(-exitBox.size.x*.5f,exitBox.size.y*.5f);
        var transition=exit.GetComponent<TransitionTrigger>();
        if (transition==null) transition=exit.gameObject.AddComponent<TransitionTrigger>();
        using (var data=new SerializedObject(boundaries.GetComponent<SceneBoundaryController>()))
        {
            data.FindProperty("_exitTrigger").objectReferenceValue=transition;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetReferences(SerializedProperty property,Object[] objects)
    {
        property.arraySize=objects.Length;
        for (int i=0;i<objects.Length;i++) property.GetArrayElementAtIndex(i).objectReferenceValue=objects[i];
    }
    private static GameObject Child(Transform parent,string name)
    {
        var obj=new GameObject(name); obj.transform.SetParent(parent,false); return obj;
    }
}
