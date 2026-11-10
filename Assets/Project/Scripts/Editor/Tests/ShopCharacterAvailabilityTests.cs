#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem;
using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;
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
    public void ProductionCatalogOnlyRollsEligibleUnownedCharacters()
    {
        shop.playerInventory = Create<PlayerRunInventory>();
        CharacterItem[] catalog = Resources.LoadAll<CharacterItem>("ItemSO");
        shop.codexInventory.allItems.AddRange(catalog);
        CharacterItem[] eligible = catalog.Where(item => item.availableInShop
            && !shop.playerInventory.HasItem(item)).ToArray();
        Assert.That(eligible, Is.Not.Empty);
        for (int i = 0; i < 20; i++)
            Assert.That(eligible, Does.Contain(Roll(ShopItemListType.Character)));
    }

    [Test]
    public void RollsExcludeUnavailableCharactersEvenWhenOwnedFilteringIsOff()
    {
        var excluded = Create<CharacterItem>();
        excluded.availableInShop = false;
        var allowed = Create<CharacterItem>();
        allowed.characterObjectData = Create<CharacterObject>();
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

    [TestCase(true)]
    [TestCase(false)]
    public void OwnedCharacterNeverRollsOrPurchasesEvenWithMetaProgression(bool dead)
    {
        shop.playerInventory = Create<PlayerRunInventory>();
        shop.metaProgression = Create<Project.Progression.CharacterProgressionState>();
        CharacterItem main = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
        CharacterItem camera = Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl");
        shop.playerInventory.TryAddItem(camera);
        shop.codexInventory.allItems.Add(main);
        shop.codexInventory.allItems.Add(camera);
        if (dead)
        {
            shop.playerInventory.DeployTeam(new[] { main.characterObjectData, camera.characterObjectData }, 0);
            shop.playerInventory.TryRemoveDefeatedTeamCharacter(camera.characterObjectData, main.characterObjectData, out _);
        }
        int currency = shop.playerInventory.currency;
        Assert.That(Roll(ShopItemListType.Character), Is.Null);
        Assert.That(shop.CanPurchaseOffer(camera, 1, 0), Is.False);
        Assert.That(shop.TryPurchase(camera, 1, 0), Is.False);
        Assert.That(shop.playerInventory.currency, Is.EqualTo(currency));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BuyingLastCharacterChangesOtherCharacterOffersToArtifactsAndRerollKeepsFallback(bool buyThroughOffer)
    {
        shop.playerInventory = Create<PlayerRunInventory>();
        shop.playerInventory.currency = 10000;
        CharacterItem camera = Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl");
        ArtifactDefinition artifact = Create<ArtifactDefinition>();
        artifact.id = "fallback-artifact";
        artifact.rarity = ArtifactRarity.R;
        artifact.price = 20;
        shop.codexInventory.allItems.Add(camera);
        shop.codexInventory.allItems.Add(artifact);
        // Unavailable characters are deliberately not part of shop completion.
        CharacterItem unavailable = Create<CharacterItem>();
        unavailable.availableInShop = false;
        shop.codexInventory.allItems.Add(unavailable);
        var first = CreateOffer();
        var second = CreateOffer();
        shop.GenerateNewShopItems();
        Assert.That(first.CurrentItem, Is.SameAs(camera));
        Assert.That(second.CurrentItem, Is.SameAs(camera));
        Assert.That(buyThroughOffer ? shop.TryPurchaseOffer(first) : shop.TryPurchase(camera), Is.True);
        Assert.That(first.IsSold, Is.EqualTo(buyThroughOffer));
        Assert.That(second.ConfiguredItemType, Is.EqualTo(ShopItemListType.Character));
        Assert.That(second.ItemType, Is.EqualTo(ShopItemListType.Artifact));
        Assert.That(second.CurrentItem, Is.SameAs(artifact));
        Assert.That(second.CurrentQuantity, Is.EqualTo(1));
        Assert.That(second.CurrentPrice, Is.EqualTo(20));
        Assert.That(shop.TryRerollOffers(), Is.True);
        Assert.That(first.CurrentItem, Is.SameAs(artifact));
        Assert.That(second.CurrentItem, Is.SameAs(artifact));
        Assert.That(second.CurrentPrice, Is.EqualTo(23));
    }

    private ShopItemListUI CreateOffer()
    {
        var host = new GameObject("CharacterOffer");
        host.SetActive(false);
        temporaryObjects.Add(host);
        ShopItemListUI offer = host.AddComponent<ShopItemListUI>();
        typeof(ShopItemListUI).GetField("itemType", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(offer, ShopItemListType.Character);
        shop.RegisterItemList(offer);
        return offer;
    }

    [Test]
    public void CharacterIdentityAliasesCannotBypassOneTimePurchase()
    {
        shop.playerInventory = Create<PlayerRunInventory>();
        CharacterItem main = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
        CharacterObject identityAlias = Create<CharacterObject>();
        identityAlias.characterId = main.characterObjectData.characterId;
        CharacterItem alias = Create<CharacterItem>();
        alias.id = "another-item-for-owned-main";
        alias.characterObjectData = identityAlias;
        shop.codexInventory.allItems.Add(alias);
        Assert.That(Roll(ShopItemListType.Character), Is.Null);
        Assert.That(shop.CanPurchaseOffer(alias, 1, 0), Is.False);
    }
}
#endif
