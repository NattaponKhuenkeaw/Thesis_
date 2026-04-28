using CombatEditor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProjectileData))]
public class ProjectileDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(8f);

        if (GUILayout.Button("Open Projectile Editor", GUILayout.Height(28f)))
        {
            ProjectileEditorWindow.OpenWindow((ProjectileData)target);
        }
    }
}
