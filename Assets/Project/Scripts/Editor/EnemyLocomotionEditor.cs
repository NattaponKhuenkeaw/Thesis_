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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groundLayers"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ledgeProbeDistance"));

        SerializedProperty allowNonGroundMove = serializedObject.FindProperty("allowNonGroundMove");
        EditorGUILayout.PropertyField(allowNonGroundMove);

        if (allowNonGroundMove.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("moveMode"));
            if (serializedObject.FindProperty("moveMode").enumValueIndex == (int)EnemyMoveMode.Fly)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fixedHeightFlight"));
                if (serializedObject.FindProperty("fixedHeightFlight").boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("flightHeight"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("flightPatrolSpan"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("flightPatrolSpeed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("flightGroundTolerance"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("flightGroundSearchDistance"));
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Move mode is Ground. Ground enemies only attack targets on the same platform. Enable Allow Non Ground Move for Fly/Warp kits.",
                MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
