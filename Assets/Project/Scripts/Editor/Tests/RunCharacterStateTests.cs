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
}
