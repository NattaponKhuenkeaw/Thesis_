#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using CombatCore;
using NUnit.Framework;
using Project.Progression;
using Project.ShopSystem;
using Project.Training;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;
using Project.Save;
using Unity.Behavior;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TrainingSessionTests
{
    private TrainingInventoryState _inventory;
    private TrainingProgressionState _meta;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        _inventory = ScriptableObject.CreateInstance<TrainingInventoryState>();
        _meta = ScriptableObject.CreateInstance<TrainingProgressionState>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_meta);
        Object.DestroyImmediate(_inventory);
    }

    [Test]
    public void PracticeStateDoesNotUseSaveAssetTypes()
    {
        Assert.That(typeof(PlayerRunInventory).IsAssignableFrom(_inventory.GetType()), Is.False);
        Assert.That(typeof(PlayerMetaProgression).IsAssignableFrom(_meta.GetType()), Is.False);
        Assert.That(typeof(TrainingSession).GetFields(Private).Any(f =>
            f.FieldType == typeof(PlayerRunInventory) || f.FieldType == typeof(PlayerMetaProgression)), Is.False);
    }

    [Test]
    public void PracticeLevelCanIncreaseDecreaseAndStartsFreshEachTime()
    {
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(1));
        _inventory.SetLevel(_inventory.GlobalMaxLevel);
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(_inventory.GlobalMaxLevel));
        _inventory.SetLevel(1);
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(1));
        var next = ScriptableObject.CreateInstance<TrainingInventoryState>();
        try { Assert.That(next.GlobalLevel, Is.EqualTo(1)); }
        finally { Object.DestroyImmediate(next); }
    }

    [Test]
    public void PracticePotentialsAreIndependentAndCopyConfigurationValues()
    {
        var saved = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        try
        {
            saved.SetPotentialLevel("training-test", PotentialTrack.Skill, 3);
            string before = saved.ExportJson();
            Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.Zero);
            var values = new List<TrainingConfiguration.CharacterPotential>
            {
                new TrainingConfiguration.CharacterPotential { characterId = "training-test", skill = 2 }
            };
            _meta.SetPotentials(values);
            values[0].skill = 0;
            Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.EqualTo(2));
            _meta.SetPotentials(values);
            Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.Zero);
            Assert.That(saved.ExportJson(), Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(saved); }
    }

    [Test]
    public void PracticeCatalogIsCompleteIdempotentAndDoesNotChangeSavedInventory()
    {
        var saved = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            saved.AddGlobalLevels(7);
            string before = JsonUtility.ToJson(saved);
            var catalog = Resources.LoadAll<BaseItem>("ItemSO");
            _inventory.PopulateCatalog(catalog);
            foreach (var item in catalog)
            {
                if (item is CharacterItem character && character.characterObjectData != null)
                    Assert.That(_inventory.ownedItems.OfType<CharacterItem>().Any(c =>
                        c.characterObjectData.characterId == character.characterObjectData.characterId), Is.True);
                if (item is ArtifactDefinition definition && !string.IsNullOrWhiteSpace(definition.id))
                    Assert.That(_inventory.ArtifactInstances.Any(a => a.definitionId == definition.id), Is.True);
            }
            int characters = _inventory.ownedItems.OfType<CharacterItem>().Count();
            int artifacts = _inventory.ArtifactInstances.Count;
            Assert.That(artifacts, Is.GreaterThan(0));
            _inventory.PopulateCatalog(catalog);
            Assert.That(_inventory.ownedItems.OfType<CharacterItem>().Count(), Is.EqualTo(characters));
            Assert.That(_inventory.ArtifactInstances.Count, Is.EqualTo(artifacts));
            Assert.That(saved.GlobalLevel, Is.EqualTo(8));
            Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(saved); }
    }

    [Test]
    public void PracticeStateCannotWriteSaveKeys()
    {
        string[] keys = { SaveSlotStore.ActiveMetaKey, SaveSlotStore.ActiveBossRunKey, SaveSlotStore.ActiveBossMetaKey,
            "project.player_run_inventory.v3", "project.player_run_inventory.v2" };
        var before = keys.ToDictionary(k => k, k => PlayerPrefs.GetString(k, "<absent>"));
        _inventory.SetLevel(10);
        _meta.SetPotentialLevel("training-test", PotentialTrack.Skill, 2);
        _inventory.SavePersistentState();
        _inventory.SaveBossLobbyCheckpoint();
        _meta.SavePersistentState();
        foreach (var key in keys) Assert.That(PlayerPrefs.GetString(key, "<absent>"), Is.EqualTo(before[key]), key);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TrainingCollectsDamageWithoutColliderDrawings(bool developerDebug)
    {
        var field = typeof(TrainingDebugManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = field.GetValue(null);
        var host = new GameObject("Training presentation test");
        try
        {
            var manager = host.AddComponent<TrainingDebugManager>();
            field.SetValue(null, manager);
            manager.showDebugVisualization = developerDebug;
            manager.SetTrainingPresentation(true);
            Assert.That(TrainingDebugManager.CollectStats, Is.True);
            Assert.That(TrainingDebugManager.ShowStatsHud, Is.False);
            manager.ToggleTrainingStats();
            Assert.That(TrainingDebugManager.ShowStatsHud, Is.True);
            manager.ToggleTrainingStats();
            Assert.That(TrainingDebugManager.ShowStatsHud, Is.False);
            Assert.That(TrainingDebugManager.CollectStats, Is.True);
            Assert.That(TrainingDebugManager.DrawColliders, Is.False);
            manager.SetTrainingPresentation(false);
            Assert.That(TrainingDebugManager.DrawColliders, Is.EqualTo(developerDebug));
        }
        finally { Object.DestroyImmediate(host); field.SetValue(null, previous); }
    }

    [Test]
    public void PassiveOrDisabledBrainCannotBeReactivatedByRecovery()
    {
        var host = new GameObject("Training AI test");
        try
        {
            var brain = host.AddComponent<EnemyBehaviorBrain>();
            var permission = typeof(EnemyBehaviorBrain).GetField("enableAgentWhenNoBossDriver", Private);
            var agent = host.GetComponent<BehaviorGraphAgent>();
            permission.SetValue(brain, false);
            brain.SetAgentActiveForGameplay(true);
            Assert.That(agent.enabled, Is.False);
            brain.SetAgentActive(true);
            Assert.That(agent.enabled, Is.False);
            permission.SetValue(brain, true);
            brain.enabled = false;
            brain.SetAgentActiveForGameplay(true);
            Assert.That(agent.enabled, Is.False);
            brain.enabled = true;
            brain.SetAgentActiveForGameplay(true);
            Assert.That(agent.enabled, Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }
}
#endif
