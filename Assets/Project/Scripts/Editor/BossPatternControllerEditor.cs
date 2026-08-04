#if UNITY_EDITOR
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Edit-mode hitbox preview — uses the same local offset/size formula as runtime ConfigureHitbox.
    /// </summary>
    [CustomEditor(typeof(BossPatternController))]
    public sealed class BossPatternControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Preview ใช้สูตรเดียวกับตอน Play (offset บน collider local)\n" +
                "เปิดทีละท่าด้วย Hitbox Preview Mode กันซ้อน\n" +
                "ส้ม = Cleave · แดง = Jump Smash · ม่วง = Charge",
                MessageType.Info);

            SerializedProperty previewMode = serializedObject.FindProperty("hitboxPreviewMode");
            SerializedProperty previewRanges = serializedObject.FindProperty("previewRanges");

            EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(previewMode, new GUIContent("Hitbox Preview Mode"));
            EditorGUILayout.PropertyField(previewRanges, new GUIContent("Preview Ranges"));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cleave"))
                previewMode.enumValueIndex = (int)BossHitboxPreviewMode.CleaveOnly;
            if (GUILayout.Button("Jump"))
                previewMode.enumValueIndex = (int)BossHitboxPreviewMode.JumpSmashOnly;
            if (GUILayout.Button("Charge"))
                previewMode.enumValueIndex = (int)BossHitboxPreviewMode.BullChargeOnly;
            if (GUILayout.Button("All"))
                previewMode.enumValueIndex = (int)BossHitboxPreviewMode.All;
            if (GUILayout.Button("None"))
                previewMode.enumValueIndex = (int)BossHitboxPreviewMode.None;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            DrawPropertiesExcluding(serializedObject, "m_Script", "hitboxPreviewMode", "previewRanges");

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            var boss = (BossPatternController)target;
            if (boss == null)
                return;

            SerializedObject so = serializedObject;
            so.Update();

            EnemyController enemy = boss.GetComponent<EnemyController>();
            Collider2D fallback = enemy != null ? enemy.attackHitbox : null;
            BossHitboxPreviewMode mode = (BossHitboxPreviewMode)so.FindProperty("hitboxPreviewMode").enumValueIndex;
            bool ranges = so.FindProperty("previewRanges").boolValue;

            if (ranges)
            {
                Handles.color = new Color(1f, 0.55f, 0.1f, 0.9f);
                Handles.DrawWireDisc(boss.transform.position, Vector3.forward, so.FindProperty("cleaveRange").floatValue);
                Handles.Label(boss.transform.position + Vector3.up * 0.4f, "Cleave Range");

                Handles.color = new Color(0.85f, 0.2f, 0.85f, 0.9f);
                Handles.DrawWireDisc(boss.transform.position, Vector3.forward, so.FindProperty("chargeDistance").floatValue);
            }

            if (mode == BossHitboxPreviewMode.CleaveOnly || mode == BossHitboxPreviewMode.All)
                DrawPatternHitbox(boss, so.FindProperty("cleave"), fallback, new Color(1f, 0.45f, 0.05f, 0.9f), "Cleave");

            if (mode == BossHitboxPreviewMode.JumpSmashOnly || mode == BossHitboxPreviewMode.All)
                DrawPatternHitbox(boss, so.FindProperty("jumpSmash"), fallback, new Color(1f, 0.15f, 0.15f, 0.9f), "JumpSmash");

            if (mode == BossHitboxPreviewMode.BullChargeOnly || mode == BossHitboxPreviewMode.All)
                DrawPatternHitbox(boss, so.FindProperty("bullCharge"), fallback, new Color(0.75f, 0.25f, 1f, 0.9f), "Charge");
        }

        private static void DrawPatternHitbox(
            BossPatternController boss,
            SerializedProperty timingProp,
            Collider2D fallback,
            Color color,
            string label)
        {
            if (timingProp == null)
                return;

            Collider2D overrideHitbox = timingProp.FindPropertyRelative("hitbox").objectReferenceValue as Collider2D;
            Collider2D source = overrideHitbox != null ? overrideHitbox : fallback;
            if (source == null)
                return;

            // Build a temporary timing snapshot matching runtime ConfigureHitbox.
            var timing = new BossPatternTiming
            {
                hitbox = overrideHitbox,
                hitboxLocalOffset = timingProp.FindPropertyRelative("hitboxLocalOffset").vector2Value,
                hitboxSizeOverride = timingProp.FindPropertyRelative("hitboxSizeOverride").vector2Value
            };

            if (!BossPatternController.TryGetPatternHitboxLocal(timing, source, out Vector2 localCenter, out Vector2 localSize))
                return;

            Transform t = source.transform;
            Vector3 worldCenter = t.TransformPoint(localCenter);
            // lossyScale included — matches collider world bounds under boss scale 1.75
            Vector3 axisX = t.TransformVector(new Vector3(localSize.x, 0f, 0f));
            Vector3 axisY = t.TransformVector(new Vector3(0f, localSize.y, 0f));
            Vector3 halfX = axisX * 0.5f;
            Vector3 halfY = axisY * 0.5f;

            Vector3 bl = worldCenter - halfX - halfY;
            Vector3 br = worldCenter + halfX - halfY;
            Vector3 tr = worldCenter + halfX + halfY;
            Vector3 tl = worldCenter - halfX + halfY;

            Handles.color = color;
            Handles.DrawSolidRectangleWithOutline(
                new[] { bl, br, tr, tl },
                new Color(color.r, color.g, color.b, 0.12f),
                color);
            Handles.Label(tr + Vector3.up * 0.15f, $"{label}  {localSize.x:0.##}x{localSize.y:0.##}");

            EditorGUI.BeginChangeCheck();
            Vector3 newHandle = Handles.PositionHandle(worldCenter, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(boss, $"Move {label} Hitbox Offset");
                Vector2 baseOffset = source is BoxCollider2D box
                    ? box.offset
                    : source is CapsuleCollider2D capsule ? capsule.offset : Vector2.zero;
                Vector3 local = t.InverseTransformPoint(newHandle);
                // Match runtime: offset = baseOffset + hitboxLocalOffset (no extra facing multiply)
                timingProp.FindPropertyRelative("hitboxLocalOffset").vector2Value =
                    new Vector2(local.x - baseOffset.x, local.y - baseOffset.y);
                timingProp.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(boss);
            }
        }
    }
}
#endif
