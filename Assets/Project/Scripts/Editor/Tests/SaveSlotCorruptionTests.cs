#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using Project.Save;
using Project.ShopSystem;
using UnityEngine;

public class SaveSlotCorruptionTests
{
    private const string MigratedKey = "project.save.slots.migrated";
    private const string LegacyBossRun = "project.player_run_inventory.boss_lobby.v1";
    private readonly Dictionary<string, string> strings = new Dictionary<string, string>();
    private readonly Dictionary<string, int> ints = new Dictionary<string, int>();
    private readonly HashSet<string> existing = new HashSet<string>();

    [SetUp]
    public void SetUp()
    {
        strings.Clear();
        ints.Clear();
        existing.Clear();
        BackupString("project.player_meta_progression.v2");
        BackupString("project.player_meta_progression.v1");
        BackupString(LegacyBossRun);
        BackupString("project.player_meta_progression.boss_lobby.v1");
        BackupInt(MigratedKey);
        for (int slot = 0; slot < SaveSlotStore.SlotCount; slot++)
        {
            BackupString(SaveSlotStore.MetaKey(slot));
            BackupString(SaveSlotStore.BossRunKey(slot));
            BackupString(SaveSlotStore.BossMetaKey(slot));
            BackupString($"project.save.slot{slot}.header");
            BackupInt($"project.save.slot{slot}.occupied");
        }
        PlayerPrefs.SetInt(MigratedKey, 1);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var pair in strings)
        {
            if (existing.Contains(pair.Key)) PlayerPrefs.SetString(pair.Key, pair.Value);
            else PlayerPrefs.DeleteKey(pair.Key);
        }
        foreach (var pair in ints)
        {
            if (existing.Contains(pair.Key)) PlayerPrefs.SetInt(pair.Key, pair.Value);
            else PlayerPrefs.DeleteKey(pair.Key);
        }
        PlayerPrefs.Save();
    }

    [Test]
    public void InvalidHeaderDoesNotPreventReadingHealthySlotOrEraseRawData()
    {
        const string broken = "{broken json";
        PlayerPrefs.SetString("project.save.slot0.header", broken);
        PlayerPrefs.SetInt("project.save.slot0.occupied", 1);
        PlayerPrefs.SetString("project.save.slot1.header", JsonUtility.ToJson(new SaveSlotHeader { currency = 321 }));

        Assert.That(SaveSlotStore.TryReadHeader(0, out var bad), Is.False);
        Assert.That(bad, Is.Null);
        Assert.That(SaveSlotStore.TryReadHeader(1, out var good), Is.True);
        Assert.That(good.currency, Is.EqualTo(321));
        Assert.That(PlayerPrefs.GetString("project.save.slot0.header"), Is.EqualTo(broken));
        Assert.That(SaveSlotStore.IsOccupied(0), Is.True);
    }

    [TestCase("")]
    [TestCase("{broken json")]
    public void InvalidBossPreviewReturnsFalse(string json)
    {
        Assert.That(PlayerRunInventory.TryBuildHeaderFromBossJson(json, out var header), Is.False);
        Assert.That(header, Is.Null);
    }

    [Test]
    public void InvalidLegacyCheckpointDoesNotAbortOrRepeatMigration()
    {
        const string broken = "{broken json";
        PlayerPrefs.DeleteKey(MigratedKey);
        PlayerPrefs.SetString(LegacyBossRun, broken);

        Assert.DoesNotThrow(SaveSlotStore.EnsureMigrated);
        Assert.That(PlayerPrefs.GetInt(MigratedKey), Is.EqualTo(1));
        Assert.That(SaveSlotStore.IsOccupied(0), Is.True);
        Assert.That(SaveSlotStore.TryReadHeader(0, out var header), Is.True);
        Assert.That(header, Is.Not.Null);
        Assert.That(PlayerPrefs.GetString(LegacyBossRun), Is.EqualTo(broken));
        Assert.That(PlayerPrefs.GetString(SaveSlotStore.BossRunKey(0)), Is.EqualTo(broken));
        Assert.DoesNotThrow(SaveSlotStore.EnsureMigrated);
    }

    [Test]
    public void MigrationPreservesExistingHeaderAndOtherSlot()
    {
        const string existingHeader = "{broken existing header";
        string healthyHeader = JsonUtility.ToJson(new SaveSlotHeader { currency = 456 });
        PlayerPrefs.DeleteKey(MigratedKey);
        PlayerPrefs.SetString(LegacyBossRun, "{broken legacy json");
        PlayerPrefs.SetString("project.save.slot0.header", existingHeader);
        PlayerPrefs.SetString("project.save.slot1.header", healthyHeader);
        PlayerPrefs.SetInt("project.save.slot1.occupied", 1);

        Assert.DoesNotThrow(SaveSlotStore.EnsureMigrated);
        Assert.That(PlayerPrefs.GetString("project.save.slot0.header"), Is.EqualTo(existingHeader));
        Assert.That(PlayerPrefs.GetString("project.save.slot1.header"), Is.EqualTo(healthyHeader));
        Assert.That(SaveSlotStore.IsOccupied(1), Is.True);
    }

    private void BackupString(string key)
    {
        if (PlayerPrefs.HasKey(key)) existing.Add(key);
        strings[key] = PlayerPrefs.GetString(key, string.Empty);
        PlayerPrefs.DeleteKey(key);
    }

    private void BackupInt(string key)
    {
        if (PlayerPrefs.HasKey(key)) existing.Add(key);
        ints[key] = PlayerPrefs.GetInt(key, 0);
        PlayerPrefs.DeleteKey(key);
    }
}
#endif
