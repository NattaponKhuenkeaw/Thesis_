#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.Progression;
using Project.ShopSystem;
using UnityEngine;

public class LevelRoomRewardFlowTests
{
    [Test]
    public void LevelRoomApplyUsesSharedRewardAndClampsAtCap()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        inventory.AddGlobalLevels(28); // Lv.29
        Assert.That(inventory.GlobalLevel, Is.EqualTo(29));

        int rolled = RunGlobalLevelReward.Resolve(0.95f); // +3
        int applied = inventory.AddGlobalLevels(rolled);

        Assert.That(rolled, Is.EqualTo(3));
        Assert.That(applied, Is.EqualTo(1));
        Assert.That(inventory.GlobalLevel, Is.EqualTo(30));

        Object.DestroyImmediate(inventory);
    }

    [Test]
    public void SecondApplyAtCapReturnsZero()
    {
        PlayerRunInventory inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        inventory.AddGlobalLevels(29);
        Assert.That(inventory.GlobalLevel, Is.EqualTo(30));

        int rolled = RunGlobalLevelReward.Resolve(0.2f);
        int applied = inventory.AddGlobalLevels(rolled);

        Assert.That(rolled, Is.EqualTo(1));
        Assert.That(applied, Is.EqualTo(0));
        Assert.That(inventory.GlobalLevel, Is.EqualTo(30));

        Object.DestroyImmediate(inventory);
    }
}
#endif
