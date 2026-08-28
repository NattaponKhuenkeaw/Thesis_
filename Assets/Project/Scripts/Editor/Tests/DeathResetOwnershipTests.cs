#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEngine;

public class DeathResetOwnershipTests
{
    [Test]
    public void DeathResetDropsGhostSnapshotCharactersWithoutItemsAndDeploysMain()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject ghost = ScriptableObject.CreateInstance<CharacterObject>();
        ghost.name = "CharacterObject_1st";
        ghost.characterId = "char-test-ghost";
        try
        {
            inventory.RememberRunStartTeam(new[] { ghost }, overwrite: true);
            inventory.ResetRunProgressForDeath();

            CharacterObject[] team = inventory.GetTeamSnapshot();
            Assert.That(inventory.HasMainCharacterDeployed(), Is.True);
            Assert.That(inventory.IsMainCharacter(team[inventory.GetMainCharacterSlotIndex()]), Is.True);
            Assert.That(TeamContains(team, ghost), Is.False);
            Assert.That(OwnsCharacterItem(inventory, ghost), Is.False);
            Assert.That(OwnsMainCharacterItem(inventory), Is.True);
        }
        finally
        {
            inventory.ConsumeLobbyMainReset();
            inventory.ClearInventory();
            Object.DestroyImmediate(ghost);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void DeathResetKeepsOwnedLobbyTeammateAndOwnsMain()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject ally = ScriptableObject.CreateInstance<CharacterObject>();
        ally.name = "CharacterObject_2nd";
        ally.characterId = "char-test-ally";
        CharacterItem allyItem = ScriptableObject.CreateInstance<CharacterItem>();
        allyItem.characterObjectData = ally;
        try
        {
            inventory.ownedItems.Add(allyItem);
            inventory.RememberRunStartTeam(new[] { ally }, overwrite: true);
            inventory.ResetRunProgressForDeath();

            CharacterObject[] team = inventory.GetTeamSnapshot();
            Assert.That(inventory.IsMainCharacter(team[inventory.GetMainCharacterSlotIndex()]), Is.True);
            Assert.That(TeamContains(team, ally), Is.True);
            Assert.That(OwnsCharacterItem(inventory, ally), Is.True);
            Assert.That(OwnsMainCharacterItem(inventory), Is.True);
        }
        finally
        {
            inventory.ConsumeLobbyMainReset();
            inventory.ClearInventory();
            Object.DestroyImmediate(allyItem);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(inventory);
        }
    }

    [Test]
    public void DeployTeamStripsCharacterObjectsThatHaveNoOwnedItem()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject ghost = ScriptableObject.CreateInstance<CharacterObject>();
        ghost.name = "CharacterObject_1st";
        ghost.characterId = "char-test-ghost-deploy";
        try
        {
            inventory.DeployTeam(new[] { ghost }, preferredIndex: 0);

            CharacterObject[] team = inventory.GetTeamSnapshot();
            Assert.That(TeamContains(team, ghost), Is.False);
            Assert.That(inventory.HasMainCharacterDeployed(), Is.True);
            Assert.That(OwnsCharacterItem(inventory, ghost), Is.False);
        }
        finally
        {
            inventory.ClearInventory();
            Object.DestroyImmediate(ghost);
            Object.DestroyImmediate(inventory);
        }
    }

    private static bool TeamContains(CharacterObject[] team, CharacterObject character)
    {
        if (team == null || character == null)
        {
            return false;
        }

        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] == character)
            {
                return true;
            }

            if (team[i] != null
                && !string.IsNullOrWhiteSpace(character.characterId)
                && string.Equals(team[i].characterId, character.characterId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OwnsCharacterItem(PlayerRunInventory inventory, CharacterObject character)
    {
        for (int i = 0; i < inventory.ownedItems.Count; i++)
        {
            if (inventory.ownedItems[i] is CharacterItem characterItem
                && characterItem.characterObjectData == character)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OwnsMainCharacterItem(PlayerRunInventory inventory)
    {
        for (int i = 0; i < inventory.ownedItems.Count; i++)
        {
            if (inventory.ownedItems[i] is CharacterItem characterItem
                && inventory.IsMainCharacter(characterItem.characterObjectData))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
