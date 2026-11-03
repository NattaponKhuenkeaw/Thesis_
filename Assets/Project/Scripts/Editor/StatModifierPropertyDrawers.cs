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
        SerializedProperty scaleValueByCounter = property.FindPropertyRelative("scaleValueByCounter");
        SerializedProperty valueCounterId = property.FindPropertyRelative("valueCounterId");
        SerializedProperty maxCounterScale = property.FindPropertyRelative("maxCounterScale");
        SerializedProperty scope = property.FindPropertyRelative("scope");
        SerializedProperty filterOutgoingDamageByCategory = property.FindPropertyRelative("filterOutgoingDamageByCategory");
        SerializedProperty outgoingDamageCategory = property.FindPropertyRelative("outgoingDamageCategory");
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
        EditorGUI.PropertyField(NextLine(ref position), scaleValueByCounter);
        if (scaleValueByCounter.boolValue)
        {
            EditorGUI.PropertyField(NextLine(ref position), valueCounterId);
            EditorGUI.PropertyField(NextLine(ref position), maxCounterScale);
        }
        EditorGUI.PropertyField(NextLine(ref position), scope);

        if (UsesOutgoingDamageFilter(target))
        {
            EditorGUI.PropertyField(NextLine(ref position), filterOutgoingDamageByCategory, new GUIContent("Filter By Category"));
            if (filterOutgoingDamageByCategory.boolValue)
            {
                EditorGUI.PropertyField(NextLine(ref position), outgoingDamageCategory, new GUIContent("Category"));
            }
        }

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
        SerializedProperty target = property.FindPropertyRelative("target");
        SerializedProperty scaleValueByCounter = property.FindPropertyRelative("scaleValueByCounter");
        SerializedProperty filterOutgoingDamageByCategory = property.FindPropertyRelative("filterOutgoingDamageByCategory");
        SerializedProperty requiredTags = property.FindPropertyRelative("requiredTags");
        SerializedProperty requiredCounters = property.FindPropertyRelative("requiredCounters");

        int lineCount = UsesPassiveSourceStat(valueSource) ? 8 : 6;
        if (scaleValueByCounter.boolValue)
        {
            lineCount += 2;
        }
        if (UsesOutgoingDamageFilter(target))
        {
            lineCount += filterOutgoingDamageByCategory.boolValue ? 2 : 1;
        }
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

    private static bool UsesOutgoingDamageFilter(SerializedProperty target)
    {
        return (PassiveModifierTarget)target.enumValueIndex == PassiveModifierTarget.OutgoingDamage;
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

internal static class EffectDrawerUtility
{
    public static readonly float LineHeight = EditorGUIUtility.singleLineHeight;
    public static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;

    public static Rect Line(ref Rect position)
    {
        Rect line = new Rect(position.x, position.y, position.width, LineHeight);
        position.y += LineHeight + Spacing;
        return line;
    }

    public static void Draw(ref Rect position, SerializedProperty property, string label = null, bool includeChildren = true)
    {
        if (property == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(property, includeChildren);
        Rect line = new Rect(position.x, position.y, position.width, height);
        if (string.IsNullOrEmpty(label))
        {
            EditorGUI.PropertyField(line, property, includeChildren);
        }
        else
        {
            EditorGUI.PropertyField(line, property, new GUIContent(label), includeChildren);
        }

        position.y += height + Spacing;
    }

    public static float Height(SerializedProperty property, bool includeChildren = true)
    {
        return property != null ? EditorGUI.GetPropertyHeight(property, includeChildren) + Spacing : 0f;
    }

    public static float Lines(int count)
    {
        return count <= 0 ? 0f : count * LineHeight + count * Spacing;
    }

    public static bool EnumEquals(SerializedProperty property, System.Enum value)
    {
        return property != null && property.enumValueIndex == System.Convert.ToInt32(value);
    }

    public static bool HasText(SerializedProperty property)
    {
        return property != null && !string.IsNullOrWhiteSpace(property.stringValue);
    }
}

public abstract class FoldoutPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect foldoutLine = EffectDrawerUtility.Line(ref position);
        property.isExpanded = EditorGUI.Foldout(foldoutLine, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawExpanded(ref position, property);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return property.isExpanded
            ? EffectDrawerUtility.LineHeight + EffectDrawerUtility.Spacing + GetExpandedHeight(property)
            : EffectDrawerUtility.LineHeight;
    }

    protected abstract void DrawExpanded(ref Rect position, SerializedProperty property);
    protected abstract float GetExpandedHeight(SerializedProperty property);

    protected static SerializedProperty Prop(SerializedProperty property, string name)
    {
        return property.FindPropertyRelative(name);
    }
}

[CustomPropertyDrawer(typeof(RuntimeTagRequirement))]
public class RuntimeTagRequirementDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "tag"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "expectedValue"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "tag"))
            + EffectDrawerUtility.Height(Prop(property, "expectedValue"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeCounterRequirement))]
public class RuntimeCounterRequirementDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "counterId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "comparison"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "value"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "counterId"))
            + EffectDrawerUtility.Height(Prop(property, "comparison"))
            + EffectDrawerUtility.Height(Prop(property, "value"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectStackRequirement))]
public class RuntimeEffectStackRequirementDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "effectIdOrStackKey"), "Effect Id / Stack Key");
        EffectDrawerUtility.Draw(ref position, Prop(property, "comparison"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "value"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "effectIdOrStackKey"))
            + EffectDrawerUtility.Height(Prop(property, "comparison"))
            + EffectDrawerUtility.Height(Prop(property, "value"));
    }
}

[CustomPropertyDrawer(typeof(PassiveRuntimeValueRequirement))]
public class PassiveRuntimeValueRequirementDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "subject"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "metric"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "comparison"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "value"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "subject"))
            + EffectDrawerUtility.Height(Prop(property, "metric"))
            + EffectDrawerUtility.Height(Prop(property, "comparison"))
            + EffectDrawerUtility.Height(Prop(property, "value"));
    }
}

[CustomPropertyDrawer(typeof(PassiveTriggerFilter))]
public class PassiveTriggerFilterDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawFilter(ref position, property, "filterByCategory", "category");
        DrawFilter(ref position, property, "filterByActionId", "actionId");
        DrawFilter(ref position, property, "filterByActionName", "actionName");
        DrawFilter(ref position, property, "filterByStepName", "stepName");
        DrawFilter(ref position, property, "filterByElement", "elementType");
        DrawFilter(ref position, property, "requireRuntimeTag", "runtimeTag");
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return FilterHeight(property, "filterByCategory", "category")
            + FilterHeight(property, "filterByActionId", "actionId")
            + FilterHeight(property, "filterByActionName", "actionName")
            + FilterHeight(property, "filterByStepName", "stepName")
            + FilterHeight(property, "filterByElement", "elementType")
            + FilterHeight(property, "requireRuntimeTag", "runtimeTag");
    }

    private static void DrawFilter(ref Rect position, SerializedProperty property, string toggleName, string valueName)
    {
        SerializedProperty toggle = Prop(property, toggleName);
        EffectDrawerUtility.Draw(ref position, toggle);
        if (toggle.boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, valueName));
        }
    }

    private static float FilterHeight(SerializedProperty property, string toggleName, string valueName)
    {
        SerializedProperty toggle = Prop(property, toggleName);
        float height = EffectDrawerUtility.Height(toggle);
        if (toggle.boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, valueName));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(PassiveRuntimeEffectStackLimitModifierDefinition))]
public class PassiveRuntimeEffectStackLimitModifierDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "effectIdOrStackKey"), "Effect Id / Stack Key");
        EffectDrawerUtility.Draw(ref position, Prop(property, "stackLimit"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "effectIdOrStackKey"))
            + EffectDrawerUtility.Height(Prop(property, "stackLimit"));
    }
}

[CustomPropertyDrawer(typeof(PassiveCooldownModifierDefinition))]
public class PassiveCooldownModifierDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "category"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "actionId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "actionName"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "operation"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "value"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "category"))
            + EffectDrawerUtility.Height(Prop(property, "actionId"))
            + EffectDrawerUtility.Height(Prop(property, "actionName"))
            + EffectDrawerUtility.Height(Prop(property, "operation"))
            + EffectDrawerUtility.Height(Prop(property, "value"));
    }
}

[CustomPropertyDrawer(typeof(PassiveActionModifierDefinition))]
public class PassiveActionModifierDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "category"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "actionId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "actionName"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "stepName"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "unlocked"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "priorityBonus"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "replacementActionId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "replacementActionName"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "startingComboDepthOverride"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "startingComboStepNameOverride"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "consumeRequiredTagsOnUse"));
        DrawValueModifier(ref position, property, "damageMultiplierOperation", "damageMultiplierValue");
        DrawValueModifier(ref position, property, "criticalRateOperation", "criticalRateValue");
        DrawValueModifier(ref position, property, "criticalDamageOperation", "criticalDamageValue");
        DrawValueModifier(ref position, property, "finalDamageOperation", "finalDamageValue");
        SerializedProperty finalCrit = Prop(property, "finalDamageRequiresCriticalHit");
        if (!EffectDrawerUtility.EnumEquals(Prop(property, "finalDamageOperation"), PassiveValueModifierOperation.None))
        {
            EffectDrawerUtility.Draw(ref position, finalCrit);
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "auraEnergyRewardAdd"));
        DrawValueModifier(ref position, property, "cooldownOperation", "cooldownValue");
        DrawValueModifier(ref position, property, "energyCostOperation", "energyCostValue");
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreDefense"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreDefenseRequiresCriticalHit"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreDamageResistance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreTypeReduction"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "replacementProjectileType"));
        if (!EffectDrawerUtility.EnumEquals(Prop(property, "replacementProjectileType"), ProjectileType.None))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "replacementProjectileData"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredTags"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredCounters"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "category"))
            + EffectDrawerUtility.Height(Prop(property, "actionId"))
            + EffectDrawerUtility.Height(Prop(property, "actionName"))
            + EffectDrawerUtility.Height(Prop(property, "stepName"))
            + EffectDrawerUtility.Height(Prop(property, "unlocked"))
            + EffectDrawerUtility.Height(Prop(property, "priorityBonus"))
            + EffectDrawerUtility.Height(Prop(property, "replacementActionId"))
            + EffectDrawerUtility.Height(Prop(property, "replacementActionName"))
            + EffectDrawerUtility.Height(Prop(property, "startingComboDepthOverride"))
            + EffectDrawerUtility.Height(Prop(property, "startingComboStepNameOverride"))
            + EffectDrawerUtility.Height(Prop(property, "consumeRequiredTagsOnUse"))
            + ValueModifierHeight(property, "damageMultiplierOperation", "damageMultiplierValue")
            + ValueModifierHeight(property, "criticalRateOperation", "criticalRateValue")
            + ValueModifierHeight(property, "criticalDamageOperation", "criticalDamageValue")
            + ValueModifierHeight(property, "finalDamageOperation", "finalDamageValue")
            + EffectDrawerUtility.Height(Prop(property, "auraEnergyRewardAdd"))
            + ValueModifierHeight(property, "cooldownOperation", "cooldownValue")
            + ValueModifierHeight(property, "energyCostOperation", "energyCostValue")
            + EffectDrawerUtility.Height(Prop(property, "ignoreDefense"))
            + EffectDrawerUtility.Height(Prop(property, "ignoreDefenseRequiresCriticalHit"))
            + EffectDrawerUtility.Height(Prop(property, "ignoreDamageResistance"))
            + EffectDrawerUtility.Height(Prop(property, "ignoreTypeReduction"))
            + EffectDrawerUtility.Height(Prop(property, "replacementProjectileType"))
            + EffectDrawerUtility.Height(Prop(property, "requiredTags"))
            + EffectDrawerUtility.Height(Prop(property, "requiredCounters"));
        if (!EffectDrawerUtility.EnumEquals(Prop(property, "finalDamageOperation"), PassiveValueModifierOperation.None))
        {
            height += EffectDrawerUtility.Height(Prop(property, "finalDamageRequiresCriticalHit"));
        }
        if (!EffectDrawerUtility.EnumEquals(Prop(property, "replacementProjectileType"), ProjectileType.None))
        {
            height += EffectDrawerUtility.Height(Prop(property, "replacementProjectileData"));
        }

        return height;
    }

    private static void DrawValueModifier(ref Rect position, SerializedProperty property, string operationName, string valueName)
    {
        SerializedProperty operation = Prop(property, operationName);
        EffectDrawerUtility.Draw(ref position, operation);
        if (!EffectDrawerUtility.EnumEquals(operation, PassiveValueModifierOperation.None))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, valueName));
        }
    }

    private static float ValueModifierHeight(SerializedProperty property, string operationName, string valueName)
    {
        SerializedProperty operation = Prop(property, operationName);
        float height = EffectDrawerUtility.Height(operation);
        if (!EffectDrawerUtility.EnumEquals(operation, PassiveValueModifierOperation.None))
        {
            height += EffectDrawerUtility.Height(Prop(property, valueName));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectDamageFilter))]
public class RuntimeEffectDamageFilterDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawFilter(ref position, property, "filterByDelivery", "delivery");
        DrawFilter(ref position, property, "filterByElement", "elementType");
        DrawFilter(ref position, property, "filterByCategory", "category");
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return FilterHeight(property, "filterByDelivery", "delivery")
            + FilterHeight(property, "filterByElement", "elementType")
            + FilterHeight(property, "filterByCategory", "category");
    }

    private static void DrawFilter(ref Rect position, SerializedProperty property, string toggleName, string valueName)
    {
        SerializedProperty toggle = Prop(property, toggleName);
        EffectDrawerUtility.Draw(ref position, toggle);
        if (toggle.boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, valueName));
        }
    }

    private static float FilterHeight(SerializedProperty property, string toggleName, string valueName)
    {
        SerializedProperty toggle = Prop(property, toggleName);
        float height = EffectDrawerUtility.Height(toggle);
        if (toggle.boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, valueName));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectApplicationPolicy))]
public class RuntimeEffectApplicationPolicyDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "kind"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "appliesOn"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "durationType"));
        if (EffectDrawerUtility.EnumEquals(Prop(property, "durationType"), RuntimeEffectDurationType.Seconds))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "durationSeconds"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "stackMode"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "stackable"));
        if (ShowsStackLimit(property))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "stackLimit"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "stackKey"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "refreshDurationOnReapply"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "removeOnOwnerDamaged"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "kind"))
            + EffectDrawerUtility.Height(Prop(property, "appliesOn"))
            + EffectDrawerUtility.Height(Prop(property, "durationType"))
            + EffectDrawerUtility.Height(Prop(property, "stackMode"))
            + EffectDrawerUtility.Height(Prop(property, "stackable"))
            + EffectDrawerUtility.Height(Prop(property, "refreshDurationOnReapply"))
            + EffectDrawerUtility.Height(Prop(property, "removeOnOwnerDamaged"));
        if (EffectDrawerUtility.EnumEquals(Prop(property, "durationType"), RuntimeEffectDurationType.Seconds))
        {
            height += EffectDrawerUtility.Height(Prop(property, "durationSeconds"));
        }
        if (ShowsStackLimit(property))
        {
            height += EffectDrawerUtility.Height(Prop(property, "stackLimit"))
                + EffectDrawerUtility.Height(Prop(property, "stackKey"));
        }

        return height;
    }

    private static bool ShowsStackLimit(SerializedProperty property)
    {
        SerializedProperty stackMode = Prop(property, "stackMode");
        return Prop(property, "stackable").boolValue
            || EffectDrawerUtility.EnumEquals(stackMode, RuntimeEffectStackMode.Stack)
            || EffectDrawerUtility.EnumEquals(stackMode, RuntimeEffectStackMode.Override);
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectParameterSet))]
public class RuntimeEffectParameterSetDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawOverride(ref position, property, "overrideStatModifiers", "statModifiers");
        DrawOverride(ref position, property, "overrideDamageModifier", "damageModifier");
        DrawOverride(ref position, property, "overrideDamageInput", "damageInput");
        DrawOverride(ref position, property, "overridePeriodicHeal", "periodicHeal");
        DrawOverride(ref position, property, "overrideHeal", "heal");
        DrawOverride(ref position, property, "overrideLifesteal", "lifesteal");
        DrawOverride(ref position, property, "overrideAdditionalDamage", "additionalDamage");
        DrawOverride(ref position, property, "overrideStatus", "status");
        DrawOverride(ref position, property, "overrideShield", "shield");
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return OverrideHeight(property, "overrideStatModifiers", "statModifiers")
            + OverrideHeight(property, "overrideDamageModifier", "damageModifier")
            + OverrideHeight(property, "overrideDamageInput", "damageInput")
            + OverrideHeight(property, "overridePeriodicHeal", "periodicHeal")
            + OverrideHeight(property, "overrideHeal", "heal")
            + OverrideHeight(property, "overrideLifesteal", "lifesteal")
            + OverrideHeight(property, "overrideAdditionalDamage", "additionalDamage")
            + OverrideHeight(property, "overrideStatus", "status")
            + OverrideHeight(property, "overrideShield", "shield");
    }

    private static void DrawOverride(ref Rect position, SerializedProperty property, string toggleName, string payloadName)
    {
        SerializedProperty toggle = Prop(property, toggleName);
        EffectDrawerUtility.Draw(ref position, toggle);
        if (toggle.boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, payloadName));
        }
    }

    private static float OverrideHeight(SerializedProperty property, string toggleName, string payloadName)
    {
        SerializedProperty toggle = Prop(property, toggleName);
        float height = EffectDrawerUtility.Height(toggle);
        if (toggle.boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, payloadName));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectDamageModifierParameters))]
public class RuntimeEffectDamageModifierParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "outgoingDamagePercentAdd"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "incomingDamageTakenPercentAdd"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "outgoingDamagePercentAdd"))
            + EffectDrawerUtility.Height(Prop(property, "incomingDamageTakenPercentAdd"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectDamageInputParameters))]
public class RuntimeEffectDamageInputParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "defenderDefensePercentAdd"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "defenderDamageResistancePercentAdd"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreDefense"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreDamageResistance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "ignoreTypeReduction"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "defenderDefensePercentAdd"))
            + EffectDrawerUtility.Height(Prop(property, "defenderDamageResistancePercentAdd"))
            + EffectDrawerUtility.Height(Prop(property, "ignoreDefense"))
            + EffectDrawerUtility.Height(Prop(property, "ignoreDamageResistance"))
            + EffectDrawerUtility.Height(Prop(property, "ignoreTypeReduction"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectHealParameters))]
public class RuntimeEffectHealParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawHealFields(ref position, property);
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return HealHeight(property);
    }

    protected static void DrawHealFields(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "valueSource"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "valueRatio"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "flatValue"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "useSourceStats"));
    }

    protected static float HealHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "valueSource"))
            + EffectDrawerUtility.Height(Prop(property, "valueRatio"))
            + EffectDrawerUtility.Height(Prop(property, "flatValue"))
            + EffectDrawerUtility.Height(Prop(property, "useSourceStats"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectPeriodicHealParameters))]
public class RuntimeEffectPeriodicHealParametersDrawer : RuntimeEffectHealParametersDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawHealFields(ref position, property);
        EffectDrawerUtility.Draw(ref position, Prop(property, "tickInterval"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return HealHeight(property) + EffectDrawerUtility.Height(Prop(property, "tickInterval"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectLifestealParameters))]
public class RuntimeEffectLifestealParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "damageDealtRatio"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "lowHpThreshold"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "lowHpMultiplier"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "damageDealtRatio"))
            + EffectDrawerUtility.Height(Prop(property, "lowHpThreshold"))
            + EffectDrawerUtility.Height(Prop(property, "lowHpMultiplier"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectAdditionalDamageParameters))]
public class RuntimeEffectAdditionalDamageParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "chance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "internalCooldown"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "attackRatio"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "sourceStatRatio"));
        if (Prop(property, "sourceStatRatio").floatValue > 0f)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "sourceStatDamageSource"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "targetMaxHpRatio"));
        DrawSourceStackScaling(ref position, property);
        EffectDrawerUtility.Draw(ref position, Prop(property, "capTargetMaxHpDamage"));
        if (Prop(property, "capTargetMaxHpDamage").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "targetMaxHpDamageCapSource"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "targetMaxHpDamageCapRatio"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "elementType"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "chance"))
            + EffectDrawerUtility.Height(Prop(property, "internalCooldown"))
            + EffectDrawerUtility.Height(Prop(property, "attackRatio"))
            + EffectDrawerUtility.Height(Prop(property, "sourceStatRatio"))
            + EffectDrawerUtility.Height(Prop(property, "targetMaxHpRatio"))
            + SourceStackScalingHeight(property)
            + EffectDrawerUtility.Height(Prop(property, "capTargetMaxHpDamage"))
            + EffectDrawerUtility.Height(Prop(property, "elementType"));
        if (Prop(property, "sourceStatRatio").floatValue > 0f)
        {
            height += EffectDrawerUtility.Height(Prop(property, "sourceStatDamageSource"));
        }
        if (Prop(property, "capTargetMaxHpDamage").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "targetMaxHpDamageCapSource"))
                + EffectDrawerUtility.Height(Prop(property, "targetMaxHpDamageCapRatio"));
        }

        return height;
    }

    private static void DrawSourceStackScaling(ref Rect position, SerializedProperty property)
    {
        SerializedProperty key = Prop(property, "sourceRuntimeEffectStackKey");
        SerializedProperty ratio = Prop(property, "attackRatioPerSourceRuntimeEffectStack");
        EffectDrawerUtility.Draw(ref position, key);
        if (EffectDrawerUtility.HasText(key) || ratio.floatValue > 0f)
        {
            EffectDrawerUtility.Draw(ref position, ratio);
            EffectDrawerUtility.Draw(ref position, Prop(property, "maxSourceRuntimeEffectStacks"));
        }
    }

    private static float SourceStackScalingHeight(SerializedProperty property)
    {
        SerializedProperty key = Prop(property, "sourceRuntimeEffectStackKey");
        SerializedProperty ratio = Prop(property, "attackRatioPerSourceRuntimeEffectStack");
        float height = EffectDrawerUtility.Height(key);
        if (EffectDrawerUtility.HasText(key) || ratio.floatValue > 0f)
        {
            height += EffectDrawerUtility.Height(ratio)
                + EffectDrawerUtility.Height(Prop(property, "maxSourceRuntimeEffectStacks"));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectStatusParameters))]
public class RuntimeEffectStatusParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "status"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "status"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectShieldParameters))]
public class RuntimeEffectShieldParametersDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "valueSource"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "valueRatio"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "flatValue"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "useSourceStats"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "valueSource"))
            + EffectDrawerUtility.Height(Prop(property, "valueRatio"))
            + EffectDrawerUtility.Height(Prop(property, "flatValue"))
            + EffectDrawerUtility.Height(Prop(property, "useSourceStats"));
    }
}

[CustomPropertyDrawer(typeof(RuntimeEffectRecipeData), true)]
public class RuntimeEffectRecipeDataDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "recipeId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "definition"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "presentation"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "policy"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "parameters"));
        SerializedProperty childRecipes = Prop(property, "childRecipes");
        if (childRecipes != null)
        {
            EffectDrawerUtility.Draw(ref position, childRecipes);
        }
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "recipeId"))
            + EffectDrawerUtility.Height(Prop(property, "definition"))
            + EffectDrawerUtility.Height(Prop(property, "presentation"))
            + EffectDrawerUtility.Height(Prop(property, "policy"))
            + EffectDrawerUtility.Height(Prop(property, "parameters"));
        SerializedProperty childRecipes = Prop(property, "childRecipes");
        if (childRecipes != null)
        {
            height += EffectDrawerUtility.Height(childRecipes);
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(PassiveRuntimeTagEffectDefinition))]
public class PassiveRuntimeTagEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "trigger"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "tag"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "addTag"));
        if (Prop(property, "addTag").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "duration"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "rememberEnemyTarget"));
        }
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "trigger"))
            + EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "tag"))
            + EffectDrawerUtility.Height(Prop(property, "addTag"));
        if (Prop(property, "addTag").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "duration"))
                + EffectDrawerUtility.Height(Prop(property, "rememberEnemyTarget"));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(PassiveRuntimeEffectDefinition))]
public class PassiveRuntimeEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "trigger"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredTags"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredCounters"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredRuntimeEffectStacks"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredValues"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "chance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "rollChancePerEnemyTarget"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "enemyTargetSource"));
        if (EffectDrawerUtility.EnumEquals(Prop(property, "enemyTargetSource"), PassiveRuntimeEnemyTargetSource.RuntimeTagHitTargets))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "enemyTargetRuntimeTag"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "consumeRuntimeTagOnApply"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "enableRandomBuffSelection"));
        if (Prop(property, "enableRandomBuffSelection").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "minRandomBuffCount"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "maxRandomBuffCount"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "allowDuplicateRandomBuffs"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "randomBuffPool"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "recipe"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "applicationMode"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "applicationCount"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "overrideSourceCharacter"));
        if (Prop(property, "overrideSourceCharacter").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "sourceCharacterId"));
        }
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "trigger"))
            + EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "requiredTags"))
            + EffectDrawerUtility.Height(Prop(property, "requiredCounters"))
            + EffectDrawerUtility.Height(Prop(property, "requiredRuntimeEffectStacks"))
            + EffectDrawerUtility.Height(Prop(property, "requiredValues"))
            + EffectDrawerUtility.Height(Prop(property, "chance"))
            + EffectDrawerUtility.Height(Prop(property, "rollChancePerEnemyTarget"))
            + EffectDrawerUtility.Height(Prop(property, "enemyTargetSource"))
            + EffectDrawerUtility.Height(Prop(property, "consumeRuntimeTagOnApply"))
            + EffectDrawerUtility.Height(Prop(property, "enableRandomBuffSelection"))
            + EffectDrawerUtility.Height(Prop(property, "recipe"))
            + EffectDrawerUtility.Height(Prop(property, "applicationMode"))
            + EffectDrawerUtility.Height(Prop(property, "applicationCount"))
            + EffectDrawerUtility.Height(Prop(property, "overrideSourceCharacter"));
        if (EffectDrawerUtility.EnumEquals(Prop(property, "enemyTargetSource"), PassiveRuntimeEnemyTargetSource.RuntimeTagHitTargets))
        {
            height += EffectDrawerUtility.Height(Prop(property, "enemyTargetRuntimeTag"));
        }
        if (Prop(property, "enableRandomBuffSelection").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "minRandomBuffCount"))
                + EffectDrawerUtility.Height(Prop(property, "maxRandomBuffCount"))
                + EffectDrawerUtility.Height(Prop(property, "allowDuplicateRandomBuffs"))
                + EffectDrawerUtility.Height(Prop(property, "randomBuffPool"));
        }
        if (Prop(property, "overrideSourceCharacter").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "sourceCharacterId"));
        }
        return height;
    }
}

[CustomPropertyDrawer(typeof(PassiveCounterEffectDefinition))]
public class PassiveCounterEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "trigger"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "counterId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "operation"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "value"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "destinationCounterId"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "destinationValue"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "trigger"))
            + EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "counterId"))
            + EffectDrawerUtility.Height(Prop(property, "operation"))
            + EffectDrawerUtility.Height(Prop(property, "value"))
            + EffectDrawerUtility.Height(Prop(property, "destinationCounterId"))
            + EffectDrawerUtility.Height(Prop(property, "destinationValue"));
    }
}

[CustomPropertyDrawer(typeof(PassiveConditionalStateEffectDefinition))]
public class PassiveConditionalStateEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "trigger"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredTags"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredCounters"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredRuntimeEffectStacks"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredValues"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "runtimeTagEffects"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "counterEffects"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "trigger"))
            + EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "requiredTags"))
            + EffectDrawerUtility.Height(Prop(property, "requiredCounters"))
            + EffectDrawerUtility.Height(Prop(property, "requiredRuntimeEffectStacks"))
            + EffectDrawerUtility.Height(Prop(property, "requiredValues"))
            + EffectDrawerUtility.Height(Prop(property, "runtimeTagEffects"))
            + EffectDrawerUtility.Height(Prop(property, "counterEffects"));
    }
}

[CustomPropertyDrawer(typeof(PassiveOnHitStatusEffectDefinition))]
public class PassiveOnHitStatusEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "status"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "chance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "scope"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "status"))
            + EffectDrawerUtility.Height(Prop(property, "chance"))
            + EffectDrawerUtility.Height(Prop(property, "scope"));
    }
}

[CustomPropertyDrawer(typeof(AdditionalDamageEffectDefinition))]
public class AdditionalDamageEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawAdditionalDamage(ref position, property);
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return AdditionalDamageHeight(property);
    }

    protected static void DrawAdditionalDamage(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "chance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "internalCooldown"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "criticalHitRequirement"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "attackRatio"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "sourceStatRatio"));
        if (Prop(property, "sourceStatRatio").floatValue > 0f)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "sourceStatDamageSource"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "targetMaxHpRatio"));
        DrawTargetStackScaling(ref position, property);
        EffectDrawerUtility.Draw(ref position, Prop(property, "capTargetMaxHpDamage"));
        if (Prop(property, "capTargetMaxHpDamage").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "targetMaxHpDamageCapSource"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "targetMaxHpDamageCapRatio"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "elementType"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "consumeRuntimeTagOnTrigger"));
    }

    protected static float AdditionalDamageHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "chance"))
            + EffectDrawerUtility.Height(Prop(property, "internalCooldown"))
            + EffectDrawerUtility.Height(Prop(property, "criticalHitRequirement"))
            + EffectDrawerUtility.Height(Prop(property, "attackRatio"))
            + EffectDrawerUtility.Height(Prop(property, "sourceStatRatio"))
            + EffectDrawerUtility.Height(Prop(property, "targetMaxHpRatio"))
            + TargetStackScalingHeight(property)
            + EffectDrawerUtility.Height(Prop(property, "capTargetMaxHpDamage"))
            + EffectDrawerUtility.Height(Prop(property, "elementType"))
            + EffectDrawerUtility.Height(Prop(property, "consumeRuntimeTagOnTrigger"));
        if (Prop(property, "sourceStatRatio").floatValue > 0f)
        {
            height += EffectDrawerUtility.Height(Prop(property, "sourceStatDamageSource"));
        }
        if (Prop(property, "capTargetMaxHpDamage").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "targetMaxHpDamageCapSource"))
                + EffectDrawerUtility.Height(Prop(property, "targetMaxHpDamageCapRatio"));
        }

        return height;
    }

    private static void DrawTargetStackScaling(ref Rect position, SerializedProperty property)
    {
        SerializedProperty key = Prop(property, "targetRuntimeEffectStackKey");
        SerializedProperty ratio = Prop(property, "attackRatioPerTargetRuntimeEffectStack");
        EffectDrawerUtility.Draw(ref position, key);
        if (EffectDrawerUtility.HasText(key) || ratio.floatValue > 0f)
        {
            EffectDrawerUtility.Draw(ref position, ratio);
            EffectDrawerUtility.Draw(ref position, Prop(property, "maxTargetRuntimeEffectStacks"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "consumeTargetRuntimeEffectStacksOnTrigger"));
        }
    }

    private static float TargetStackScalingHeight(SerializedProperty property)
    {
        SerializedProperty key = Prop(property, "targetRuntimeEffectStackKey");
        SerializedProperty ratio = Prop(property, "attackRatioPerTargetRuntimeEffectStack");
        float height = EffectDrawerUtility.Height(key);
        if (EffectDrawerUtility.HasText(key) || ratio.floatValue > 0f)
        {
            height += EffectDrawerUtility.Height(ratio)
                + EffectDrawerUtility.Height(Prop(property, "maxTargetRuntimeEffectStacks"))
                + EffectDrawerUtility.Height(Prop(property, "consumeTargetRuntimeEffectStacksOnTrigger"));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(PassiveDashThroughDamageDefinition))]
public class PassiveDashThroughDamageDefinitionDrawer : AdditionalDamageEffectDefinitionDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        DrawAdditionalDamage(ref position, property);
        EffectDrawerUtility.Draw(ref position, Prop(property, "overrideDetectionBox"));
        if (Prop(property, "overrideDetectionBox").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "detectionBoxSize"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "startDelaySeconds"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "activeSeconds"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "scanIntervalSeconds"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requireDashInvulnerability"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "hitEachEnemyOnce"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "maxTargetsPerDash"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = AdditionalDamageHeight(property)
            + EffectDrawerUtility.Height(Prop(property, "overrideDetectionBox"))
            + EffectDrawerUtility.Height(Prop(property, "startDelaySeconds"))
            + EffectDrawerUtility.Height(Prop(property, "activeSeconds"))
            + EffectDrawerUtility.Height(Prop(property, "scanIntervalSeconds"))
            + EffectDrawerUtility.Height(Prop(property, "requireDashInvulnerability"))
            + EffectDrawerUtility.Height(Prop(property, "hitEachEnemyOnce"))
            + EffectDrawerUtility.Height(Prop(property, "maxTargetsPerDash"));
        if (Prop(property, "overrideDetectionBox").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "detectionBoxSize"));
        }

        return height;
    }
}

[CustomPropertyDrawer(typeof(PassiveDefensiveEffectDefinition))]
public class PassiveDefensiveEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "evasionChance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "grantsDamageImmunity"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "grantsDebuffImmunity"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        return EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "evasionChance"))
            + EffectDrawerUtility.Height(Prop(property, "grantsDamageImmunity"))
            + EffectDrawerUtility.Height(Prop(property, "grantsDebuffImmunity"));
    }
}

[CustomPropertyDrawer(typeof(PassiveResourceEffectDefinition))]
public class PassiveResourceEffectDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "trigger"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredTags"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredCounters"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredRuntimeEffectStacks"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "requiredValues"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "chance"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "effectType"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "scope"));
        if (UsesResourceValue(property))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "valueMode"));
            if (EffectDrawerUtility.EnumEquals(Prop(property, "valueMode"), PassiveResourceValueMode.CharacterStat))
            {
                EffectDrawerUtility.Draw(ref position, Prop(property, "valueSource"));
            }
            EffectDrawerUtility.Draw(ref position, Prop(property, "valueRatio"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "flatValue"));
        }
        if (UsesRuntimeTag(property))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "runtimeTag"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "runtimeTagDuration"));
        }
        if (UsesCooldownTarget(property))
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "cooldownActionId"));
            EffectDrawerUtility.Draw(ref position, Prop(property, "cooldownActionName"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "cooldownTag"));
        if (EffectDrawerUtility.HasText(Prop(property, "cooldownTag")) || Prop(property, "cooldownDuration").floatValue > 0f)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "cooldownDuration"));
        }
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "trigger"))
            + EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "requiredTags"))
            + EffectDrawerUtility.Height(Prop(property, "requiredCounters"))
            + EffectDrawerUtility.Height(Prop(property, "requiredRuntimeEffectStacks"))
            + EffectDrawerUtility.Height(Prop(property, "requiredValues"))
            + EffectDrawerUtility.Height(Prop(property, "chance"))
            + EffectDrawerUtility.Height(Prop(property, "effectType"))
            + EffectDrawerUtility.Height(Prop(property, "scope"))
            + EffectDrawerUtility.Height(Prop(property, "cooldownTag"));
        if (UsesResourceValue(property))
        {
            height += EffectDrawerUtility.Height(Prop(property, "valueMode"))
                + EffectDrawerUtility.Height(Prop(property, "valueRatio"))
                + EffectDrawerUtility.Height(Prop(property, "flatValue"));
            if (EffectDrawerUtility.EnumEquals(Prop(property, "valueMode"), PassiveResourceValueMode.CharacterStat))
            {
                height += EffectDrawerUtility.Height(Prop(property, "valueSource"));
            }
        }
        if (UsesRuntimeTag(property))
        {
            height += EffectDrawerUtility.Height(Prop(property, "runtimeTag"))
                + EffectDrawerUtility.Height(Prop(property, "runtimeTagDuration"));
        }
        if (UsesCooldownTarget(property))
        {
            height += EffectDrawerUtility.Height(Prop(property, "cooldownActionId"))
                + EffectDrawerUtility.Height(Prop(property, "cooldownActionName"));
        }
        if (EffectDrawerUtility.HasText(Prop(property, "cooldownTag")) || Prop(property, "cooldownDuration").floatValue > 0f)
        {
            height += EffectDrawerUtility.Height(Prop(property, "cooldownDuration"));
        }

        return height;
    }

    private static bool UsesResourceValue(SerializedProperty property)
    {
        PassiveResourceEffectType type = (PassiveResourceEffectType)Prop(property, "effectType").enumValueIndex;
        return type == PassiveResourceEffectType.Heal
            || type == PassiveResourceEffectType.RefillUltimateAuraEnergy
            || type == PassiveResourceEffectType.ReduceSkillCooldown
            || type == PassiveResourceEffectType.ReduceUltimateCooldown;
    }

    private static bool UsesRuntimeTag(SerializedProperty property)
    {
        return EffectDrawerUtility.EnumEquals(Prop(property, "effectType"), PassiveResourceEffectType.WaiveUltimateAuraEnergyCost);
    }

    private static bool UsesCooldownTarget(SerializedProperty property)
    {
        PassiveResourceEffectType type = (PassiveResourceEffectType)Prop(property, "effectType").enumValueIndex;
        return type == PassiveResourceEffectType.ResetSkillCooldown
            || type == PassiveResourceEffectType.ResetUltimateCooldown
            || type == PassiveResourceEffectType.ReduceSkillCooldown
            || type == PassiveResourceEffectType.ReduceUltimateCooldown;
    }
}

[CustomPropertyDrawer(typeof(PassiveEnemyElementModifierDefinition))]
public class PassiveEnemyElementModifierDefinitionDrawer : FoldoutPropertyDrawer
{
    protected override void DrawExpanded(ref Rect position, SerializedProperty property)
    {
        EffectDrawerUtility.Draw(ref position, Prop(property, "trigger"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "filter"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "elementType"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "incomingDamagePercentAdd"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "resistancePercentAdd"));
        EffectDrawerUtility.Draw(ref position, Prop(property, "permanent"));
        if (!Prop(property, "permanent").boolValue)
        {
            EffectDrawerUtility.Draw(ref position, Prop(property, "duration"));
        }
        EffectDrawerUtility.Draw(ref position, Prop(property, "stacks"));
    }

    protected override float GetExpandedHeight(SerializedProperty property)
    {
        float height = EffectDrawerUtility.Height(Prop(property, "trigger"))
            + EffectDrawerUtility.Height(Prop(property, "filter"))
            + EffectDrawerUtility.Height(Prop(property, "elementType"))
            + EffectDrawerUtility.Height(Prop(property, "incomingDamagePercentAdd"))
            + EffectDrawerUtility.Height(Prop(property, "resistancePercentAdd"))
            + EffectDrawerUtility.Height(Prop(property, "permanent"))
            + EffectDrawerUtility.Height(Prop(property, "stacks"));
        if (!Prop(property, "permanent").boolValue)
        {
            height += EffectDrawerUtility.Height(Prop(property, "duration"));
        }

        return height;
    }
}
