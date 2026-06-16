# Analysis Report — Milestone 1 (R1 & R3)

This report details the investigation of the codebase for Neo-JJK's Milestone 1 requirements, focusing on **R1 (Aura Energy Gain of Chance Refactor)** and **R3 (Remove Aura Energy Rate from Artifact Field)**.

---

## 1. Executive Summary
- **R1 (Aura Energy Gain of Chance Refactor)**: The `AuraEnergyRate` enum and field (previously removed in a local refactoring) must be restored to `CharacterStats.cs` with tiers `Low (5%)`, `Med (10%)`, and `High (15%)`. In `PlayerCombat.NotifyResolvedEnemyHit`, the probability roll must be executed on all hits (kills and non-kills alike) using this rate, unless no hitbox data is active or `baseGainAmount <= 0f`.
- **R3 (Remove Aura Energy Rate from Artifact Field)**: Artifacts use `CharacterStats` for their `bonusStats` field, which automatically exposes all serialized fields of `CharacterStats` in the Inspector. To cleanly hide `auraEnergyRate` from Artifacts without breaking `CharacterStats` serialization or violating compatibility contracts, a Custom Editor for `Artifact` (`ArtifactEditor.cs`) should be created to draw the `bonusStats` properties while excluding the `auraEnergyRate` field.

---

## 2. Evidence Chain & Observations

### 2.1 Character Stats (`CharacterStats.cs`)
- **Location**: `Assets/Project/Scripts/Character/CharacterStats.cs`
- **Current State**:
  - The `AuraEnergyRate` enum and `auraEnergyRate` field are absent (removed in local modifications).
  - The `Clone()` and `AddNumericBonuses()` methods do not handle `auraEnergyRate`.
- **Reference Assets**:
  - Existing Character assets (e.g., `CharacterObject_Kyo.asset`, `CharacterObject_Rugal.asset`, `CharacterObject_Goenitz.asset`, `CharacterObject_main.asset`) still serialize `auraEnergyRate` as integer values (e.g., `auraEnergyRate: 0`, `1`, or `2`).
  - Restoring `AuraEnergyRate` as an enum with `Low = 0`, `Med = 1`, `High = 2` automatically aligns with these serialized assets.

### 2.2 Combat Energy Gain (`PlayerCombat.cs`)
- **Location**: `Assets/Project/Scripts/Combat/PlayerCombat.cs`
- **Current State**:
  - `NotifyResolvedEnemyHit()` currently uses a hardcoded check:
    ```csharp
    if (UnityEngine.Random.value > 0.8f)
        return;
    ```
  - The `ResolveAuraEnergyGainChance()` helper and `AuraEnergyRate` local lookup are missing.
  - The chance check is wrapped inside `if (!isDefeated)`.
- **Proposed Logic**:
  - The chance roll should be done on every resolved hit (both normal and kill hits) by moving the roll outside of `if (!isDefeated)`.
  - An early return `if (character == null || baseGainAmount <= 0f)` prevents the roll if no energy is configured.

### 2.3 Artifact Definition (`Artifact.cs`)
- **Location**: `Assets/Project/Scripts/Inventory/Items/Artifact.cs`
- **Current State**:
  - Defines `public CharacterStats bonusStats;` (lines 14-16).
  - No custom editor exists for `Artifact`.
- **Proposed Solution**:
  - Add `ArtifactEditor.cs` under `Assets/Project/Scripts/Editor/` to custom-draw the Inspector.
  - Traverse the child properties of `bonusStats` and skip drawing `auraEnergyRate`.

---

## 3. Proposed Code Changes

### Proposed `CharacterStats.cs` Changes
```diff
diff --git a/Assets/Project/Scripts/Character/CharacterStats.cs b/Assets/Project/Scripts/Character/CharacterStats.cs
index c66f0f0..29e58bb 100644
--- a/Assets/Project/Scripts/Character/CharacterStats.cs
+++ b/Assets/Project/Scripts/Character/CharacterStats.cs
@@ -1,5 +1,12 @@
 using UnityEngine;
 
+public enum AuraEnergyRate
+{
+    Low,
+    Med,
+    High
+}
+
 [System.Serializable]
 public class CharacterStats
 {
@@ -30,6 +37,8 @@ public class CharacterStats
     [Header("Aura Energy")]
     [Tooltip("Multiplier applied to all gained aura energy.")]
     public float auraEnergyMultiplier = 1f; // Multiplier for gained AE
+    [Tooltip("Aura Energy gain rate tier. Low = 5% chance, Med = 10%, High = 15%.")]
+    public AuraEnergyRate auraEnergyRate = AuraEnergyRate.Med;
 
     public CharacterStats Clone()
     {
@@ -47,6 +56,7 @@ public class CharacterStats
             damageMastery = damageMastery,
             damageResistance = damageResistance,
             auraEnergyMultiplier = auraEnergyMultiplier,
+            auraEnergyRate = auraEnergyRate
         };
     }
```

### Proposed `PlayerCombat.cs` Changes
```diff
diff --git a/Assets/Project/Scripts/Combat/PlayerCombat.cs b/Assets/Project/Scripts/Combat/PlayerCombat.cs
index 04af699..4eb5a17 100644
--- a/Assets/Project/Scripts/Combat/PlayerCombat.cs
+++ b/Assets/Project/Scripts/Combat/PlayerCombat.cs
@@ -785,11 +785,17 @@ public class PlayerCombat : MonoBehaviour, ICharacterCombat
         CharacterStats stats = teamLoader != null
             ? teamLoader.GetCharacterRuntimeStats(character)
             : character.stats;
+        AuraEnergyRate auraEnergyRate = stats != null
+            ? stats.auraEnergyRate
+            : AuraEnergyRate.Med;
 
         float hitDensityMultiplier = 1f;
+        if (UnityEngine.Random.value > ResolveAuraEnergyGainChance(auraEnergyRate))
+            return;
+
         if (!isDefeated)
         {
-            if (UnityEngine.Random.value > 0.8f)
-                return;
-
             if (!TryConsumeAuraEnergyGrantSlot())
                 return;
 
@@ -814,6 +820,15 @@ public class PlayerCombat : MonoBehaviour, ICharacterCombat
         teamLoader.AddCharacterAE(character, energyToGain);
     }
 
+    private static float ResolveAuraEnergyGainChance(AuraEnergyRate auraEnergyRate)
+    {
+        return auraEnergyRate switch
+        {
+            AuraEnergyRate.Low => 0.05f,
+            AuraEnergyRate.High => 0.15f,
+            _ => 0.10f
+        };
+    }
```

### Proposed `ArtifactEditor.cs` (New File under `Assets/Project/Scripts/Editor/`)
```csharp
using UnityEditor;
using UnityEngine;
using Project.InventorySystem.Items;

namespace Project.Editor
{
    [CustomEditor(typeof(Artifact))]
    public class ArtifactEditor : UnityEditor.Editor
    {
        private SerializedProperty idProp;
        private SerializedProperty itemNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty iconProp;
        private SerializedProperty priceProp;
        private SerializedProperty isEquippableProp;
        private SerializedProperty passiveDescriptionProp;
        private SerializedProperty bonusStatsProp;
        private SerializedProperty additionalDamageEffectsProp;

        private void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            itemNameProp = serializedObject.FindProperty("itemName");
            descriptionProp = serializedObject.FindProperty("description");
            iconProp = serializedObject.FindProperty("icon");
            priceProp = serializedObject.FindProperty("price");
            isEquippableProp = serializedObject.FindProperty("isEquippable");
            passiveDescriptionProp = serializedObject.FindProperty("passiveDescription");
            bonusStatsProp = serializedObject.FindProperty("bonusStats");
            additionalDamageEffectsProp = serializedObject.FindProperty("additionalDamageEffects");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 1. Draw BaseItem fields
            EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(idProp);
            EditorGUILayout.PropertyField(itemNameProp);
            EditorGUILayout.PropertyField(descriptionProp);
            EditorGUILayout.PropertyField(iconProp);
            EditorGUILayout.PropertyField(priceProp);

            EditorGUILayout.Space();

            // 2. Draw Artifact Specifics
            EditorGUILayout.LabelField("Artifact Specifics", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(isEquippableProp);
            EditorGUILayout.PropertyField(passiveDescriptionProp);

            EditorGUILayout.Space();

            // 3. Draw Stat Bonuses (excluding auraEnergyRate)
            if (bonusStatsProp != null)
            {
                EditorGUILayout.LabelField("Stat Bonuses", EditorStyles.boldLabel);
                bonusStatsProp.isExpanded = EditorGUILayout.Foldout(bonusStatsProp.isExpanded, "Bonus Stats");
                if (bonusStatsProp.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty child = bonusStatsProp.Copy();
                    bool enterChildren = true;
                    while (child.NextVisible(enterChildren))
                    {
                        enterChildren = false;

                        // Ensure we are only drawing children of bonusStats
                        if (!child.propertyPath.StartsWith(bonusStatsProp.propertyPath + "."))
                            break;

                        // Exclude auraEnergyRate from the inspector
                        if (child.name == "auraEnergyRate")
                            continue;

                        EditorGUILayout.PropertyField(child, true);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space();

            // 4. Draw Passive Damage effects
            EditorGUILayout.PropertyField(additionalDamageEffectsProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
```

---

## 4. Verification Methods

### 4.1 Inspector Verification
- Open Unity Editor, select a `CharacterObject` asset (e.g. `CharacterObject_Kyo.asset`).
- Verify that the `Aura Energy Rate` field is present in the `Stats` section and displays as `Low (5%)`, `Med (10%)`, or `High (15%)`.
- Select an `Artifact` asset. Verify that under `Bonus Stats`, the `Aura Energy Rate` field is completely hidden and not editable.

### 4.2 Code Compilation & Reload
- Call `refresh_unity` MCP tool to reload the Unity Domain and inspect the console logs using `read_console` to verify that no compiler errors exist.
