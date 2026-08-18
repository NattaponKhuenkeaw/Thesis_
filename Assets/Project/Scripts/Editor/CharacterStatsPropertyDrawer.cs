using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CharacterStats))]
public sealed class CharacterStatsPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < CombatStatCatalog.BuildableIds.Count; i++)
            {
                CombatStatMetadata metadata = CombatStatCatalog.Get(CombatStatCatalog.BuildableIds[i]);
                SerializedProperty statProperty = property.FindPropertyRelative(metadata.SerializedFieldName);
                if (statProperty == null)
                {
                    continue;
                }

                GUIContent statLabel = new GUIContent(metadata.Label);
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = EditorGUI.GetPropertyHeight(statProperty, statLabel, true);
                EditorGUI.PropertyField(row, statProperty, statLabel, true);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        for (int i = 0; i < CombatStatCatalog.BuildableIds.Count; i++)
        {
            CombatStatMetadata metadata = CombatStatCatalog.Get(CombatStatCatalog.BuildableIds[i]);
            SerializedProperty statProperty = property.FindPropertyRelative(metadata.SerializedFieldName);
            if (statProperty == null)
            {
                continue;
            }

            GUIContent statLabel = new GUIContent(metadata.Label);
            height += EditorGUIUtility.standardVerticalSpacing
                + EditorGUI.GetPropertyHeight(statProperty, statLabel, true);
        }

        return height;
    }
}
