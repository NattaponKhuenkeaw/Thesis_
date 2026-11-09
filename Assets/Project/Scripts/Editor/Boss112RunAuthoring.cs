#if UNITY_EDITOR
using System.Linq;
using Project.AIBehaviour.Boss;
using Project.Core.ZoneState;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Boss112RunAuthoring
{
    public const string BossPath = "Assets/Project/Character/enemy/Boss/Observer112/Boss112_Run.prefab";
    public const string ArenaPath = "Assets/Project/Resources/RoomLayouts/RoomLayout_Boss_Observer112.prefab";
    public const string EncounterPath = "Assets/Project/Resources/EncounterSO/Encounter_Boss_Observer112.asset";
    public const string RoomPath = "Assets/Project/Resources/MapSO/RoomData_Boss_02.asset";
    private const float ArenaHalfWidth = 23.678f;

    public static void Apply()
    {
        BuildBoss(); BuildArena(); ConfigurePresentation();
        var encounter = AssetDatabase.LoadAssetAtPath<EncounterSO>(EncounterPath);
        if (encounter == null)
        {
            encounter = Object.Instantiate(Resources.Load<EncounterSO>("EncounterSO/Encounter_Boss_Executioner"));
            encounter.name = "Encounter_Boss_Observer112";
            AssetDatabase.CreateAsset(encounter, EncounterPath);
        }
        using (var data = new SerializedObject(encounter))
        {
            var waves = data.FindProperty("waves"); waves.arraySize = 1;
            var wave = waves.GetArrayElementAtIndex(0);
            wave.FindPropertyRelative("name").stringValue = "Observer112";
            var enemies = wave.FindPropertyRelative("enemies"); enemies.arraySize = 1;
            enemies.GetArrayElementAtIndex(0).FindPropertyRelative("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
            enemies.GetArrayElementAtIndex(0).FindPropertyRelative("spawnSlotIndex").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var room = AssetDatabase.LoadAssetAtPath<RoomData>(RoomPath);
        if (room == null)
        {
            room = Object.Instantiate(Resources.Load<RoomData>("MapSO/RoomData_Boss_01"));
            room.name = "RoomData_Boss_02";
            AssetDatabase.CreateAsset(room, RoomPath);
        }
        using (var data = new SerializedObject(room))
        {
            data.FindProperty("roomId").stringValue = "Boss_02";
            data.FindProperty("displayName").stringValue = "Boss 112";
            data.FindProperty("biomeIndex").intValue = 1;
            data.FindProperty("useStaticCamera").boolValue = false;
            data.FindProperty("layoutPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
            data.FindProperty("encounter").objectReferenceValue = encounter;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var map = Resources.Load<MapSO>("MapSO/Test_MapSO");
        if (!map.Rooms.Contains(room)) { map.Rooms.Add(room); EditorUtility.SetDirty(map); }
        AssetDatabase.SaveAssets();
        ApplyEntranceCorridor();
    }

    public static void BuildBoss()
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath) != null;
        var root = existing ? PrefabUtility.LoadPrefabContents(BossPath)
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.BossPath));
        try
        {
            if (!existing)
            {
                root.name = "Boss112_Run";
                var controller = root.GetComponent<Boss112Controller>();
                using (var data = new SerializedObject(controller))
                {
                    data.FindProperty("autoAttack").boolValue = true;
                    data.FindProperty("useRunBossFlow").boolValue = true;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var enemy = root.GetComponent<EnemyController>();
                using (var data = new SerializedObject(enemy))
                {
                    data.FindProperty("useSpawnPresentation").boolValue = false;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
            }
            if (Boss112WormTestAuthoring.AttachBody(root) || !existing)
                PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally
        {
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static void BuildArena()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath) != null) return;
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.ArenaPath));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        try
        {
            root.name = "RoomLayout_Boss_Observer112";
            root.transform.Find("TestArenaTerrain").name = "ArenaTerrain";
            var boxes = root.GetComponentsInChildren<BoxCollider2D>(true);
            var floor = boxes.Single(c => c.name == "Floor");
            var left = boxes.Single(c => c.name == "LeftWall");
            var right = boxes.Single(c => c.name == "RightWall");
            var ceiling = boxes.Single(c => c.name == "Ceiling");
            float terrainWidth = 2f * ArenaHalfWidth + left.size.x + right.size.x;
            floor.size = new Vector2(terrainWidth, floor.size.y);
            ceiling.size = new Vector2(terrainWidth, ceiling.size.y);
            left.transform.localPosition = new Vector3(-ArenaHalfWidth - left.size.x * .5f, left.transform.localPosition.y, 0f);
            right.transform.localPosition = new Vector3(ArenaHalfWidth + right.size.x * .5f, right.transform.localPosition.y, 0f);
            var arena = root.AddComponent<Boss112Arena>();
            Bind(arena, "floor", floor); Bind(arena, "leftWall", left); Bind(arena, "rightWall", right);
            Bind(arena, "departurePoint", Child(root.transform, "DeparturePoint", new Vector3(0f, 23f, 0f)));
            var entrance = Child(root.transform, "Entrance_Default", new Vector3(-28.2f, .05f, 0f)).gameObject.AddComponent<EntrancePoint>();
            Bind(entrance, "stopPoint", Child(root.transform, "StopPoint", new Vector3(-24.2f, .05f, 0f)));
            var boundaries = root.AddComponent<SceneBoundaryController>();
            Bind(boundaries, "_entranceWall", left); Bind(boundaries, "_exitWall", right);
            var trigger = Child(root.transform, "BossSpawnTrigger", new Vector3(0f, 20f, 0f));
            var box = trigger.gameObject.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(36f, 44f);
            var waves = trigger.gameObject.AddComponent<RoomWaveManager>();
            using (var data = new SerializedObject(waves))
            {
                data.FindProperty("useBossDeathFlow").boolValue = true;
                data.FindProperty("unlockSceneExitOnClear").boolValue = false;
                var slots = data.FindProperty("spawnSlots"); slots.arraySize = 1;
                slots.GetArrayElementAtIndex(0).objectReferenceValue = Child(root.transform, "BossSpawnPoint", new Vector3(0f, 7f, 0f));
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var camera = Child(root.transform, "Boss112ArenaCamera", new Vector3(0f, 9f, -17f)).gameObject.AddComponent<CinemachineCamera>();
            camera.Priority = 250;
            var lens = camera.Lens; lens.OrthographicSize = 14f; lens.ModeOverride = LensSettings.OverrideModes.Orthographic; camera.Lens = lens;
            PrefabUtility.SaveAsPrefabAsset(root, ArenaPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ConfigurePresentation()
    {
        var boss = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            // Hover is owned by Boss112Controller; ground-fall animation would wait forever on its kinematic body.
            using var data = new SerializedObject(boss.GetComponent<EnemyController>());
            if (data.FindProperty("useSpawnPresentation").boolValue)
            {
                data.FindProperty("useSpawnPresentation").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(boss, BossPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        var arena = PrefabUtility.LoadPrefabContents(ArenaPath);
        try
        {
            var camera = arena.GetComponentInChildren<CinemachineCamera>(true);
            if (camera.GetComponent<CinemachineImpulseListener>() == null)
            {
                camera.gameObject.AddComponent<CinemachineImpulseListener>();
                PrefabUtility.SaveAsPrefabAsset(arena, ArenaPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(arena); }
    }

    public static void ApplyEntranceCorridor()
    {
        var root = PrefabUtility.LoadPrefabContents(ArenaPath);
        try
        {
            var entrance = root.GetComponentInChildren<EntrancePoint>(true);
            var floor = root.transform.Find("ArenaTerrain/Floor").GetComponent<BoxCollider2D>();
            var gate = root.transform.Find("ArenaTerrain/LeftWall").GetComponent<BoxCollider2D>();
            var right = root.transform.Find("ArenaTerrain/RightWall").GetComponent<BoxCollider2D>();
            float ground = floor.bounds.max.y;
            float corridorLeft = Mathf.Min(entrance.SpawnPosition.x, entrance.StopPosition.x) - 4f;
            float corridorRight = floor.bounds.min.x + 2f;
            if (corridorRight <= corridorLeft || entrance.StopPosition.x >= gate.bounds.max.x)
                throw new System.InvalidOperationException("Boss 112 entrance and stop point must be in the corridor, left of the arena gate.");
            var corridor = GetOrCreateBox(root.transform, "EntranceCorridorFloor", floor.gameObject.layer);
            corridor.transform.position = new Vector3((corridorLeft + corridorRight) * .5f, ground - 2f, floor.transform.position.z);
            corridor.size = new Vector2(corridorRight - corridorLeft, 4f);
            corridor.isTrigger = false;
            corridor.sharedMaterial = floor.sharedMaterial;
            var startWall = GetOrCreateBox(root.transform, "StageStartWall", gate.gameObject.layer);
            startWall.transform.position = new Vector3(corridorLeft - 3f, gate.bounds.center.y, gate.transform.position.z);
            startWall.size = new Vector2(6f, gate.bounds.size.y);
            startWall.isTrigger = false;
            startWall.sharedMaterial = gate.sharedMaterial;
            gate.isTrigger = true;
            var boundary = root.GetComponent<SceneBoundaryController>();
            Bind(boundary, "_entranceWall", startWall);
            Bind(boundary, "_fightEntranceWall", gate);
            float triggerLeft = gate.bounds.max.x + 3f;
            float triggerRight = right.bounds.min.x;
            var trigger = root.transform.Find("BossSpawnTrigger").GetComponent<BoxCollider2D>();
            trigger.transform.position = new Vector3((triggerLeft + triggerRight) * .5f, ground + 20f, trigger.transform.position.z);
            trigger.offset = Vector2.zero;
            trigger.size = new Vector2(triggerRight - triggerLeft, 44f);
            PrefabUtility.SaveAsPrefabAsset(root, ArenaPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var room = AssetDatabase.LoadAssetAtPath<RoomData>(RoomPath);
        using var data = new SerializedObject(room);
        data.FindProperty("useStaticCamera").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static BoxCollider2D GetOrCreateBox(Transform root, string name, int layer)
    {
        var child = root.Find(name);
        if (child == null) child = Child(root, name, Vector3.zero);
        child.gameObject.layer = layer;
        var collider = child.GetComponent<BoxCollider2D>();
        if (collider == null) collider = child.gameObject.AddComponent<BoxCollider2D>();
        return collider;
    }

    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child;
    }
    private static void Bind(Object target, string field, Object value)
    {
        using var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
