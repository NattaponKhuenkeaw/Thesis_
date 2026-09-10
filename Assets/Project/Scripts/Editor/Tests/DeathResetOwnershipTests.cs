#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using UnityEngine;
using Object = UnityEngine.Object;

public class DeathResetOwnershipTests
{
    private PlayerRunInventory inventory;
    private CharacterItem mainItem;
    private CharacterObject main;
    private CharacterObject ally;
    private CharacterItem allyItem;
    private readonly List<Object> temporaryObjects = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        inventory = Create<PlayerRunInventory>();
        mainItem = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
        Assert.That(mainItem, Is.Not.Null);
        main = mainItem.characterObjectData;
        Assert.That(main, Is.Not.Null);
        ally = Create<CharacterObject>();
        ally.name = "DeathTestAlly";
        ally.characterId = "death-test-ally";
        ally.stats = new CharacterStats { maxHP = 100f };
        allyItem = Create<CharacterItem>();
        allyItem.id = "death-test-ally-item";
        allyItem.characterObjectData = ally;
        inventory.AddItem(allyItem);
        inventory.DeployTeam(new[] { main, ally }, 0);
        inventory.RememberRunStartTeam(new[] { main, ally }, overwrite: true);
        inventory.SetRunCharacterCurrentHP(main, 100f);
        inventory.SetRunCharacterCurrentHP(ally, 100f);
    }

    [TearDown]
    public void TearDown()
    {
        inventory.ClearInventory();
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(temporaryObjects[i]);
        temporaryObjects.Clear();
    }

    [Test]
    public void MainDefeatWithSurvivorCannotBeReownedRedeployedOrHealed()
    {
        inventory.SetRunCharacterCurrentHP(main, 0f);
        Assert.That(inventory.TryRemoveDefeatedTeamCharacter(main, ally, out int active), Is.True);
        Assert.That(active, Is.Zero);
        Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(ally));
        Assert.That(inventory.IsDeathTeardown, Is.False);

        for (int i = 0; i < 3; i++)
        {
            inventory.LoadPersistentStateIfNeeded();
            inventory.SetRunCharacterCurrentHP(main, 999f);
            inventory.DeployTeam(new[] { main, ally }, 1);
            Assert.That(inventory.CanAddItem(mainItem), Is.False);
            Assert.That(inventory.TryAddItem(mainItem), Is.False);
            Assert.That(Owns(main), Is.False);
            Assert.That(Contains(inventory.GetTeamSnapshot(), main), Is.False);
            Assert.That(inventory.GetRunCharacter(main), Is.Null);
        }

        TeamLoader loader = CreateLoader(main, out PlayerCharacter player);
        loader.SetCharacterCurrentHP(main, 999f);
        Assert.That(player.CurrentHP, Is.Zero);
        Assert.That(loader.GetCharacterCurrentHP(main), Is.Zero);
        Assert.That(inventory.GetRunCharacter(ally).currentHP, Is.EqualTo(100f));
    }

    [Test]
    public void NonMainDefeatWithSurvivorLosesOwnershipImmediately()
    {
        inventory.SetRunCharacterCurrentHP(ally, 0f);
        Assert.That(inventory.TryRemoveDefeatedTeamCharacter(ally, main, out _), Is.True);
        Assert.That(Owns(ally), Is.False);
        Assert.That(Owns(main), Is.True);
        Assert.That(inventory.GetRunCharacter(ally), Is.Null);
        Assert.That(inventory.TryAddItem(allyItem), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WipeThenLobbyThenNewRunNeverRestoresDefeatedAlly(bool mainDiesLast)
    {
        CharacterObject first = mainDiesLast ? ally : main;
        CharacterObject last = mainDiesLast ? main : ally;
        inventory.SetRunCharacterCurrentHP(first, 0f);
        Assert.That(inventory.TryRemoveDefeatedTeamCharacter(first, last, out _), Is.True);
        inventory.SetRunCharacterCurrentHP(last, 0f);
        Assert.That(inventory.TryRemoveDefeatedTeamCharacter(last, null, out int active), Is.False);
        Assert.That(active, Is.EqualTo(-1));
        Assert.That(Owns(main), Is.False);
        Assert.That(Owns(ally), Is.False);
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.Zero);

        inventory.ResetRunProgressForDeath();
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.Zero);
        inventory.EnsureMainCharacterOnLobbyTeam();
        Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(main));
        Assert.That(Owns(main), Is.True);
        Assert.That(Owns(ally), Is.False);
        Assert.That(inventory.TryPrepareForNewRun(1, out string reason), Is.True, reason);
        Assert.That(inventory.IsDeathTeardown, Is.False);
        Assert.That(inventory.IsDeathLobby, Is.False);
        Assert.That(inventory.HasPendingLobbyMainReset, Is.False);
        Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(main));
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.EqualTo(1));
        Assert.That(Owns(main), Is.True);
        Assert.That(Owns(ally), Is.False);
        Assert.That(Contains(inventory.GetRunStartTeamOrCurrent(), ally), Is.False);
    }

    [Test]
    public void TerminalTeamLoaderPathRemovesLastNonMainBeforeWipe()
    {
        inventory.SetRunCharacterCurrentHP(main, 0f);
        inventory.TryRemoveDefeatedTeamCharacter(main, ally, out _);
        inventory.SetRunCharacterCurrentHP(ally, 0f);
        TeamLoader loader = CreateLoader(ally, out _);
        Assert.That(loader.TryBeginActiveDefeatRecovery(), Is.False);
        Assert.That(Owns(ally), Is.False);
        Assert.That(inventory.IsCharacterDefeated(ally), Is.True);
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.Zero);
        Assert.That(loader.GetCharacterCurrentHP(ally), Is.Zero);
    }

    [Test]
    public void TerminalCharacterMissingFromSlotsStillLosesOwnership()
    {
        inventory.DeployTeam(new[] { main }, 0);
        inventory.SetRunCharacterCurrentHP(main, 0f);
        inventory.SetRunCharacterCurrentHP(ally, 0f);
        Assert.That(inventory.TryRemoveDefeatedTeamCharacter(ally, null, out _), Is.False);
        Assert.That(Owns(main), Is.False);
        Assert.That(Owns(ally), Is.False);
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.Zero);
    }

    [Test]
    public void DeathLobbyRestoresPlayableOwnedMainAfterNotificationConsumed()
    {
        inventory.ResetRunProgressForDeath();
        TeamLoader loader = CreateLoader(ally, out PlayerCharacter player);
        loader.RestoreLobbyStateAfterDeath(player);
        Assert.That(player.characterObject, Is.SameAs(main));
        Assert.That(player.CurrentHP, Is.GreaterThan(0f));
        Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP));
        Assert.That(loader.GetCharacterCurrentAE(main), Is.Zero);
        inventory.ConsumeLobbyMainReset();
        inventory.LoadPersistentStateIfNeeded();
        inventory.DeployTeam(inventory.GetTeamSnapshot(), 0);
        Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(main));
        Assert.That(inventory.IsDeathLobby, Is.True);
        Assert.That(Owns(main), Is.True);
        Assert.That(inventory.TryAddItem(mainItem), Is.False);
        Assert.That(inventory.TryPrepareForNewRun(1, out string reason), Is.True, reason);
        loader.DeployLoadout(inventory.GetTeamSnapshot(), 0, resetHealthForNewRun: true);
        Assert.That(Owns(main), Is.True);
        Assert.That(player.characterObject, Is.SameAs(main));
        Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP));
        Assert.That(loader.GetCharacterCurrentAE(main), Is.Zero);
    }

    [Test]
    public void NewRunAlwaysPutsMainInOwnedItems()
    {
        inventory.ResetRunProgressForDeath();
        Assert.That(Owns(main), Is.False);
        inventory.EnsureMainCharacterOnLobbyTeam();
        Assert.That(Owns(main), Is.True);
        Assert.That(inventory.TryPrepareForNewRun(1, out string reason), Is.True, reason);
        Assert.That(Owns(main), Is.True);
        Assert.That(inventory.IsMainCharacter(inventory.GetTeamCharacterAt(0)), Is.True);
        Assert.That(inventory.IsDeathTeardown, Is.False);
        Assert.That(inventory.IsCharacterDefeated(main), Is.False);
    }

    [Test]
    public void RepeatedNewRunPreparationDoesNotDuplicateMainOwnership()
    {
        inventory.ResetRunProgressForDeath();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(inventory.TryPrepareForNewRun(1, out string reason), Is.True, reason);
            Assert.That(inventory.ownedItems.FindAll(item => item == mainItem).Count, Is.EqualTo(1));
            Assert.That(inventory.IsMainCharacter(inventory.GetTeamCharacterAt(0)), Is.True);
        }
    }

    [Test]
    public void FailedNewRunValidationPreservesLobbyMainOwnership()
    {
        inventory.ResetRunProgressForDeath();
        inventory.EnsureMainCharacterOnLobbyTeam();
        Assert.That(inventory.TryPrepareForNewRun(3, out string reason), Is.False);
        Assert.That(reason, Is.Not.Empty);
        Assert.That(inventory.IsDeathLobby, Is.True);
        Assert.That(inventory.IsCharacterDefeated(main), Is.True);
        Assert.That(Owns(main), Is.True);
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.EqualTo(1));
    }

    [Test]
    public void FailedNewRunBeforeLobbyDoesNotRestoreMainDuringTeardown()
    {
        inventory.ResetRunProgressForDeath();
        Assert.That(inventory.TryPrepareForNewRun(3, out _), Is.False);
        Assert.That(Owns(main), Is.False);
        Assert.That(inventory.IsDeathTeardown, Is.True);
        Assert.That(inventory.IsDeathLobby, Is.False);
    }

    [Test]
    public void RepeatedLobbyRestoreKeepsExactlyOneOwnedMain()
    {
        inventory.SetRunCharacterCurrentHP(ally, 0f);
        inventory.TryRemoveDefeatedTeamCharacter(ally, main, out _);
        inventory.ResetRunProgressForDeath();
        Assert.That(Owns(main), Is.False);
        for (int i = 0; i < 3; i++)
        {
            inventory.EnsureMainCharacterOnLobbyTeam();
            inventory.ConsumeLobbyMainReset();
            inventory.LoadPersistentStateIfNeeded();
            inventory.DeployTeam(inventory.GetTeamSnapshot(), 0);
            Assert.That(inventory.ownedItems.FindAll(item => item == mainItem).Count, Is.EqualTo(1));
            Assert.That(Owns(ally), Is.False);
            Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(main));
        }
    }

    [Test]
    public void NewRunUsesRemainingOwnershipWithoutRestoringSnapshotTeamInLobby()
    {
        inventory.ResetRunProgressForDeath();
        Assert.That(Owns(ally), Is.True);
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.Zero);
        inventory.EnsureMainCharacterOnLobbyTeam();
        Assert.That(inventory.GetDeployedTeamMemberCount(), Is.EqualTo(1));
        Assert.That(inventory.TryPrepareForNewRun(2, out string reason), Is.True, reason);
        Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(main));
        Assert.That(inventory.GetTeamCharacterAt(1), Is.SameAs(ally));
    }

    [Test]
    public void OwnedLivingCharactersStillDeployAfterDeathTeardown()
    {
        inventory.ResetRunProgressForDeath();
        Assert.That(inventory.IsDeathTeardown, Is.True);
        Assert.That(inventory.IsDeathLobby, Is.False);
        Assert.That(Owns(ally), Is.True);

        inventory.DeployTeam(new[] { ally }, 0);
        Assert.That(inventory.GetTeamCharacterAt(0), Is.SameAs(ally));
        Assert.That(Owns(ally), Is.True);
    }

    [Test]
    public void DeathLobbyDeployKeepsOwnedSurvivorInTeamSlots()
    {
        inventory.ResetRunProgressForDeath();
        inventory.EnsureMainCharacterOnLobbyTeam();
        inventory.DeployTeam(new[] { main, ally }, 0);
        Assert.That(inventory.IsMainCharacter(inventory.GetTeamCharacterAt(0)), Is.True);
        Assert.That(inventory.GetTeamCharacterAt(1), Is.SameAs(ally));
        Assert.That(Owns(ally), Is.True);
        Assert.That(Owns(main), Is.True);
    }

    [Test]
    public void ResetStillDropsRunAcquiredCharacters()
    {
        CharacterObject pickup = Create<CharacterObject>();
        pickup.characterId = "death-test-pickup";
        CharacterItem item = Create<CharacterItem>();
        item.characterObjectData = pickup;
        inventory.AddItem(item);
        inventory.ResetRunProgressForDeath();
        Assert.That(Owns(pickup), Is.False);
        Assert.That(Owns(ally), Is.True);
    }

    [Test]
    public void MainIdentityAliasCannotEscapeDefeatRemoval()
    {
        CharacterObject alias = Create<CharacterObject>();
        alias.name = "CharacterObject_main";
        alias.characterId = "alias-main";
        CharacterItem item = Create<CharacterItem>();
        item.characterObjectData = alias;
        inventory.ownedItems.Add(item);
        inventory.SetRunCharacterCurrentHP(main, 0f);
        inventory.TryRemoveDefeatedTeamCharacter(main, ally, out _);
        Assert.That(Owns(alias), Is.False);
        Assert.That(inventory.IsCharacterDefeated(alias), Is.True);
        Assert.That(inventory.GetRunCharacter(alias), Is.Null);
    }

    [Test]
    public void LobbyMainItemCannotRestoreDefeatedMainInNextRunDraft()
    {
        inventory.ResetRunProgressForDeath();
        inventory.EnsureMainCharacterOnLobbyTeam();
        TeamDraftController draft = new TeamDraftController(inventory, null, Array.Empty<TeamSlotUI>());
        CharacterItem lobbyItem = draft.FindCharacterItem(main);
        Assert.That(lobbyItem, Is.SameAs(mainItem));
        Assert.That(Owns(main), Is.True);
        Assert.That(inventory.TryPrepareForNewRun(2, out string reason), Is.True, reason);
        inventory.SetRunCharacterCurrentHP(main, 0f);
        inventory.TryRemoveDefeatedTeamCharacter(main, ally, out _);
        Assert.That(draft.FindCharacterItem(main), Is.Null);
        Assert.That(Owns(main), Is.False);
    }

    [Test]
    public void FreshInventoryStillBootstrapsMainAndRejectsUnownedDeployment()
    {
        PlayerRunInventory fresh = Create<PlayerRunInventory>();
        CharacterObject unowned = Create<CharacterObject>();
        unowned.characterId = "unowned-deploy";
        fresh.DeployTeam(new[] { unowned }, 0);
        Assert.That(fresh.HasMainCharacterDeployed(), Is.True);
        Assert.That(Contains(fresh.GetTeamSnapshot(), unowned), Is.False);
    }

    private TeamLoader CreateLoader(CharacterObject character, out PlayerCharacter player)
    {
        GameObject host = new GameObject("DeathOwnershipTestHost");
        host.SetActive(false);
        temporaryObjects.Add(host);
        player = host.AddComponent<PlayerCharacter>();
        player.characterObject = character;
        TeamLoader loader = host.AddComponent<TeamLoader>();
        typeof(TeamLoader).GetField("runInventory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, inventory);
        loader.playerCharacter = player;
        return loader;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        temporaryObjects.Add(value);
        return value;
    }

    private bool Owns(CharacterObject character)
    {
        return inventory.ownedItems.Exists(item => item is CharacterItem characterItem
            && characterItem.characterObjectData == character);
    }

    private static bool Contains(CharacterObject[] team, CharacterObject character)
    {
        return Array.Exists(team, member => member == character);
    }
}
#endif
