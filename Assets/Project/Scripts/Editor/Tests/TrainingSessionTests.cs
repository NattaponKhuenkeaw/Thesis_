#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using CombatCore;
using NUnit.Framework;
using Project.Progression;
using Project.ShopSystem;
using Project.Training;
using Unity.Behavior;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TrainingSessionTests
{
    private PlayerRunInventory _inventory;
    private PlayerMetaProgression _meta;
    private readonly object _owner = new object();
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        _inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        _meta = ScriptableObject.CreateInstance<PlayerMetaProgression>();
    }

    [TearDown]
    public void TearDown()
    {
        _meta.EndTrainingSession(_owner);
        _inventory.EndTrainingSession(_owner);
        Object.DestroyImmediate(_meta);
        Object.DestroyImmediate(_inventory);
    }

    [Test]
    public void LevelOverrideCanDecreaseAndNeverChangesPersistentLevel()
    {
        int original = _inventory.GlobalLevel;
        _inventory.BeginTrainingSession(_owner);
        _inventory.SetTrainingLevelOverride(_owner, _inventory.GlobalMaxLevel);
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(_inventory.GlobalMaxLevel));
        Assert.That((int)typeof(PlayerRunInventory).GetField("_globalLevel", Private).GetValue(_inventory), Is.EqualTo(original));
        _inventory.SetTrainingLevelOverride(_owner, 1);
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(1));
        _inventory.EndTrainingSession(_owner);
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(original));
    }

    [Test]
    public void OnlyOwnerCanChangeOrEndAnOverride()
    {
        _inventory.BeginTrainingSession(_owner);
        _inventory.SetTrainingLevelOverride(_owner, 7);
        Assert.Throws<InvalidOperationException>(() => _inventory.SetTrainingLevelOverride(new object(), 2));
        _inventory.EndTrainingSession(new object());
        Assert.That(_inventory.GlobalLevel, Is.EqualTo(7));
        Assert.Throws<InvalidOperationException>(() => _meta.SetTrainingPotentialOverrides(new object(), null));
    }

    [Test]
    public void PotentialOverridesDoNotChangeSaveExportsAndRestoreOnEnd()
    {
        _meta.SetPotentialLevel("training-test", PotentialTrack.Skill, 1);
        string original = _meta.ExportJson();
        _meta.BeginTrainingSession(_owner);
        var values = new List<TrainingConfiguration.CharacterPotential>
        {
            new TrainingConfiguration.CharacterPotential { characterId = "training-test", skill = 3, ultimate = 2 }
        };
        _meta.SetTrainingPotentialOverrides(_owner, values);
        Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.EqualTo(3));
        Assert.That(_meta.ExportJson(), Is.EqualTo(original));
        values[0].skill = 0;
        Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.EqualTo(3), "Caller cannot mutate staged values.");
        _meta.SetTrainingPotentialOverrides(_owner, values);
        Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.Zero);
        _meta.EndTrainingSession(_owner);
        Assert.That(_meta.GetPotentialLevel("training-test", PotentialTrack.Skill), Is.EqualTo(1));
        Assert.That(_meta.ExportJson(), Is.EqualTo(original));
    }

    [Test]
    public void ReducingPotentialInvalidatesUnlockedNodeCache()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
        Assert.That(character, Is.Not.Null);
        _meta.BeginTrainingSession(_owner);
        _meta.SetTrainingPotentialOverrides(_owner, new[]
        {
            new TrainingConfiguration.CharacterPotential { characterId = character.characterId, skill = CharacterKitDefinition.MaxPotentialLevel,
                ultimate = CharacterKitDefinition.MaxPotentialLevel, passive = CharacterKitDefinition.MaxPotentialLevel }
        });
        var full = _meta.GetUnlockedPotentialNodes(character);
        _meta.SetTrainingPotentialOverrides(_owner, new[] { new TrainingConfiguration.CharacterPotential { characterId = character.characterId } });
        var reduced = _meta.GetUnlockedPotentialNodes(character);
        Assert.That(reduced.Count, Is.LessThan(full.Count));
        foreach (var node in reduced) Assert.That(node.unlockedByDefault || node.level <= 0, Is.True);
    }

    [Test]
    public void SessionRestoresMetaDataOmittedFromTheSaveSchema()
    {
        var items = (List<PermanentItemStack>)typeof(PlayerMetaProgression).GetField("permanentItems", Private).GetValue(_meta);
        items.Add(new PermanentItemStack { itemId = "legacy-test", displayName = "Legacy item", quantity = 2 });
        _meta.BeginTrainingSession(_owner);
        items.Clear();
        _meta.EndTrainingSession(_owner);
        Assert.That(_meta.PermanentItems.Count, Is.EqualTo(1));
        Assert.That(_meta.PermanentItems[0].quantity, Is.EqualTo(2));
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
