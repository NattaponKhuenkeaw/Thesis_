#if UNITY_EDITOR
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEngine;
using Unity.Behavior;

namespace Project.EditorTools
{
    /// <summary>
    /// One-click bootstrap for the Executioner boss MVP prefab.
    /// </summary>
    public static class BossExecutionerPrefabFactory
    {
        private const string EnemyCorePath = "Assets/Project/TestChar/Prefab/EnemyCore.prefab";
        private const string OutputPath = "Assets/Project/TestChar/Prefab/Boss_Executioner.prefab";

        [MenuItem("Project/Boss/Create Executioner Prefab From EnemyCore")]
        public static void CreateExecutionerPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCorePath);
            if (source == null)
            {
                Debug.LogError($"[BossExecutionerPrefabFactory] Missing source prefab at {EnemyCorePath}");
                return;
            }

            GameObject instance = Object.Instantiate(source);
            instance.name = "Boss_Executioner";

            // Scale up body for "big hitbox" silhouette.
            instance.transform.localScale = new Vector3(1.75f, 1.75f, 1f);

            EnemyController enemy = instance.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.maxHP = 800f;
                enemy.currentHP = 800f;
                enemy.attackDamage = 150f;
                enemy.defense = 500f;
                enemy.isEliteOrBoss = true;

                if (enemy.bodyCollider is BoxCollider2D bodyBox)
                {
                    bodyBox.size = new Vector2(
                        Mathf.Max(bodyBox.size.x, 1.4f),
                        Mathf.Max(bodyBox.size.y, 2.2f));
                }

                if (enemy.attackHitbox is BoxCollider2D attackBox)
                {
                    attackBox.size = new Vector2(2.6f, 1.4f);
                    attackBox.offset = new Vector2(1.4f, 0.2f);
                }
            }

            BehaviorGraphAgent agent = instance.GetComponent<BehaviorGraphAgent>();
            if (agent != null)
                agent.enabled = false;

            EnemyWorldUI worldUi = instance.GetComponentInChildren<EnemyWorldUI>(true);
            if (worldUi != null)
            {
                var worldUiSo = new SerializedObject(worldUi);
                SerializedProperty showHealthBar = worldUiSo.FindProperty("showHealthBar");
                if (showHealthBar != null)
                    showHealthBar.boolValue = false;
                worldUiSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (instance.GetComponent<BossPatternController>() == null)
                instance.AddComponent<BossPatternController>();

            Transform existingMarker = instance.transform.Find("GroundTelegraph");
            GameObject markerGo = existingMarker != null ? existingMarker.gameObject : new GameObject("GroundTelegraph");
            if (existingMarker == null)
            {
                markerGo.transform.SetParent(instance.transform, false);
                markerGo.transform.localPosition = Vector3.zero;
            }

            SpriteRenderer markerSr = markerGo.GetComponent<SpriteRenderer>();
            if (markerSr == null)
                markerSr = markerGo.AddComponent<SpriteRenderer>();
            markerSr.color = new Color(0.85f, 0.15f, 0.1f, 0.45f);
            markerSr.sortingOrder = 20;
            markerSr.enabled = false;

            BossGroundTelegraph telegraph = markerGo.GetComponent<BossGroundTelegraph>();
            if (telegraph == null)
                telegraph = markerGo.AddComponent<BossGroundTelegraph>();

            var telegraphSo = new SerializedObject(telegraph);
            telegraphSo.FindProperty("markerRenderer").objectReferenceValue = markerSr;
            telegraphSo.ApplyModifiedPropertiesWithoutUndo();

            BossPatternController patternController = instance.GetComponent<BossPatternController>();
            var patternSo = new SerializedObject(patternController);
            patternSo.FindProperty("enemy").objectReferenceValue = enemy;
            patternSo.FindProperty("animHandler").objectReferenceValue = instance.GetComponentInChildren<AnimationHandler_Enemy>(true);
            patternSo.FindProperty("rb").objectReferenceValue = instance.GetComponent<Rigidbody2D>();
            patternSo.FindProperty("groundTelegraph").objectReferenceValue = telegraph;
            patternSo.FindProperty("useDefaultStats").boolValue = true;
            patternSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(instance, OutputPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject created = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            Debug.Log($"[BossExecutionerPrefabFactory] Created {OutputPath}. Assign it to EncounterSO spawn entries (via RoomData.Encounter).");
        }
    }
}
#endif
