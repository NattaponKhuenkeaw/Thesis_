using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEngine;

public class RunCharacterStateTests
{
    [Test]
    public void CharacterRunUpgradeStartsFromDefinitionGrowthButLevelOne()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.upgrade.level = 12;
        character.upgrade.currentExp = 34;
        character.upgrade.maxLevel = 40;
        character.upgrade.attackPerLevel = 2f;

        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        characterItem.characterObjectData = character;

        inventory.TryAddItem(characterItem);
        CharacterUpgrade runUpgrade = inventory.GetCharacterUpgrade(character);

        Assert.That(runUpgrade, Is.Not.Null);
        Assert.That(runUpgrade.CurrentLevel, Is.EqualTo(1));
        Assert.That(runUpgrade.currentExp, Is.EqualTo(0));
        Assert.That(runUpgrade.MaxLevel, Is.EqualTo(40));
        Assert.That(runUpgrade.attackPerLevel, Is.EqualTo(2f));
        Assert.That(character.upgrade.CurrentLevel, Is.EqualTo(12));
        Assert.That(character.upgrade.currentExp, Is.EqualTo(34));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void RunUpgradeMutationDoesNotMutateCharacterDefinition()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.upgrade.level = 1;
        character.upgrade.currentExp = 0;
        character.upgrade.attackPerLevel = 1f;

        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        characterItem.characterObjectData = character;

        inventory.TryAddItem(characterItem);
        CharacterUpgrade runUpgrade = inventory.GetCharacterUpgrade(character);
        runUpgrade.AddExp(CharacterUpgrade.GetRequiredEXP(1));

        Assert.That(runUpgrade.CurrentLevel, Is.EqualTo(2));
        Assert.That(character.upgrade.CurrentLevel, Is.EqualTo(1));
        Assert.That(character.upgrade.currentExp, Is.EqualTo(0));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void CharacterStatsResolveFromRunUpgradeWithoutMutatingDefinition()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.stats.attack = 10f;
        character.stats.defense = 5f;
        character.stats.maxHP = 100f;
        character.upgrade.level = 1;
        character.upgrade.currentExp = 0;
        character.upgrade.attackPerLevel = 3f;
        character.upgrade.defensePerLevel = 2f;
        character.upgrade.maxHpPerLevel = 20f;

        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        characterItem.characterObjectData = character;

        inventory.TryAddItem(characterItem);
        CharacterUpgrade runUpgrade = inventory.GetCharacterUpgrade(character);
        runUpgrade.AddExp(CharacterUpgrade.GetRequiredEXP(1));

        CharacterStats runStats = inventory.GetCharacterStats(character);
        CharacterStats definitionStats = character.GetLeveledStats();

        Assert.That(runStats.attack, Is.EqualTo(13f));
        Assert.That(runStats.defense, Is.EqualTo(7f));
        Assert.That(runStats.maxHP, Is.EqualTo(120f));
        Assert.That(definitionStats.attack, Is.EqualTo(10f));
        Assert.That(definitionStats.defense, Is.EqualTo(5f));
        Assert.That(definitionStats.maxHP, Is.EqualTo(100f));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void DeployedTeamSlotResolvesRunCharacterState()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        inventory.DeployTeam(new[] { character }, 0);

        RunCharacterState state = inventory.GetTeamRunCharacterAt(0);

        Assert.That(state, Is.Not.Null);
        Assert.That(state.character, Is.SameAs(character));
        Assert.That(inventory.GetTeamSnapshot()[0], Is.SameAs(character));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(character);
    }

    [Test]
    public void RuntimeEffectStatBonusAppliesAndExpiresFromRunState()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            character.stats.attack = 100f;
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 1f;
            effect.statModifiers.Add(new RuntimeEffectStatModifier
            {
                target = RuntimeEffectStatTarget.Attack,
                operation = RuntimeEffectStatOperation.AddSourceStatRatio,
                sourceStat = RuntimeEffectStatTarget.Attack,
                value = 0.2f
            });

            inventory.DeployTeam(new[] { character }, 0);
            inventory.ApplyRuntimeEffect(character, effect, character, character.stats);

            Assert.That(inventory.GetCharacterStats(character).attack, Is.EqualTo(120f).Within(0.001f));
            Assert.That(character.GetLeveledStats().attack, Is.EqualTo(100f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));

            inventory.TickRuntimeEffects(1.1f);

            Assert.That(inventory.GetCharacterStats(character).attack, Is.EqualTo(100f).Within(0.001f));
            Assert.That(inventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectTimerTicksForOffFieldTeamMembers()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject activeCharacter = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject offFieldCharacter = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 1f;

            inventory.DeployTeam(new[] { activeCharacter, offFieldCharacter }, 0);
            inventory.ApplyRuntimeEffect(activeCharacter, effect, activeCharacter, activeCharacter.stats);
            inventory.ApplyRuntimeEffect(offFieldCharacter, effect, activeCharacter, activeCharacter.stats);

            Assert.That(inventory.GetActiveRuntimeEffects(activeCharacter).Count, Is.EqualTo(1));
            Assert.That(inventory.GetActiveRuntimeEffects(offFieldCharacter).Count, Is.EqualTo(1));

            inventory.TickRuntimeEffects(1.1f);

            Assert.That(inventory.GetActiveRuntimeEffects(activeCharacter).Count, Is.EqualTo(0));
            Assert.That(inventory.GetActiveRuntimeEffects(offFieldCharacter).Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(offFieldCharacter);
            Object.DestroyImmediate(activeCharacter);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void RuntimeEffectInstance_TracksAppliedDurationOverrideForUiFill()
    {
        StatRuntimeEffectDefinition effect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        RuntimeEffectInstance instance = new RuntimeEffectInstance();

        try
        {
            effect.durationType = RuntimeEffectDurationType.Seconds;
            effect.durationSeconds = 10f;

            instance.Initialize(effect, character, null, 5f);

            Assert.That(instance.remainingSeconds, Is.EqualTo(5f).Within(0.001f));
            Assert.That(instance.appliedDurationSeconds, Is.EqualTo(5f).Within(0.001f));

            instance.Refresh(2.5f);

            Assert.That(instance.remainingSeconds, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(instance.appliedDurationSeconds, Is.EqualTo(2.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(effect);
        }
    }
}
