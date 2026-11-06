#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Progression;
using Project.ShopSystem;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public class CleanSaveBuildGuardTests
{
    private PlayerRunInventory inventory;
    private PlayerMetaProgression progression;

    [SetUp]
    public void SetUp()
    {
        inventory = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerRunInventory>(CleanSaveBuildGuard.InventoryPath));
        progression = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerMetaProgression>(CleanSaveBuildGuard.ProgressionPath));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(progression);
    }

    [Test]
    public void ShippedAssetsHaveOnlyFreshMainStartingData()
    {
        Assert.DoesNotThrow(() => new CleanSaveBuildGuard().OnPreprocessBuild(null));
        Assert.DoesNotThrow(() => CleanSaveBuildGuard.Validate(inventory, progression));
    }

    [TestCase("currency", 1263)]
    [TestCase("_globalLevel", 10)]
    [TestCase("activeTeamSlotIndex", 1)]
    public void BuildRejectsProgressWithoutSilentlyResettingIt(string field, int value)
    {
        using (var data = new SerializedObject(inventory))
        {
            data.FindProperty(field).intValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        string before = EditorJsonUtility.ToJson(inventory);
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(inventory, progression));
        Assert.That(EditorJsonUtility.ToJson(inventory), Is.EqualTo(before));
    }

    [TestCase("artifactInstances")]
    [TestCase("itemStacks")]
    [TestCase("ownedItems")]
    public void BuildRejectsLootAndAdditionalCharacters(string field)
    {
        using (var data = new SerializedObject(inventory))
        {
            data.FindProperty(field).arraySize += 1;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(inventory, progression));
    }

    [TestCase("fragments")]
    [TestCase("characterPotentials")]
    [TestCase("unlockedCharacterIds")]
    [TestCase("permanentItems")]
    public void BuildRejectsMetaProgress(string field)
    {
        using (var data = new SerializedObject(progression))
        {
            data.FindProperty(field).arraySize = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(inventory, progression));
    }

    [Test]
    public void BuildRejectsMissingAssets()
    {
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(null, progression));
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(inventory, null));
    }

    [TestCase("currentHP")]
    [TestCase("currentAE")]
    public void BuildRejectsSavedCombatVitals(string field)
    {
        inventory.ResetRunStateForMenuStart();
        using (var data = new SerializedObject(inventory))
        {
            var character = data.FindProperty("runCharacters").GetArrayElementAtIndex(0);
            character.FindPropertyRelative(field).floatValue = 25f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(inventory, progression));
    }

    [Test]
    public void BuildRejectsAnEquippedArtifactReference()
    {
        using (var data = new SerializedObject(inventory))
        {
            var refs = data.FindProperty("teamSlots").GetArrayElementAtIndex(0).FindPropertyRelative("artifactRefs");
            refs.arraySize = 1;
            refs.GetArrayElementAtIndex(0).FindPropertyRelative("instanceId").stringValue = "test-loot";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.Throws<BuildFailedException>(() => CleanSaveBuildGuard.Validate(inventory, progression));
    }

    [Test]
    public void NewGameStartsCleanAndKeepsTheAuthoredRollTableAndLevelCap()
    {
        Object rollTable;
        int levelCap;
        using (var data = new SerializedObject(inventory))
        {
            rollTable = data.FindProperty("artifactRollTable").objectReferenceValue;
            levelCap = data.FindProperty("_globalMaxLevel").intValue;
            data.FindProperty("currency").intValue = 1263;
            data.FindProperty("_globalLevel").intValue = 10;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        inventory.ResetRunStateForMenuStart();
        Assert.DoesNotThrow(() => CleanSaveBuildGuard.Validate(inventory, progression));
        using var clean = new SerializedObject(inventory);
        Assert.That(clean.FindProperty("artifactRollTable").objectReferenceValue, Is.SameAs(rollTable));
        Assert.That(clean.FindProperty("_globalMaxLevel").intValue, Is.EqualTo(levelCap));
    }
}
#endif
