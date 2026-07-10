using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RuntimeEffectStatModifier))]
public class RuntimeEffectStatModifierDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = NextLine(ref position);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        SerializedProperty target = property.FindPropertyRelative("target");
        SerializedProperty operation = property.FindPropertyRelative("operation");
        SerializedProperty sourceStat = property.FindPropertyRelative("sourceStat");
        SerializedProperty statRatioValueSource = property.FindPropertyRelative("statRatioValueSource");
        SerializedProperty value = property.FindPropertyRelative("value");

        EditorGUI.PropertyField(NextLine(ref position), target);
        EditorGUI.PropertyField(NextLine(ref position), operation);

        if (UsesRuntimeSourceStat(operation))
        {
            EditorGUI.PropertyField(NextLine(ref position), sourceStat);
            EditorGUI.PropertyField(NextLine(ref position), statRatioValueSource, new GUIContent("Ratio Value Source"));
        }

        EditorGUI.PropertyField(NextLine(ref position), value);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        SerializedProperty operation = property.FindPropertyRelative("operation");
        int lineCount = UsesRuntimeSourceStat(operation) ? 6 : 4;
        return lineCount * EditorGUIUtility.singleLineHeight
            + (lineCount - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    private static bool UsesRuntimeSourceStat(SerializedProperty operation)
    {
        RuntimeEffectStatOperation resolved = (RuntimeEffectStatOperation)operation.enumValueIndex;
        return resolved == RuntimeEffectStatOperation.AddSourceStatRatio
            || resolved == RuntimeEffectStatOperation.AddTargetStatRatio;
    }

    private static Rect NextLine(ref Rect position)
    {
        Rect line = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return line;
    }
}

[CustomPropertyDrawer(typeof(PassiveStatModifierDefinition))]
public class PassiveStatModifierDefinitionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = NextLine(ref position);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        SerializedProperty target = property.FindPropertyRelative("target");
        SerializedProperty operation = property.FindPropertyRelative("operation");
        SerializedProperty valueSource = property.FindPropertyRelative("valueSource");
        SerializedProperty value = property.FindPropertyRelative("value");
        SerializedProperty sourceStat = property.FindPropertyRelative("sourceStat");
        SerializedProperty statRatioValueSource = property.FindPropertyRelative("statRatioValueSource");
        SerializedProperty scope = property.FindPropertyRelative("scope");
        SerializedProperty requiredTags = property.FindPropertyRelative("requiredTags");
        SerializedProperty requiredCounters = property.FindPropertyRelative("requiredCounters");

        EditorGUI.PropertyField(NextLine(ref position), target);
        EditorGUI.PropertyField(NextLine(ref position), operation);
        EditorGUI.PropertyField(NextLine(ref position), valueSource);

        if (UsesPassiveSourceStat(valueSource))
        {
            EditorGUI.PropertyField(NextLine(ref position), sourceStat);
            EditorGUI.PropertyField(NextLine(ref position), statRatioValueSource, new GUIContent("Ratio Value Source"));
        }

        EditorGUI.PropertyField(NextLine(ref position), value);
        EditorGUI.PropertyField(NextLine(ref position), scope);

        line = NextPropertyRect(ref position, requiredTags);
        EditorGUI.PropertyField(line, requiredTags, true);

        line = NextPropertyRect(ref position, requiredCounters);
        EditorGUI.PropertyField(line, requiredCounters, true);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        SerializedProperty valueSource = property.FindPropertyRelative("valueSource");
        SerializedProperty requiredTags = property.FindPropertyRelative("requiredTags");
        SerializedProperty requiredCounters = property.FindPropertyRelative("requiredCounters");

        int lineCount = UsesPassiveSourceStat(valueSource) ? 8 : 6;
        float height = lineCount * EditorGUIUtility.singleLineHeight
            + (lineCount - 1) * EditorGUIUtility.standardVerticalSpacing;

        height += EditorGUI.GetPropertyHeight(requiredTags, true) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(requiredCounters, true) + EditorGUIUtility.standardVerticalSpacing;
        return height;
    }

    private static bool UsesPassiveSourceStat(SerializedProperty valueSource)
    {
        return (PassiveModifierValueSource)valueSource.enumValueIndex == PassiveModifierValueSource.SourceStatRatio;
    }

    private static Rect NextLine(ref Rect position)
    {
        Rect line = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return line;
    }

    private static Rect NextPropertyRect(ref Rect position, SerializedProperty property)
    {
        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect line = new Rect(position.x, position.y, position.width, height);
        position.y += height + EditorGUIUtility.standardVerticalSpacing;
        return line;
    }
}
