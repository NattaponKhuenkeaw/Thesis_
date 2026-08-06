#if UNITY_EDITOR
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Builds a boss-room layout prefab compatible with RoomLayoutHost + RoomWaveManager.
    /// </summary>
    public static class BossRoomLayoutFactory
    {
        private const string OutputFolder = "Assets/Project/Resources/RoomLayouts";
        private const string OutputPath = OutputFolder + "/RoomLayout_Boss_Executioner.prefab";
        private const string BossPrefabPath = "Assets/Project/TestChar/Prefab/Boss_Executioner.prefab";
        private const string RoomDataPath = "Assets/Project/Resources/MapSO/RoomData_Boss_01.asset";
        private const string EncounterFolder = "Assets/Project/Resources/EncounterSO";
        private const string EncounterAssetPath = EncounterFolder + "/Encounter_Boss_Executioner.asset";

        // Arena tuned for Jump Smash / Bull Charge (wider than trash combat).
        private const float GroundY = -4f;
        private const float FloorTopY = -3.5f;
        private const float ArenaHalfWidth = 20f;

        [MenuItem("Project/Boss/Create Executioner Boss Room Layout")]
        public static void CreateBossExecutionerLayout()
        {
            EnsureFolder("Assets/Project/Resources");
            EnsureFolder(OutputFolder);

            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null)
            {
                Debug.LogWarning(
                    $"[BossRoomLayoutFactory] '{BossPrefabPath}' missing. Creating layout with empty boss spawn; assign prefab later.");
            }

            GameObject root = new GameObject("RoomLayout_Boss_Executioner");

            // ---- Environment ----
            GameObject environment = CreateChild(root, "EnvironmentDependency");
            SceneBoundaryController boundary = environment.AddComponent<SceneBoundaryController>();

            GameObject ground = CreateChild(environment, "Ground");
            ground.layer = LayerMask.NameToLayer("Ground");
            ground.transform.localPosition = new Vector3(0f, GroundY, 0f);
            ground.transform.localScale = new Vector3(ArenaHalfWidth * 2f + 4f, 1f, 1f);
            BoxCollider2D groundCol = ground.AddComponent<BoxCollider2D>();
            groundCol.size = Vector2.one;
            SpriteRenderer groundSr = ground.AddComponent<SpriteRenderer>();
            groundSr.color = new Color(0.18f, 0.14f, 0.16f, 1f);
            groundSr.sprite = LoadPlaceholderSprite();
            groundSr.drawMode = SpriteDrawMode.Sliced;
            groundSr.size = Vector2.one;
            groundSr.sortingOrder = -20;

            GameObject startWall = CreateWall(environment, "StartWall", -ArenaHalfWidth - 1.5f, 8f, 1.5f, 16f);
            GameObject exitWall = CreateWall(environment, "ExitWall", ArenaHalfWidth + 1.5f, 8f, 1.5f, 16f);

            GameObject entrance = CreateChild(environment, "Entrance");
            entrance.transform.localPosition = new Vector3(-ArenaHalfWidth + 2f, FloorTopY, 0f);
            EntrancePoint entrancePoint = entrance.AddComponent<EntrancePoint>();
            GameObject stopPoint = CreateChild(entrance, "Stoppoint");
            stopPoint.transform.localPosition = new Vector3(3.5f, 0f, 0f);

            SerializedObject entranceSo = new SerializedObject(entrancePoint);
            entranceSo.FindProperty("entranceId").stringValue = "Default";
            entranceSo.FindProperty("stopPoint").objectReferenceValue = stopPoint.transform;
            entranceSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject portalsRoot = CreateChild(environment, "PortalsRoot");
            portalsRoot.SetActive(false);

            GameObject exit = CreateChild(portalsRoot, "Exit");
            exit.transform.localPosition = new Vector3(ArenaHalfWidth - 1f, FloorTopY + 1.5f, 0f);
            BoxCollider2D exitCol = exit.AddComponent<BoxCollider2D>();
            exitCol.isTrigger = true;
            exitCol.size = new Vector2(2.5f, 6f);
            TransitionTrigger exitTrigger = exit.AddComponent<TransitionTrigger>();
            SerializedObject exitSo = new SerializedObject(exitTrigger);
            exitSo.FindProperty("openRoomChoiceUi").boolValue = true;
            exitSo.FindProperty("targetEntranceId").stringValue = "Default";
            exitSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject boundarySo = new SerializedObject(boundary);
            boundarySo.FindProperty("_entranceWall").objectReferenceValue = startWall.GetComponent<Collider2D>();
            boundarySo.FindProperty("_exitWall").objectReferenceValue = exitWall.GetComponent<Collider2D>();
            boundarySo.FindProperty("_exitTrigger").objectReferenceValue = exitTrigger;
            boundarySo.FindProperty("_entrancePadding").floatValue = 0.75f;
            boundarySo.FindProperty("_exitPadding").floatValue = 0.75f;
            boundarySo.FindProperty("_cameraCueDistance").floatValue = 3f;
            boundarySo.FindProperty("_cameraCueDuration").floatValue = 0.65f;
            boundarySo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Combat / waves ----
            GameObject combat = CreateChild(root, "Combat");

            GameObject bossSpawn = CreateChild(combat, "BossSpawnPoint");
            bossSpawn.transform.localPosition = new Vector3(5f, FloorTopY, 0f);

            GameObject encounter = CreateChild(combat, "RoomWaveManager");
            encounter.transform.localPosition = new Vector3(-8f, FloorTopY + 2f, 0f);
            BoxCollider2D encounterCol = encounter.AddComponent<BoxCollider2D>();
            encounterCol.isTrigger = true;
            encounterCol.size = new Vector2(10f, 8f);
            RoomWaveManager waveManager = encounter.AddComponent<RoomWaveManager>();

            SerializedObject waveSo = new SerializedObject(waveManager);
            waveSo.FindProperty("playerTag").stringValue = "Player";
            waveSo.FindProperty("startOnlyOnce").boolValue = true;
            waveSo.FindProperty("delayBetweenWaves").floatValue = 0f;
            waveSo.FindProperty("unlockSceneExitOnClear").boolValue = false;
            waveSo.FindProperty("useBossDeathFlow").boolValue = true;
            waveSo.FindProperty("portalsRoot").objectReferenceValue = portalsRoot;
            waveSo.FindProperty("waves").arraySize = 0;

            SerializedProperty spawnSlots = waveSo.FindProperty("spawnSlots");
            spawnSlots.arraySize = 1;
            spawnSlots.GetArrayElementAtIndex(0).objectReferenceValue = bossSpawn.transform;
            waveSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureBossEncounterAsset(bossPrefab);

            // ---- Visual helpers (optional markers) ----
            GameObject visuals = CreateChild(root, "Visuals");
            GameObject arenaMarker = CreateChild(visuals, "ArenaWidthMarker");
            arenaMarker.transform.localPosition = new Vector3(0f, FloorTopY + 0.05f, 0f);
            SpriteRenderer markerSr = arenaMarker.AddComponent<SpriteRenderer>();
            markerSr.color = new Color(0.55f, 0.12f, 0.18f, 0.35f);
            markerSr.sprite = LoadPlaceholderSprite();
            markerSr.drawMode = SpriteDrawMode.Sliced;
            markerSr.size = Vector2.one;
            arenaMarker.transform.localScale = new Vector3(ArenaHalfWidth * 2f, 0.2f, 1f);
            markerSr.sortingOrder = -19;

            // Save prefab
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath) != null)
            {
                AssetDatabase.DeleteAsset(OutputPath);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            Object.DestroyImmediate(root);

            AssignLayoutToRoomData(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[BossRoomLayoutFactory] Created {OutputPath} and wired RoomData_Boss_01.layoutPrefab.");
        }

        private static void AssignLayoutToRoomData(GameObject layoutPrefab)
        {
            RoomData roomData = AssetDatabase.LoadAssetAtPath<RoomData>(RoomDataPath);
            if (roomData == null)
            {
                Debug.LogWarning($"[BossRoomLayoutFactory] Missing RoomData at {RoomDataPath}");
                return;
            }

            SerializedObject so = new SerializedObject(roomData);
            so.FindProperty("layoutPrefab").objectReferenceValue = layoutPrefab;
            so.FindProperty("roomKind").enumValueIndex = (int)RoomKind.Boss;
            so.FindProperty("displayName").stringValue = "Boss — Executioner";
            so.FindProperty("includeInRandomOffers").boolValue = false;
            so.FindProperty("zoneMode").enumValueIndex = (int)ZoneMode.CombatZone;
            so.FindProperty("defaultEntranceId").stringValue = "Default";
            // Keep combat_1 as shell scene for hybrid swap / legacy fallback.
            if (string.IsNullOrWhiteSpace(so.FindProperty("sceneName").stringValue))
            {
                so.FindProperty("sceneName").stringValue = "combat_1";
            }

            EncounterSO encounter = AssetDatabase.LoadAssetAtPath<EncounterSO>(EncounterAssetPath);
            if (encounter != null)
            {
                so.FindProperty("encounter").objectReferenceValue = encounter;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(roomData);
        }

        private static void EnsureBossEncounterAsset(GameObject bossPrefab)
        {
            EnsureFolder("Assets/Project/Resources");
            EnsureFolder(EncounterFolder);

            EncounterSO encounter = AssetDatabase.LoadAssetAtPath<EncounterSO>(EncounterAssetPath);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<EncounterSO>();
                AssetDatabase.CreateAsset(encounter, EncounterAssetPath);
            }

            SerializedObject so = new SerializedObject(encounter);
            so.FindProperty("delayBetweenWaves").floatValue = 0f;
            SerializedProperty waves = so.FindProperty("waves");
            waves.arraySize = 1;
            SerializedProperty wave0 = waves.GetArrayElementAtIndex(0);
            wave0.FindPropertyRelative("name").stringValue = "Executioner";
            SerializedProperty enemies = wave0.FindPropertyRelative("enemies");
            enemies.arraySize = 1;
            SerializedProperty entry = enemies.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = bossPrefab;
            entry.FindPropertyRelative("spawnSlotIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);
        }

        private static GameObject CreateWall(GameObject parent, string name, float x, float y, float width, float height)
        {
            GameObject wall = CreateChild(parent, name);
            wall.transform.localPosition = new Vector3(x, y, 0f);
            BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
            col.size = new Vector2(width, height);
            SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.25f, 0.22f, 0.28f, 0.85f);
            sr.sprite = LoadPlaceholderSprite();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = Vector2.one;
            wall.transform.localScale = new Vector3(width, height, 1f);
            // Collider size 1x1 under scaled transform keeps physical size = scale.
            col.size = Vector2.one;
            sr.sortingOrder = -10;
            return wall;
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static Sprite LoadPlaceholderSprite()
        {
            // Prefer a project sprite so the prefab reference survives serialization.
            string[] candidates =
            {
                "Assets/Project/TestChar/Sprite",
                "Assets/Project"
            };

            foreach (string folder in candidates)
            {
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
                if (guids == null || guids.Length == 0)
                {
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
