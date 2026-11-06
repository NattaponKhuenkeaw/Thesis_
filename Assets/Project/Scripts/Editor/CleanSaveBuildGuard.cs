using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CleanSaveBuildGuard : IPreprocessBuildWithReport
{
    public const string InventoryPath = "Assets/Project/Resources/InventorySO/PlayerRunInventory.asset";
    public const string ProgressionPath = "Assets/Project/Resources/InventorySO/PlayerMetaProgression.asset";
    public int callbackOrder => int.MaxValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        Validate(AssetDatabase.LoadAssetAtPath<PlayerRunInventory>(InventoryPath),
            AssetDatabase.LoadAssetAtPath<PlayerMetaProgression>(ProgressionPath));
    }

    public static void Validate(PlayerRunInventory inventory, PlayerMetaProgression progression)
    {
        var problems = new List<string>();
        var main = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
        if (main == null || main.characterObjectData == null)
            problems.Add("The starting Main character is missing.");
        if (inventory == null) problems.Add("PlayerRunInventory is missing.");
        if (progression == null) problems.Add("PlayerMetaProgression is missing.");
        if (main != null && main.characterObjectData != null && inventory != null)
        {
            using var data = new SerializedObject(inventory);
            Check(data.FindProperty("currency").intValue == 1000, "Starting Soul must be 1000.", problems);
            Check(data.FindProperty("_globalLevel").intValue == 1, "Starting Level must be 1.", problems);
            Check(data.FindProperty("activeTeamSlotIndex").intValue == 0, "Starting team slot must be 0.", problems);
            CheckEmpty(data, "artifactInstances", problems);
            CheckEmpty(data, "itemStacks", problems);
            var owned = data.FindProperty("ownedItems");
            Check(owned.arraySize == 1 && owned.GetArrayElementAtIndex(0).objectReferenceValue == main,
                "The starting inventory must contain only Main.", problems);
            var characters = data.FindProperty("runCharacters");
            Check(characters.arraySize <= 1, "Run character progress is present.", problems);
            for (int i = 0; i < characters.arraySize; i++)
            {
                var character = characters.GetArrayElementAtIndex(i);
                var upgrade = character.FindPropertyRelative("upgrade");
                Check(character.FindPropertyRelative("character").objectReferenceValue == main.characterObjectData
                    && character.FindPropertyRelative("currentHP").floatValue == -1f
                    && character.FindPropertyRelative("currentAE").floatValue == -1f
                    && upgrade.FindPropertyRelative("level").intValue == 1
                    && upgrade.FindPropertyRelative("currentExp").intValue == 0,
                    "Run HP, AE or character upgrades are present.", problems);
            }
            var slots = data.FindProperty("teamSlots");
            Check(slots.arraySize == 2, "The starting team must have two slots.", problems);
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                Check(slot.FindPropertyRelative("character").objectReferenceValue == (i == 0 ? main.characterObjectData : null),
                    "Only Main may be in the starting team.", problems);
                var legacy = slot.FindPropertyRelative("legacyArtifacts");
                for (int j = 0; j < legacy.arraySize; j++)
                    Check(legacy.GetArrayElementAtIndex(j).objectReferenceValue == null, "Equipped legacy Artifact is present.", problems);
                var refs = slot.FindPropertyRelative("artifactRefs");
                for (int j = 0; j < refs.arraySize; j++)
                    Check(string.IsNullOrEmpty(refs.GetArrayElementAtIndex(j).FindPropertyRelative("instanceId").stringValue),
                        "Equipped Artifact is present.", problems);
            }
        }
        if (progression != null)
        {
            using var data = new SerializedObject(progression);
            Check(data.FindProperty("metaCurrency").intValue == 0, "Meta currency is present.", problems);
            foreach (string field in new[] { "fragments", "characterPotentials", "unlockedCharacterIds", "permanentItems" })
                CheckEmpty(data, field, problems);
        }
        if (problems.Count > 0)
            throw new BuildFailedException("Authored player assets contain test progress. Build cancelled.\n"
                + string.Join("\n", problems)
                + "\nExit Play mode, then use Tools > Never Night > Save > Clear Authored Test Data. PlayerPrefs are not changed.");
    }

    [MenuItem("Tools/Never Night/Save/Clear Authored Test Data")]
    public static void ClearAuthoredTestData()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play mode before clearing authored test data.");
        var inventory = AssetDatabase.LoadAssetAtPath<PlayerRunInventory>(InventoryPath);
        var progression = AssetDatabase.LoadAssetAtPath<PlayerMetaProgression>(ProgressionPath);
        var main = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
        if (inventory == null || progression == null || main == null || main.characterObjectData == null)
            throw new System.InvalidOperationException("Starting player assets are missing.");
        Undo.RecordObjects(new Object[] { inventory, progression }, "Clear authored test progress");
        using (var data = new SerializedObject(inventory))
        {
            data.FindProperty("currency").intValue = 1000;
            data.FindProperty("_globalLevel").intValue = 1;
            data.FindProperty("activeTeamSlotIndex").intValue = 0;
            foreach (string field in new[] { "artifactInstances", "itemStacks", "runCharacters" })
                data.FindProperty(field).ClearArray();
            var owned = data.FindProperty("ownedItems");
            owned.arraySize = 1;
            owned.GetArrayElementAtIndex(0).objectReferenceValue = main;
            var slots = data.FindProperty("teamSlots");
            slots.arraySize = 2;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("character").objectReferenceValue = i == 0 ? main.characterObjectData : null;
                slot.FindPropertyRelative("legacyArtifacts").ClearArray();
                slot.FindPropertyRelative("artifactRefs").ClearArray();
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        using (var data = new SerializedObject(progression))
        {
            data.FindProperty("metaCurrency").intValue = 0;
            foreach (string field in new[] { "fragments", "characterPotentials", "unlockedCharacterIds", "permanentItems" })
                data.FindProperty(field).ClearArray();
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Validate(inventory, progression);
        EditorUtility.SetDirty(inventory);
        EditorUtility.SetDirty(progression);
        AssetDatabase.SaveAssetIfDirty(inventory);
        AssetDatabase.SaveAssetIfDirty(progression);
        Debug.Log("Authored test progress cleared. Local player saves and settings were preserved.");
    }

    private static void CheckEmpty(SerializedObject data, string field, List<string> problems) =>
        Check(data.FindProperty(field).arraySize == 0, field + " contains test progress.", problems);

    private static void Check(bool valid, string message, List<string> problems)
    {
        if (!valid) problems.Add(message);
    }
}
