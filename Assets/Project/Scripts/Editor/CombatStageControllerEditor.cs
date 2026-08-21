#if UNITY_EDITOR
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    [CustomEditor(typeof(CombatStageController))]
    public sealed class CombatStageControllerEditor : Editor
    {
        private bool[] chamberOpen;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Tune this component, not Scene Boundary Controller above.\n" +
                "Each room: assign Encounter (types + weights) + Enemy Count.\n" +
                "Turn on Scene View Gizmos to see room bounds.",
                MessageType.Info);

            SerializedProperty chambersProp = serializedObject.FindProperty("chambers");
            if (chambersProp == null)
            {
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            int newSize = EditorGUILayout.IntField("Room Count", chambersProp.arraySize);
            if (newSize < 0)
            {
                newSize = 0;
            }

            if (newSize != chambersProp.arraySize)
            {
                chambersProp.arraySize = newSize;
            }

            EnsureFoldouts(chambersProp.arraySize);

            for (int i = 0; i < chambersProp.arraySize; i++)
            {
                SerializedProperty chamber = chambersProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = chamber.FindPropertyRelative("name");
                SerializedProperty encounterProp = chamber.FindPropertyRelative("encounter");
                SerializedProperty countProp = chamber.FindPropertyRelative("enemyCount")
                    ?? chamber.FindPropertyRelative("enemyQuota");

                string roomName = nameProp != null ? nameProp.stringValue : string.Empty;
                string title = string.IsNullOrWhiteSpace(roomName)
                    ? $"Room {i + 1}"
                    : $"Room {i + 1}: {roomName}";
                if (encounterProp != null
                    && encounterProp.objectReferenceValue == null
                    && countProp != null
                    && countProp.intValue > 0)
                {
                    title += "  — no Encounter";
                }

                EditorGUILayout.Space(8f);
                chamberOpen[i] = EditorGUILayout.BeginFoldoutHeaderGroup(chamberOpen[i], title);
                if (chamberOpen[i])
                {
                    EditorGUI.indentLevel++;
                    DrawRelative(chamber, "name", "Name");
                    DrawRelative(chamber, "encounter", "Encounter");
                    if (countProp != null)
                    {
                        EditorGUILayout.PropertyField(countProp, new GUIContent("Enemy Count"));
                    }

                    EditorGUILayout.Space(4f);
                    DrawRelative(chamber, "entranceWall", "Entrance Wall");
                    DrawRelative(chamber, "exitWall", "Exit Wall");
                    DrawRelative(chamber, "enterTrigger", "Enter Trigger");
                    DrawRelative(chamber, "spawnRoot", "Spawn Root");
                    DrawRelative(chamber, "spawnSlots", "Spawn Slots");
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            EditorGUILayout.Space(10f);
            DrawRoot("corridorPadding");
            DrawRoot("drawSceneGuides");
            DrawRoot("gizmoHeight");

            serializedObject.ApplyModifiedProperties();

            var stage = (CombatStageController)target;
            EditorGUILayout.Space(8f);
            DrawChamberStatus(stage);

            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void DrawRoot(string fieldName)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }

        private static void DrawRelative(SerializedProperty chamber, string fieldName, string label)
        {
            SerializedProperty property = chamber.FindPropertyRelative(fieldName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
        }

        private void EnsureFoldouts(int count)
        {
            if (chamberOpen != null && chamberOpen.Length == count)
            {
                return;
            }

            var next = new bool[count];
            for (int i = 0; i < count; i++)
            {
                next[i] = true;
            }

            chamberOpen = next;
        }

        private void DrawChamberStatus(CombatStageController stage)
        {
            if (stage == null || stage.Chambers == null || stage.Chambers.Count == 0)
            {
                EditorGUILayout.HelpBox("Room Count is 0. Set it above, then assign an Encounter per room.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            for (int i = 0; i < stage.Chambers.Count; i++)
            {
                CombatStageController.Chamber chamber = stage.Chambers[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Room {i + 1}", GUILayout.Width(56f));
                EditorGUILayout.LabelField(DescribeChamber(chamber), EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Focus", GUILayout.Width(56f)))
                {
                    FrameChamber(stage, chamber);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private static string DescribeChamber(CombatStageController.Chamber chamber)
        {
            if (chamber == null)
            {
                return "empty";
            }

            if (chamber.encounter == null && chamber.enemyCount > 0)
            {
                return "assign Encounter";
            }

            if (chamber.entranceWall == null || chamber.exitWall == null)
            {
                return "missing left/right wall";
            }

            if (!HasSpawn(chamber))
            {
                return "missing Spawn Root / Spawn_0";
            }

            if (chamber.enemyCount <= 0)
            {
                return "ready · 0 enemies (no fight)";
            }

            if (chamber.encounter == null)
            {
                return $"ready · {chamber.enemyCount} · assign Encounter";
            }

            return $"ready · {chamber.enemyCount} · {chamber.encounter.name}";
        }

        private static bool HasSpawn(CombatStageController.Chamber chamber)
        {
            if (chamber.spawnSlots != null)
            {
                for (int i = 0; i < chamber.spawnSlots.Length; i++)
                {
                    if (chamber.spawnSlots[i] != null)
                    {
                        return true;
                    }
                }
            }

            return chamber.spawnRoot != null;
        }

        private static void FrameChamber(CombatStageController stage, CombatStageController.Chamber chamber)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || chamber == null)
            {
                return;
            }

            if (RoomBoundaryMath.TryResolveLockedX(chamber.entranceWall, chamber.exitWall, null, out float left, out float right))
            {
                float midY = GuideMidY(chamber, stage);
                float height = GuideHeight(chamber);
                var bounds = new Bounds(
                    new Vector3((left + right) * 0.5f, midY, 0f),
                    new Vector3(Mathf.Max(2f, right - left) + 2f, height + 2f, 2f));
                view.Frame(bounds, false);
                view.Repaint();
                return;
            }

            Transform ping = chamber.entranceWall != null
                ? chamber.entranceWall.transform
                : chamber.exitWall != null
                    ? chamber.exitWall.transform
                    : stage.transform;
            Selection.activeTransform = ping;
            view.FrameSelected();
        }

        private void OnSceneGUI()
        {
            var stage = (CombatStageController)target;
            if (stage == null || !stage.DrawSceneGuides || stage.Chambers == null)
            {
                return;
            }

            for (int i = 0; i < stage.Chambers.Count; i++)
            {
                DrawWallHandles(stage, stage.Chambers[i]);
            }
        }

        private static void DrawWallHandles(CombatStageController stage, CombatStageController.Chamber chamber)
        {
            if (chamber == null
                || !RoomBoundaryMath.TryResolveLockedX(
                    chamber.entranceWall,
                    chamber.exitWall,
                    null,
                    out float left,
                    out float right))
            {
                return;
            }

            float midY = GuideMidY(chamber, stage);
            Handles.color = new Color(1f, 1f, 1f, 0.95f);

            if (chamber.entranceWall != null)
            {
                MoveWallEdge(chamber.entranceWall, new Vector3(left, midY, 0f), useMaximumEdge: true);
            }

            if (chamber.exitWall != null)
            {
                MoveWallEdge(chamber.exitWall, new Vector3(right, midY, 0f), useMaximumEdge: false);
            }
        }

        private static float GuideMidY(CombatStageController.Chamber chamber, CombatStageController stage)
        {
            if (chamber != null && chamber.entranceWall != null)
            {
                return chamber.entranceWall.bounds.center.y;
            }

            if (chamber != null && chamber.exitWall != null)
            {
                return chamber.exitWall.bounds.center.y;
            }

            return stage != null ? stage.transform.position.y : 0f;
        }

        private static float GuideHeight(CombatStageController.Chamber chamber)
        {
            float height = 8f;
            if (chamber != null && chamber.entranceWall != null)
            {
                height = Mathf.Max(height, chamber.entranceWall.bounds.size.y);
            }

            if (chamber != null && chamber.exitWall != null)
            {
                height = Mathf.Max(height, chamber.exitWall.bounds.size.y);
            }

            return height;
        }

        private static void MoveWallEdge(Collider2D wall, Vector3 handlePos, bool useMaximumEdge)
        {
            float size = HandleUtility.GetHandleSize(handlePos) * 0.18f;
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.Slider(
                handlePos,
                Vector3.right,
                size,
                Handles.CubeHandleCap,
                0.05f);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(wall.transform, "Move combat chamber wall");
            float currentEdge = useMaximumEdge ? wall.bounds.max.x : wall.bounds.min.x;
            Vector3 position = wall.transform.position;
            position.x += next.x - currentEdge;
            wall.transform.position = position;
            EditorUtility.SetDirty(wall.transform);
        }
    }
}
#endif
