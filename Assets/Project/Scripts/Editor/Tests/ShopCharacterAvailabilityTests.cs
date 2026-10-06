#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.ShopSystem.UI;
using UnityEngine;

public class ShopCharacterAvailabilityTests
{
    private readonly List<Object> temporaryObjects = new List<Object>();
    private ShopManager shop;
    private Random.State previousRandomState;

    [SetUp]
    public void SetUp()
    {
        previousRandomState = Random.state;
        var root = new GameObject("ShopCharacterAvailabilityTest");
        temporaryObjects.Add(root);
        shop = root.AddComponent<ShopManager>();
        shop.codexInventory = Create<CodexInventory>();
        shop.excludeOwnedItems = false;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(temporaryObjects[i]);
        temporaryObjects.Clear();
        Random.state = previousRandomState;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        temporaryObjects.Add(value);
        return value;
    }

    private BaseItem Roll(ShopItemListType type) => (BaseItem)typeof(ShopManager)
        .GetMethod("GetRandomItemForType", BindingFlags.NonPublic | BindingFlags.Instance)
        .Invoke(shop, new object[] { type });

    [Test]
    public void ProductionCatalogOnlyOffersMainAndCameraGirl()
    {
        string[] available = Resources.LoadAll<CharacterItem>("ItemSO")
            .Where(item => item.availableInShop).Select(item => item.name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "char_0_Main", "char_1_CameraGIrl" }, available);
    }

    [Test]
    public void RollsExcludeUnavailableCharactersEvenWhenOwnedFilteringIsOff()
    {
        var excluded = Create<CharacterItem>();
        excluded.availableInShop = false;
        var allowed = Create<CharacterItem>();
        shop.codexInventory.allItems.Add(excluded);
        shop.codexInventory.allItems.Add(allowed);
        for (int i = 0; i < 20; i++)
            Assert.That(Roll(ShopItemListType.Character), Is.SameAs(allowed));
    }

    [Test]
    public void UnavailableCharacterCannotBePurchasedFromStaleOffer()
    {
        var excluded = Create<CharacterItem>();
        excluded.id = "shop-unavailable-test-character";
        excluded.availableInShop = false;
        shop.playerInventory = Create<PlayerRunInventory>();
        int currency = shop.playerInventory.currency;
        Assert.That(shop.CanPurchaseOffer(excluded, 1, 0), Is.False);
        Assert.That(shop.TryPurchase(excluded, 1, 0), Is.False);
        Assert.That(shop.playerInventory.currency, Is.EqualTo(currency));
        Assert.That(shop.playerInventory.HasItem(excluded), Is.False);
    }

    [Test]
    public void ConsumableOffersAreUnaffected()
    {
        var consumable = Create<Item>();
        shop.codexInventory.allItems.Add(consumable);
        Assert.That(Roll(ShopItemListType.Consumable), Is.SameAs(consumable));
    }
}
#endif
