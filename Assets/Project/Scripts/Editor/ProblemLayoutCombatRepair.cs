using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ProblemLayoutCombatRepair
{
    public const string Output = "Logs/ProblemLayouts";

    [MenuItem("Tools/Project/Level Design/Restore Five Spawn Slots In LV4 And LV9")]
    public static void RestoreFiveSpawnSlots()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        Directory.CreateDirectory(Output);
        foreach (int level in new[] { 4, 9 })
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            string backup = $"{Output}/LV{level}-before-five-slots.prefab.backup";
            if (!File.Exists(backup)) File.Copy(path, backup);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string protectedBefore = ProtectedSnapshot(root);
                var stage = root.GetComponentInChildren<CombatStageController>();
                using (var data = new SerializedObject(stage))
                {
                    var chambers = data.FindProperty("chambers");
                    for (int i = 0; i < stage.ChamberCount; i++)
                    {
                        var room = stage.Chambers[i];
                        float left = room.spawnSlots.Min(s => s.position.x);
                        float right = room.spawnSlots.Max(s => s.position.x);
                        float floor = room.spawnSlots[0].position.y;
                        SetFiveSlots(room, chambers.GetArrayElementAtIndex(i).FindPropertyRelative("spawnSlots"), left, right, floor);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                BindFirstRoom(root, stage);
                if (protectedBefore != ProtectedSnapshot(root))
                    throw new System.InvalidOperationException("Spawn-only change touched protected geometry or artwork: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        const string obsolete = "Assets/Project/Resources/EncounterSO/Encounter_Hard_LV04_LV09.asset";
        if (AssetDatabase.LoadAssetAtPath<EncounterSO>(obsolete) != null)
        {
            var references = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p != obsolete
                && (p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".asset")))
                .Where(p => AssetDatabase.GetDependencies(p, false).Contains(obsolete)).ToArray();
            if (references.Length != 0)
                throw new System.InvalidOperationException("Special Hard palette is still referenced: " + string.Join(", ", references));
            if (!AssetDatabase.DeleteAsset(obsolete)) throw new System.IO.IOException("Cannot remove unused special Hard palette.");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LV4/LV9] Restored five slots per chamber and shared difficulty palettes. All non-spawn artwork, colliders, recovery platforms and chamber wiring preserved.");
    }

    private static string ProtectedSnapshot(GameObject root)
    {
        var stage = root.GetComponentInChildren<CombatStageController>();
        var components = root.GetComponentsInChildren<Component>(true)
            .Where(c => c != null && !(c is CombatStageController) && !(c is RoomWaveManager)
                && !c.transform.name.StartsWith("Spawn_") && c.transform.name != "SpawnPoints")
            .Select(c => AnimationUtility.CalculateTransformPath(c.transform, root.transform) + "|" + c.GetType().FullName + "|" + EditorJsonUtility.ToJson(c));
        var wiring = stage.Chambers.Select(c => $"{c.name}|{c.enemyCount}|{c.encounter?.GetInstanceID()}|{c.entranceWall?.GetInstanceID()}|{c.exitWall?.GetInstanceID()}|{c.enterTrigger?.GetInstanceID()}|{c.spawnRoot?.GetInstanceID()}|"
            + string.Join(",", (c.additionalExitWalls ?? new Collider2D[0]).Select(w => w.GetInstanceID())));
        return string.Join("\n", components.Concat(wiring));
    }

    private static void SetFiveSlots(CombatStageController.Chamber room, SerializedProperty slots, float left, float right, float y)
    {
        left = Mathf.Max(left, LayoutRoadAuthoring.WorldRect((BoxCollider2D)room.entranceWall).xMax + 1.5f);
        right = Mathf.Min(right, LayoutRoadAuthoring.WorldRect((BoxCollider2D)room.exitWall).xMin - 1.5f);
        if (right - left < 10.8f) throw new System.InvalidOperationException("Not enough clear space for five separated slots: " + room.name);
        slots.arraySize = 5;
        for (int n = 0; n < 5; n++)
        {
            var slot = room.spawnRoot.Find($"Spawn_{n}");
            if (slot == null) throw new System.InvalidOperationException("Missing original spawn marker: " + room.name);
            slot.position = new Vector3(Mathf.Lerp(left, right, n / 4f), y, slot.position.z);
            slots.GetArrayElementAtIndex(n).objectReferenceValue = slot;
        }
        foreach (Transform child in room.spawnRoot.Cast<Transform>().ToArray())
            if (child.name.StartsWith("Spawn_") && int.TryParse(child.name.Substring(6), out int index) && index >= 5)
                Object.DestroyImmediate(child.gameObject);
    }

    private static void BindFirstRoom(GameObject root, CombatStageController stage)
    {
        using (var data = new SerializedObject(root.GetComponentInChildren<RoomWaveManager>()))
        {
            var slots = data.FindProperty("spawnSlots"); slots.arraySize = 5;
            for (int n = 0; n < 5; n++) slots.GetArrayElementAtIndex(n).objectReferenceValue = stage.Chambers[0].spawnSlots[n];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [MenuItem("Tools/Project/Level Design/Repair LV4 And LV9 Combat Areas")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        Directory.CreateDirectory(Output);
        foreach (int level in new[] { 4, 9 })
        {
            string path = BiomeLayoutAuthoring.LayoutPath(level);
            string backup = $"{Output}/LV{level}-original.prefab.backup";
            if (!File.Exists(backup)) File.Copy(path, backup);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Retain the artist's original objects, but retire the low enclosed bridge.
                foreach (string name in level == 4 ? new[] { "Ground_08", "Ground_09" } : new[] { "Ground_07", "Ground_08" })
                    root.transform.Find("Terrain/" + name).gameObject.SetActive(false);
                var ceiling = root.transform.Find("Boundaries/Ceiling").GetComponent<BoxCollider2D>();
                var gate = root.transform.Find("Boundaries/ChamberGates/ChamberGate_05").GetComponent<BoxCollider2D>();
                Rect ground = LayoutRoadAuthoring.WorldRect(root.transform.Find("Terrain/Ground_01").GetComponent<BoxCollider2D>());
                float top = LayoutRoadAuthoring.WorldRect(ceiling).yMin;
                gate.transform.position = new Vector3(gate.transform.position.x, (ground.yMin + top) * .5f, 0);
                gate.offset = Vector2.zero;
                gate.size = new Vector2(6f, top - ground.yMin);
                var seal = root.transform.Find("Boundaries/UpperExitSeal");
                if (seal != null) seal.GetComponent<BoxCollider2D>().enabled = false;
                var platforms = root.transform.Find("OneWayPlatforms");
                float roof = level == 4 ? 17.32f : 22.12f;
                for (int i = 1; i <= Mathf.CeilToInt(roof / 3.2f); i++)
                    AddPlatform(platforms, $"Recovery_{i:00}", 191.8f, i * 3.2f);
                if (level == 9) AddPlatform(platforms, "RoofTransition", 138.7f, 19.8f);
                var stage = root.GetComponentInChildren<CombatStageController>();
                using (var data = new SerializedObject(stage))
                {
                    var chambers = data.FindProperty("chambers");
                    float[] floors = level == 4 ? new[] { 13.56f, 11.12f, 6.2f, 17.32f, 17.32f, 0f }
                        : new[] { 2.4f, 7.12f, 2.4f, 17.32f, 22.12f, 0f };
                    for (int i = 0; i < stage.ChamberCount; i++)
                    {
                        var room = stage.Chambers[i];
                        float left = LayoutRoadAuthoring.WorldRect((BoxCollider2D)room.entranceWall).xMax;
                        float right = LayoutRoadAuthoring.WorldRect((BoxCollider2D)room.exitWall).xMin;
                        if (i == 3 || i == 4)
                        {
                            Vector3 entry = room.enterTrigger.transform.position;
                            entry.x = left + 1f;
                            room.enterTrigger.transform.position = entry;
                        }
                        float center = (left + right) * .5f + (i == 5 ? 1.8f : 0f);
                        var item = chambers.GetArrayElementAtIndex(i);
                        SetFiveSlots(room, item.FindPropertyRelative("spawnSlots"), center - 14.85f, center + 14.85f, floors[i] + .05f);
                        item.FindPropertyRelative("additionalExitWalls").arraySize = 0;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                BindFirstRoom(root, stage);
                root.GetComponentInChildren<CompositeCollider2D>().GenerateGeometry();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Capture(level, "after");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LV4/LV9 repair] Retired low bridges, added return ladders, sealed encounter bypass and kept five safe slots per floor. Enemy counts use shared difficulty palettes.");
    }

    private static void AddPlatform(Transform parent, string name, float x, float top)
    {
        Transform step = parent.Find(name);
        if (step == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(LevelDesignLayoutBuilder.PlatformPrefabPath);
            step = ((GameObject)PrefabUtility.InstantiatePrefab(source, parent)).transform;
            step.name = name;
        }
        var box = step.GetComponent<BoxCollider2D>();
        var image = step.GetComponent<SpriteRenderer>();
        var reference = parent.GetChild(0).GetComponent<SpriteRenderer>();
        image.sharedMaterial = reference.sharedMaterial;
        image.sprite = reference.sprite;
        image.drawMode = reference.drawMode;
        image.size = reference.size;
        image.color = reference.color;
        PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        float height = box.size.y * step.localScale.y;
        step.localScale = new Vector3(4.5f / box.size.x, step.localScale.y, 1);
        step.position = new Vector3(x, top - height * .5f - box.offset.y * step.localScale.y, 0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(step);
    }

    public static void Inspect()
    {
        Directory.CreateDirectory(Output);
        foreach (int level in new[] { 4, 9 })
        {
            var root = PrefabUtility.LoadPrefabContents(BiomeLayoutAuthoring.LayoutPath(level));
            try
            {
                var lines = new List<string>();
                foreach (var box in root.GetComponentsInChildren<BoxCollider2D>(true))
                    lines.Add($"BOX {AnimationUtility.CalculateTransformPath(box.transform, root.transform)} {LayoutRoadAuthoring.WorldRect(box).ToString("F3")} trigger={box.isTrigger} enabled={box.enabled}");
                foreach (var room in root.GetComponentInChildren<CombatStageController>().Chambers)
                {
                    lines.Add($"ROOM {room.name} entrance={room.entranceWall.name} exit={room.exitWall.name} extras={string.Join(",", (room.additionalExitWalls ?? new Collider2D[0]).Select(w => w.name))}");
                    foreach (var slot in room.spawnSlots) lines.Add($"SPAWN {room.name}/{slot.name} {slot.position.ToString("F3")}");
                }
                File.WriteAllLines($"{Output}/LV{level}-inspection.txt", lines);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Capture(level, "before");
        }
    }

    public static void Capture(int level, string suffix)
    {
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.NewPreviewScene();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(BiomeLayoutAuthoring.LayoutPath(level));
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        var cameraObject = new GameObject("Layout Combat Review Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .075f);
        camera.GetUniversalAdditionalCameraData().renderShadows = false;
        var boxes = root.transform.Find("Terrain").GetComponentsInChildren<BoxCollider2D>();
        float left = boxes.Min(b => LayoutRoadAuthoring.WorldRect(b).xMin);
        float right = boxes.Max(b => LayoutRoadAuthoring.WorldRect(b).xMax);
        float top = boxes.Max(b => LayoutRoadAuthoring.WorldRect(b).yMax);
        camera.transform.position = new Vector3((left + right) * .5f, top * .5f, -100);
        camera.orthographicSize = Mathf.Max(24f, (right - left) / 4f + 3f);
        var target = new RenderTexture(1920, 960, 24);
        target.Create();
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes($"{Output}/LV{level}-{suffix}.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
