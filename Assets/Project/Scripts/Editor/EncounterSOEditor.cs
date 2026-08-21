#if UNITY_EDITOR
using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    [CustomEditor(typeof(EncounterSO))]
    public sealed class EncounterSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Combat rooms: add types + Weight, then pick Shuffle Bag or Weighted Random.\n" +
                "How many enemies spawn is set on each room (Enemy Count), not here.",
                MessageType.Info);

            Draw("typePickMode", "Type Pick");
            SerializedProperty types = serializedObject.FindProperty("enemyTypes");
            if (types != null)
            {
                EditorGUILayout.PropertyField(types, new GUIContent("Enemy Types"), true);
            }

            EditorGUILayout.Space(6f);
            Draw("aliveCap", "Alive Cap");
            Draw("spawnInterval", "Spawn Interval");

            SerializedProperty spawnMode = serializedObject.FindProperty("spawnMode");
            if (spawnMode != null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.PropertyField(spawnMode, new GUIContent("Spawn Mode"));
                if (spawnMode.enumValueIndex == (int)EncounterSpawnMode.Burst)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("waves"), true);
                    Draw("delayBetweenWaves", "Delay Between Waves");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string fieldName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
        }
    }
}
#endif
