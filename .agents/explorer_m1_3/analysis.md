# Milestone 1 Investigation: R1 (Aura Energy Gain Chance Refactor) and R3 (Remove Aura Energy Rate from Artifact Field)

## Executive Summary
This analysis details the current references to `AuraEnergyRate` and `auraEnergyRate` in the codebase and provides a design for reintegrating the `AuraEnergyRate` categories (`High (15%)`, `Med (10%)`, `Low (5%)`) into `CharacterStats.cs` and `PlayerCombat.cs` (for R1) while ensuring that:
1. The `auraEnergyRate` field of `CharacterStats` within the `Artifact` object is **not visible or editable** in the Artifact inspector (for R3).
2. The `AddNumericBonuses` method in `CharacterStats` **does not merge or add** the `auraEnergyRate` stat during runtime calculations.

---

## 1. Codebase Reference Map

Through a comprehensive grep search, references to `AuraEnergyRate` and `auraEnergyRate` were analyzed:
* **`Assets/Project/Scripts/Character/CharacterStats.cs`**:
  * Currently does **not** define `AuraEnergyRate` or `auraEnergyRate`.
  * `Clone()` does not clone it.
  * `AddNumericBonuses()` does not sum it.
* **`Assets/Project/Scripts/Combat/PlayerCombat.cs`**:
  * Currently uses a hardcoded 20% random check (`UnityEngine.Random.value > 0.8f`) in `NotifyResolvedEnemyHit()` for non-kill hits, instead of utilizing the enum.
* **`Assets/Project/Scripts/Inventory/Items/Artifact.cs`**:
  * Defines `public CharacterStats bonusStats;` (line 16). Because `CharacterStats` is `[System.Serializable]`, Unity automatically serializes and draws all public serialized fields of `CharacterStats` inside the Artifact inspector by default.
* **Asset Serialization Files (YAML)**:
  * Existing character files (e.g., `CharacterObject_Kyo.asset`) and artifact files (e.g., `a_1_Shattered Crown.asset`) still contain serialized lines for `auraEnergyRate: X`. These are currently vestigial because they don't map to any C# fields, meaning Unity safely ignores them upon loading.

---

## 2. Preventing visibility and editability in the Artifact Inspector

If `auraEnergyRate` is added back to `CharacterStats` to support character-level energy rate variations (R1), it would default to displaying inside the nested foldout of `bonusStats` in `Artifact.cs`. To prevent this, two standard Unity Editor extension patterns can be used.

### Method A: Custom Property Drawer for `CharacterStats` (Recommended)
By creating a custom property drawer for the `CharacterStats` class, we can check the parent serialized object's type at runtime. If the containing asset is an `Artifact`, we can selectively skip drawing the `auraEnergyRate` field.

**Proposed Script: `Assets/Project/Scripts/Editor/CharacterStatsDrawer.cs`**
```csharp
using UnityEditor;
using UnityEngine;
using Project.InventorySystem.Items;

[CustomPropertyDrawer(typeof(CharacterStats))]
public class CharacterStatsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Determine if the target object hosting this property is an Artifact asset
        bool isArtifact = property.serializedObject.targetObject is Artifact;

        position.height = EditorGUIUtility.singleLineHeight;
        property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label);
        
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            SerializedProperty endProperty = property.GetEndProperty();
            SerializedProperty childProp = property.Copy();
            childProp.NextVisible(true);
            
            while (!SerializedProperty.EqualContents(childProp, endProperty))
            {
                // Conditionally skip rendering auraEnergyRate on Artifacts
                if (isArtifact && childProp.name == "auraEnergyRate")
                {
                    childProp.NextVisible(false);
                    continue;
                }
                
                position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(position, childProp, true);
                childProp.NextVisible(false);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float height = EditorGUIUtility.singleLineHeight;
        bool isArtifact = property.serializedObject.targetObject is Artifact;

        SerializedProperty endProperty = property.GetEndProperty();
        SerializedProperty childProp = property.Copy();
        childProp.NextVisible(true);

        while (!SerializedProperty.EqualContents(childProp, endProperty))
        {
            if (isArtifact && childProp.name == "auraEnergyRate")
            {
                childProp.NextVisible(false);
                continue;
            }
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            childProp.NextVisible(false);
        }

        return height;
    }
}
```

### Method B: Custom Inspector for `Artifact`
Alternatively, we can write a custom inspector editor specifically for the `Artifact` class. This leaves the standard `CharacterStats` drawing untouched elsewhere but explicitly overrides the drawing of `Artifact.bonusStats` to skip rendering the `auraEnergyRate` sub-property.

**Proposed Script: `Assets/Project/Scripts/Editor/ArtifactEditor.cs`**
```csharp
using UnityEditor;
using Project.InventorySystem.Items;

[CustomEditor(typeof(Artifact))]
public class ArtifactEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty prop = serializedObject.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name == "m_Script")
                    continue;

                if (prop.name == "bonusStats")
                {
                    EditorGUILayout.PropertyField(prop, false); // Draw stats foldout header
                    if (prop.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        SerializedProperty child = prop.Copy();
                        SerializedProperty end = prop.GetEndProperty();
                        if (child.NextVisible(true))
                        {
                            while (!SerializedProperty.EqualContents(child, end))
                            {
                                // Draw all fields inside bonusStats except auraEnergyRate
                                if (child.name != "auraEnergyRate")
                                {
                                    EditorGUILayout.PropertyField(child, true);
                                }
                                child.NextVisible(false);
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }
            while (prop.NextVisible(false));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
```

---

## 3. Ensuring `AddNumericBonuses` Does Not Merge It

In `CharacterStats.cs`, the `AddNumericBonuses` method is defined as:
```csharp
    public void AddNumericBonuses(CharacterStats bonus)
    {
        if (bonus == null) return;

        maxHP += bonus.maxHP;
        attack += bonus.attack;
        defense += bonus.defense;
        speed += bonus.speed;
        elementalMultiplier += bonus.elementalMultiplier;
        critRate += bonus.critRate;
        critDamage += bonus.critDamage;
        cooldownReduction += bonus.cooldownReduction;
        skillMastery += bonus.skillMastery;
        damageMastery += bonus.damageMastery;
        damageResistance += bonus.damageResistance;
        auraEnergyMultiplier += bonus.auraEnergyMultiplier;
    }
```

To ensure `auraEnergyRate` is not merged/added during stat summation:
1. **Explicit Exclusion**: The method works by explicitly summing each field one-by-one rather than using reflection. As long as `AddNumericBonuses` is kept as-is and does not reference `auraEnergyRate`, no merge will occur.
2. **Type Safety**: Because `AuraEnergyRate` is an enum, trying to add them together (e.g. `auraEnergyRate += bonus.auraEnergyRate;`) would fail compilation. This provides compile-time guarantee that the enum cannot be merged.

---

## 4. Integration Proposals for R1 (AE Gain Chance Refactor)

To fully integrate the enum rates back into the combat system:

### A. Define the Enum and Field in `CharacterStats.cs`
Update `CharacterStats.cs` to declare `AuraEnergyRate` and the serializable field, and update `Clone()`.

```csharp
public enum AuraEnergyRate
{
    Low,
    Med,
    High
}

[System.Serializable]
public class CharacterStats
{
    // ... Existing stats fields ...

    [Header("Aura Energy")]
    public AuraEnergyRate auraEnergyRate = AuraEnergyRate.Med;
    [Tooltip("Multiplier applied to all gained aura energy.")]
    public float auraEnergyMultiplier = 1f;

    public CharacterStats Clone()
    {
        return new CharacterStats
        {
            // ... Existing fields ...
            auraEnergyMultiplier = auraEnergyMultiplier,
            auraEnergyRate = auraEnergyRate // Clone enum value
        };
    }

    public void AddNumericBonuses(CharacterStats bonus)
    {
        // Explicitly exclude auraEnergyRate here
        // ... Existing fields mapped ...
    }
}
```

### B. Handle Chance Check in `PlayerCombat.NotifyResolvedEnemyHit`
In `PlayerCombat.cs`, retrieve the chance threshold dynamically based on the active runtime character stats.

```csharp
    public void NotifyResolvedEnemyHit(
        CharacterObject character,
        float onHitAmount,
        float onKillAmount,
        bool isDefeated)
    {
        float baseGainAmount = isDefeated ? onKillAmount : onHitAmount;
        if (character == null || baseGainAmount <= 0f)
            return;

        if (teamLoader == null)
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();

        CharacterStats stats = teamLoader != null
            ? teamLoader.GetCharacterRuntimeStats(character)
            : character.stats;

        float hitDensityMultiplier = 1f;
        if (!isDefeated)
        {
            // Retrieve dynamic threshold based on AuraEnergyRate
            float chance = 0.10f; // Default (Medium)
            if (stats != null)
            {
                switch (stats.auraEnergyRate)
                {
                    case AuraEnergyRate.High:
                        chance = 0.15f;
                        break;
                    case AuraEnergyRate.Med:
                        chance = 0.10f;
                        break;
                    case AuraEnergyRate.Low:
                        chance = 0.05f;
                        break;
                }
            }

            if (UnityEngine.Random.value > chance)
                return;

            if (!TryConsumeAuraEnergyGrantSlot())
                return;

            hitDensityMultiplier = ConsumeSameFrameHitAuraMultiplier();
        }

        float statMultiplier = stats != null
            ? Mathf.Max(0f, stats.auraEnergyMultiplier)
            : 1f;
        float energyToGain = Mathf.Max(0f, baseGainAmount)
            * statMultiplier
            * hitDensityMultiplier;

        if (energyToGain <= 0f)
            return;

        if (teamLoader == null)
            return;

        teamLoader.AddCharacterAE(character, energyToGain);
    }
```
