using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using UnityEngine;

public class InventoryAcquisitionResolverTests
{
    [Test]
    public void ConsumableAcquisitionAddsRequestedQuantity()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        PlayerMetaProgression metaProgression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        Item potion = ScriptableObject.CreateInstance<Item>();
        potion.id = "level-potion";
        potion.itemName = "Level Potion";
        potion.maxStackSize = 99;

        AcquisitionResult result = InventoryAcquisitionResolver.Acquire(potion, inventory, metaProgression, 10);

        Assert.That(result.Kind, Is.EqualTo(AcquisitionKind.AddedRunItem));
        Assert.That(inventory.GetItemCount(potion), Is.EqualTo(10));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(metaProgression);
        Object.DestroyImmediate(potion);
    }

    [Test]
    public void DuplicateCharacterConvertsToPotentialFragments()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        PlayerMetaProgression metaProgression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        CharacterObject characterDefinition = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterItem character = ScriptableObject.CreateInstance<CharacterItem>();
        character.id = "test-character";
        character.itemName = "Test Character";
        character.characterObjectData = characterDefinition;

        AcquisitionResult firstResult = InventoryAcquisitionResolver.Acquire(character, inventory, metaProgression);
        AcquisitionResult duplicateResult = InventoryAcquisitionResolver.Acquire(character, inventory, metaProgression);

        Assert.That(firstResult.Kind, Is.EqualTo(AcquisitionKind.AddedRunCharacter));
        Assert.That(duplicateResult.Kind, Is.EqualTo(AcquisitionKind.ConvertedToFragments));
        Assert.That(inventory.GetItemCount(character), Is.EqualTo(1));
        Assert.That(inventory.GetRunCharacter(character.characterObjectData), Is.Not.Null);
        Assert.That(metaProgression.GetFragmentCount(character), Is.EqualTo(1));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(metaProgression);
        Object.DestroyImmediate(characterDefinition);
        Object.DestroyImmediate(character);
    }

    [Test]
    public void DuplicateArtifactConvertsToTranscendenceFragments()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        PlayerMetaProgression metaProgression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        Artifact artifact = ScriptableObject.CreateInstance<Artifact>();
        artifact.id = "test-artifact";
        artifact.itemName = "Test Artifact";

        AcquisitionResult firstResult = InventoryAcquisitionResolver.Acquire(artifact, inventory, metaProgression);
        AcquisitionResult duplicateResult = InventoryAcquisitionResolver.Acquire(artifact, inventory, metaProgression);

        Assert.That(firstResult.Kind, Is.EqualTo(AcquisitionKind.AddedRunArtifact));
        Assert.That(duplicateResult.Kind, Is.EqualTo(AcquisitionKind.ConvertedToFragments));
        Assert.That(inventory.GetItemCount(artifact), Is.EqualTo(1));
        Assert.That(metaProgression.GetFragmentCount(artifact), Is.EqualTo(1));

        Object.DestroyImmediate(inventory);
        Object.DestroyImmediate(metaProgression);
        Object.DestroyImmediate(artifact);
    }

    [Test]
    public void PotentialUpgradeSpendsFragmentsAndCapsTrack()
    {
        PlayerMetaProgression metaProgression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        CharacterObject characterDefinition = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();

        characterDefinition.characterId = "potential-character";
        characterDefinition.kitDefinition.potentialNodes.Add(new PotentialNodeDefinition
        {
            nodeId = "potential-character.skill.lv1",
            track = PotentialTrack.Skill,
            level = 1,
            fragmentCost = 2
        });
        characterItem.id = characterDefinition.characterId;
        characterItem.itemName = "Potential Character";
        characterItem.characterObjectData = characterDefinition;
        metaProgression.AddFragments(characterItem, 10);

        Assert.That(metaProgression.TryUpgradePotential(characterDefinition, PotentialTrack.Skill), Is.True);
        Assert.That(metaProgression.GetPotentialLevel(characterDefinition, PotentialTrack.Skill), Is.EqualTo(1));
        Assert.That(metaProgression.GetFragmentCount(characterItem), Is.EqualTo(8));

        Assert.That(metaProgression.TryUpgradePotential(characterDefinition, PotentialTrack.Skill), Is.True);
        Assert.That(metaProgression.TryUpgradePotential(characterDefinition, PotentialTrack.Skill), Is.True);
        Assert.That(metaProgression.TryUpgradePotential(characterDefinition, PotentialTrack.Skill), Is.True);
        Assert.That(metaProgression.GetPotentialLevel(characterDefinition, PotentialTrack.Skill), Is.EqualTo(4));
        Assert.That(metaProgression.TryUpgradePotential(characterDefinition, PotentialTrack.Skill), Is.False);

        Object.DestroyImmediate(metaProgression);
        Object.DestroyImmediate(characterDefinition);
        Object.DestroyImmediate(characterItem);
    }

    [Test]
    public void MetaProgressionJsonRoundTripPreservesFragmentsAndPotential()
    {
        PlayerMetaProgression source = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        PlayerMetaProgression restored = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        CharacterObject characterDefinition = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();

        characterDefinition.characterId = "roundtrip-character";
        characterItem.id = characterDefinition.characterId;
        characterItem.itemName = "Roundtrip Character";
        characterItem.characterObjectData = characterDefinition;

        source.AddFragments(characterItem, 4);
        source.SetPotentialLevel(characterDefinition, PotentialTrack.Ultimate, 3);
        string json = source.ExportJson();

        restored.ImportJson(json);

        Assert.That(restored.GetFragmentCount(characterItem), Is.EqualTo(4));
        Assert.That(restored.GetPotentialLevel(characterDefinition, PotentialTrack.Ultimate), Is.EqualTo(3));

        Object.DestroyImmediate(source);
        Object.DestroyImmediate(restored);
        Object.DestroyImmediate(characterDefinition);
        Object.DestroyImmediate(characterItem);
    }
}
