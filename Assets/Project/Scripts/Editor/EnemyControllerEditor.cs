using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hides combat fields that the current role or kit does not use, so the prefab
/// only shows what a designer can actually change.
/// </summary>
[CustomEditor(typeof(EnemyController))]
[CanEditMultipleObjects]
public sealed class EnemyControllerEditor : Editor
{
    private static readonly string[] RangedOnlyFields =
    {
        "rangedShotSpeed",
        "rangedShotLifetime",
        "rangedMuzzle",
        "rangedSpawnOffset",
        "rangedShotSprite",
        "rangedShotTint",
        "rangedShotRadius",
        "rangedLineMask",
    };

    private static readonly string[] KitOwnedFields =
    {
        "combatRole",
        "attackRangeOverride",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty kit = serializedObject.FindProperty("kit");
        SerializedProperty role = serializedObject.FindProperty("combatRole");
        bool hasKit = kit != null && kit.objectReferenceValue != null;
        bool ranged = ResolveIsRanged(kit, role, hasKit);

        HashSet<string> hidden = BuildHiddenSet(hasKit, ranged);

        if (hasKit)
        {
            EditorGUILayout.HelpBox(
                "Combat values come from the Enemy Kit asset. Clear the kit to author them on this prefab.",
                MessageType.Info);
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator);
                }

                continue;
            }

            if (hidden.Contains(iterator.propertyPath))
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool ResolveIsRanged(SerializedProperty kit, SerializedProperty role, bool hasKit)
    {
        if (hasKit)
        {
            var kitAsset = kit.objectReferenceValue as EnemyKit;
            return kitAsset != null && kitAsset.UsesRangedAttack;
        }

        return role != null && role.enumValueIndex == (int)EnemyCombatRole.RangedHold;
    }

    private static HashSet<string> BuildHiddenSet(bool hasKit, bool ranged)
    {
        var hidden = new HashSet<string>();

        if (hasKit)
        {
            foreach (string field in KitOwnedFields)
            {
                hidden.Add(field);
            }
        }

        // The muzzle Transform lives on the prefab, so a kit cannot supply it.
        if (!ranged || hasKit)
        {
            foreach (string field in RangedOnlyFields)
            {
                if (hasKit && ranged && field == "rangedMuzzle")
                {
                    continue;
                }

                hidden.Add(field);
            }
        }

        return hidden;
    }
}
