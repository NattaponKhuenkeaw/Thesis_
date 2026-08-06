#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Progression;
using Project.ShopSystem;
using UnityEngine;

public class RunGlobalLevelTests
{
    [TestCase(-1f, 1)]
    [TestCase(0f, 1)]
    [TestCase(0.649999f, 1)]
    [TestCase(0.65f, 2)]
    [TestCase(0.899999f, 2)]
    [TestCase(0.90f, 3)]
    [TestCase(1f, 3)]
    [TestCase(2f, 3)]
    public void RewardRollUsesApprovedBoundaries(float roll, int expectedLevels)
    {
        Assert.That(RunGlobalLevelReward.Resolve(roll), Is.EqualTo(expectedLevels));
    }

    [Test]
    public void InvalidRewardRollFallsBackToOneLevel()
    {
        Assert.That(RunGlobalLevelReward.Resolve(float.NaN), Is.EqualTo(1));
    }

    [Test]
    public void AddGlobalLevelsClampsAndEmitsPreviousAndCurrentLevel()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        int eventCount = 0;
        int eventPrevious = 0;
        int eventCurrent = 0;
        inventory.OnGlobalLevelChanged += (previous, current) =>
        {
            eventCount++;
            eventPrevious = previous;
            eventCurrent = current;
        };

        int applied = inventory.AddGlobalLevels(1000);

        Assert.That(applied, Is.EqualTo(29));
        Assert.That(inventory.GlobalLevel, Is.EqualTo(30));
        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(eventPrevious, Is.EqualTo(1));
        Assert.That(eventCurrent, Is.EqualTo(30));
        Assert.That(inventory.AddGlobalLevels(1), Is.EqualTo(0));
        Assert.That(eventCount, Is.EqualTo(1));

        Object.DestroyImmediate(inventory);
    }

    [Test]
    public void ResetRunProgressResetsGlobalLevelAndEmitsChange()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        inventory.AddGlobalLevels(3);
        int eventPrevious = 0;
        int eventCurrent = 0;
        inventory.OnGlobalLevelChanged += (previous, current) =>
        {
            eventPrevious = previous;
            eventCurrent = current;
        };

        inventory.ResetRunProgress();

        Assert.That(inventory.GlobalLevel, Is.EqualTo(1));
        Assert.That(eventPrevious, Is.EqualTo(4));
        Assert.That(eventCurrent, Is.EqualTo(1));

        Object.DestroyImmediate(inventory);
    }

    [Test]
    public void UnownedCharacterStatsUseGlobalLevelWithoutCreatingRunState()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        character.stats = new CharacterStats
        {
            attack = 10f,
            defense = 20f,
            maxHP = 100f
        };
        character.upgrade = new CharacterUpgrade
        {
            maxLevel = 30,
            attackPerLevel = 2f,
            defensePerLevel = 3f,
            maxHpPerLevel = 10f
        };
        inventory.AddGlobalLevels(2);

        CharacterStats stats = inventory.GetCharacterBaseStats(character);

        Assert.That(inventory.GlobalLevel, Is.EqualTo(3));
        Assert.That(stats.attack, Is.EqualTo(14f));
        Assert.That(stats.defense, Is.EqualTo(26f));
        Assert.That(stats.maxHP, Is.EqualTo(120f));
        Assert.That(inventory.GetRunCharacter(character), Is.Null);

        Object.DestroyImmediate(character);
        Object.DestroyImmediate(inventory);
    }
}
#endif
