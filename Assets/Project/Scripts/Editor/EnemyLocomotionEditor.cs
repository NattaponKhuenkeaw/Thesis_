using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyLocomotion))]
public sealed class EnemyLocomotionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackSlotOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("arrivalThreshold"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("faceTargetOnMove"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("samePlatformMaxY"));

        SerializedProperty allowNonGroundMove = serializedObject.FindProperty("allowNonGroundMove");
        EditorGUILayout.PropertyField(allowNonGroundMove);

        if (allowNonGroundMove.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("moveMode"));
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Move mode is Ground. Enemy1–3 stay on the ground and only attack targets on the same platform. Enable Allow Non Ground Move only for Fly/Warp kits.",
                MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
