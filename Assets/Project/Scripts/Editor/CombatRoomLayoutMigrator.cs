#if UNITY_EDITOR
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.EditorTools
{
    /// <summary>
    /// Moves combat_1 baked room content into RoomLayout_Combat_01 and leaves a shell + RoomLayoutHost.
    /// </summary>
    public static class CombatRoomLayoutMigrator
    {
        private const string ScenePath = "Assets/Project/Scenes/combat scene/combat_1.unity";
        private const string OutputFolder = "Assets/Project/Resources/RoomLayouts";
        private const string OutputPrefabPath = OutputFolder + "/RoomLayout_Combat_01.prefab";
        private const string RoomDataPath = "Assets/Project/Resources/MapSO/RoomData_Combat_01.asset";

        [MenuItem("Project/Rooms/Migrate combat_1 Into RoomLayout_Combat_01")]
        public static void MigrateCombat1()
        {
            if (!EditorUtility.DisplayDialog(
                    "Migrate combat_1",
                    "This will:\n" +
                    "• Create RoomLayout_Combat_01.prefab from EnvironmentDependency / waves / background\n" +
                    "• Remove those objects from combat_1\n" +
                    "• Add RoomLayoutHost + bootstrap Combat_01\n\n" +
                    "Save any open work first. Continue?",
                    "Migrate",
                    "Cancel"))
            {
                return;
            }

            if (!MigrateInternal(interactive: true))
            {
                EditorUtility.DisplayDialog("Migrate combat_1", "Migration failed. See Console.", "OK");
            }
        }

        /// <summary>Batchmode entry: -executeMethod Project.EditorTools.CombatRoomLayoutMigrator.MigrateCombat1Batch</summary>
        public static void MigrateCombat1Batch()
        {
            bool ok = MigrateCombat1Silent();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool MigrateCombat1Silent()
        {
            return MigrateInternal(interactive: false);
        }

        private static bool MigrateInternal(bool interactive)
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError($"[CombatRoomLayoutMigrator] Scene missing: {ScenePath}");
                return false;
            }

            // Don't wipe an unsaved combat_1 the user is mid-editing without saving first.
            if (EditorSceneManager.GetSceneByPath(ScenePath).isLoaded
                && EditorSceneManager.GetSceneByPath(ScenePath).isDirty)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetSceneByPath(ScenePath));
            }

            EnsureFolder("Assets/Project/Resources");
            EnsureFolder(OutputFolder);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject environment = GameObject.Find("EnvironmentDependency");
            if (environment == null)
            {
                // Already migrated — still ensure RoomData points at the prefab if present.
                GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
                if (existingPrefab != null)
                {
                    WireRoomData(existingPrefab);
                    EnsureHost(AssetDatabase.LoadAssetAtPath<RoomData>(RoomDataPath));
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[CombatRoomLayoutMigrator] EnvironmentDependency already gone. Ensured host + RoomData wiring.");
                    return true;
                }

                Debug.LogError("[CombatRoomLayoutMigrator] EnvironmentDependency not found and no layout prefab exists.");
                return false;
            }

            GameObject waveManager = GameObject.Find("RoomWaveManager");
            GameObject background = GameObject.Find("BackGround");

            // Prefer spawn points from spawnSlots, then legacy wave entries.
            Transform spawn0 = null, spawn1 = null, spawn2 = null;
            if (waveManager != null)
            {
                SerializedObject waveSo = new SerializedObject(waveManager.GetComponent<RoomWaveManager>());
                SerializedProperty slots = waveSo.FindProperty("spawnSlots");
                if (slots != null && slots.arraySize > 0)
                {
                    if (slots.arraySize > 0)
                        spawn0 = slots.GetArrayElementAtIndex(0).objectReferenceValue as Transform;
                    if (slots.arraySize > 1)
                        spawn1 = slots.GetArrayElementAtIndex(1).objectReferenceValue as Transform;
                    if (slots.arraySize > 2)
                        spawn2 = slots.GetArrayElementAtIndex(2).objectReferenceValue as Transform;
                }

                SerializedProperty waves = waveSo.FindProperty("waves");
                if (waves != null && waves.arraySize > 0)
                {
                    SerializedProperty enemies = waves.GetArrayElementAtIndex(0).FindPropertyRelative("enemies");
                    if (enemies != null)
                    {
                        if (spawn0 == null && enemies.arraySize > 0)
                            spawn0 = enemies.GetArrayElementAtIndex(0).FindPropertyRelative("spawnPoint").objectReferenceValue as Transform;
                        if (spawn1 == null && enemies.arraySize > 1)
                            spawn1 = enemies.GetArrayElementAtIndex(1).FindPropertyRelative("spawnPoint").objectReferenceValue as Transform;
                        if (spawn2 == null && enemies.arraySize > 2)
                            spawn2 = enemies.GetArrayElementAtIndex(2).FindPropertyRelative("spawnPoint").objectReferenceValue as Transform;
                    }
                }
            }

            if (spawn0 == null) spawn0 = GameObject.Find("GameObject")?.transform;
            if (spawn1 == null) spawn1 = GameObject.Find("GameObject (1)")?.transform;
            if (spawn2 == null) spawn2 = GameObject.Find("GameObject (2)")?.transform;

            GameObject layoutRoot = new GameObject("RoomLayout_Combat_01");

            GameObject combatFolder = new GameObject("Combat");
            combatFolder.transform.SetParent(layoutRoot.transform, false);

            GameObject spawnsFolder = new GameObject("Spawns");
            spawnsFolder.transform.SetParent(combatFolder.transform, false);

            Reparent(environment, layoutRoot.transform);
            if (background != null)
                Reparent(background, layoutRoot.transform);

            if (waveManager != null)
                Reparent(waveManager, combatFolder.transform);

            if (spawn0 != null)
            {
                Reparent(spawn0.gameObject, spawnsFolder.transform);
                spawn0.name = "Spawn_0";
            }

            if (spawn1 != null)
            {
                Reparent(spawn1.gameObject, spawnsFolder.transform);
                spawn1.name = "Spawn_1";
            }

            if (spawn2 != null)
            {
                Reparent(spawn2.gameObject, spawnsFolder.transform);
                spawn2.name = "Spawn_2";
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) != null)
                AssetDatabase.DeleteAsset(OutputPrefabPath);

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(layoutRoot, OutputPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("[CombatRoomLayoutMigrator] Failed to save layout prefab.");
                return false;
            }

            Object.DestroyImmediate(layoutRoot);

            RoomData combatRoom = AssetDatabase.LoadAssetAtPath<RoomData>(RoomDataPath);
            EnsureHost(combatRoom);
            WireRoomData(prefabAsset);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[CombatRoomLayoutMigrator] Done. Prefab={OutputPrefabPath}, sceneSaved={saved}.");

            if (interactive)
            {
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
                EditorUtility.DisplayDialog(
                    "Migrate combat_1",
                    "Migration complete.\n\nCreated RoomLayout_Combat_01 and wired RoomData_Combat_01.\ncombat_1 is now a shell with RoomLayoutHost.",
                    "OK");
            }

            return saved;
        }

        private static void EnsureHost(RoomData bootstrapRoom)
        {
            RoomLayoutHost existingHost = Object.FindFirstObjectByType<RoomLayoutHost>();
            if (existingHost != null)
                Object.DestroyImmediate(existingHost.gameObject);

            GameObject hostGo = new GameObject("RoomLayoutHost");
            Transform mount = new GameObject("LayoutRoot").transform;
            mount.SetParent(hostGo.transform, false);

            RoomLayoutHost host = hostGo.AddComponent<RoomLayoutHost>();
            SerializedObject hostSo = new SerializedObject(host);
            hostSo.FindProperty("layoutRoot").objectReferenceValue = mount;
            hostSo.FindProperty("bootstrapRoom").objectReferenceValue = bootstrapRoom;
            hostSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireRoomData(GameObject layoutPrefab)
        {
            RoomData combatRoom = AssetDatabase.LoadAssetAtPath<RoomData>(RoomDataPath);
            if (combatRoom == null)
            {
                Debug.LogWarning($"[CombatRoomLayoutMigrator] Missing RoomData at {RoomDataPath}");
                return;
            }

            SerializedObject roomSo = new SerializedObject(combatRoom);
            roomSo.FindProperty("layoutPrefab").objectReferenceValue = layoutPrefab;
            roomSo.FindProperty("sceneName").stringValue = "combat_1";
            roomSo.FindProperty("roomId").stringValue = "Combat_01";
            roomSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combatRoom);
        }

        private static void Reparent(GameObject go, Transform parent)
        {
            if (go == null || parent == null)
                return;

            Undo.SetTransformParent(go.transform, parent, "Reparent into RoomLayout");
            go.transform.SetParent(parent, true);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
