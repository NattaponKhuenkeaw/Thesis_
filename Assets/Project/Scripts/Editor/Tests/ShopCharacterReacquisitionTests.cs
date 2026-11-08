#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.ShopSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ShopCharacterReacquisitionTests
{
    [TestCase("char_0_Main", true, true)]
    [TestCase("char_0_Main", true, false)]
    [TestCase("char_0_Main", false, true)]
    [TestCase("char_0_Main", false, false)]
    [TestCase("char_1_CameraGIrl", true, true)]
    [TestCase("char_1_CameraGIrl", true, false)]
    [TestCase("char_1_CameraGIrl", false, true)]
    [TestCase("char_1_CameraGIrl", false, false)]
    public void ProductionShopReacquiresDefeatedCharacterOnlyWhenFundsCoverRealPrice(
        string resource, bool enoughMoney, bool withProgression)
    {
        var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath("d36c77798e07625409da16f442bc42f5"));
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        try
        {
            var roots = scene.GetRootGameObjects();
            var shop = roots.SelectMany(root => root.GetComponentsInChildren<ShopManager>(true)).Single();
            var offer = roots.SelectMany(root => root.GetComponentsInChildren<ShopItemListUI>(true))
                .First(value => value.ItemType == ShopItemListType.Character);
            var item = Resources.Load<CharacterItem>("ItemSO/" + resource);
            var survivor = Resources.Load<CharacterItem>("ItemSO/" +
                (resource == "char_0_Main" ? "char_1_CameraGIrl" : "char_0_Main"));
            Assert.That(item, Is.Not.Null);
            Assert.That(survivor, Is.Not.Null);
            Assert.That(shop.playerInventory, Is.Not.Null);
            Assert.That(shop.metaProgression, Is.Not.Null);
            Assert.That(shop.codexInventory.allItems, Does.Contain(item));
            inventory.TryAddItem(item);
            inventory.TryAddItem(survivor);
            inventory.DeployTeam(new[] { item.characterObjectData, survivor.characterObjectData }, 0);
            inventory.SetRunCharacterCurrentHP(item.characterObjectData, 0f);
            inventory.SetRunCharacterCurrentHP(survivor.characterObjectData, 100f);
            Assert.That(inventory.TryRemoveDefeatedTeamCharacter(
                item.characterObjectData, survivor.characterObjectData, out _), Is.True);
            Assert.That(inventory.HasItem(item), Is.False);
            Assert.That(inventory.IsCharacterDefeated(item.characterObjectData), Is.True);
            inventory.currency = enoughMoney ? item.price + 100 : item.price - 1;
            int before = inventory.currency;
            shop.playerInventory = inventory;
            if (!withProgression) shop.metaProgression = null;
            Assert.That(inventory.CanReacquireDefeatedCharacter(item), Is.True);
            var excluded = typeof(ShopManager).GetMethod("ShouldExcludeFromShopRoll",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That((bool)excluded.Invoke(shop, new object[] { item }), Is.False);
            offer.Populate(item, 1, item.price);
            Assert.That(shop.CanPurchaseOffer(item, 1, item.price), Is.EqualTo(enoughMoney));
            Assert.That(shop.TryPurchaseOffer(offer), Is.EqualTo(enoughMoney));
            Assert.That(offer.IsSold, Is.EqualTo(enoughMoney));
            Assert.That(inventory.HasItem(item), Is.EqualTo(enoughMoney));
            Assert.That(inventory.IsCharacterDefeated(item.characterObjectData), Is.EqualTo(!enoughMoney));
            Assert.That(inventory.currency, Is.EqualTo(enoughMoney ? before - item.price : before));
            Assert.That(typeof(CharacterInventoryState).GetField("suppressAutoBootstrap",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventory), Is.False);
            if (enoughMoney)
            {
                Assert.That(inventory.GetRunCharacter(item.characterObjectData), Is.Not.Null);
                inventory.DeployTeam(new[] { survivor.characterObjectData, item.characterObjectData }, 0);
                Assert.That(inventory.ContainsTeamCharacter(item.characterObjectData), Is.True);
                Assert.That(shop.TryPurchaseOffer(offer), Is.False);
                Assert.That(inventory.currency, Is.EqualTo(before - item.price));
            }
        }
        finally
        {
            Object.DestroyImmediate(inventory);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void BuyingMainCannotBypassFullTeamDeath()
    {
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var host = new GameObject("FullWipeShopTest");
        try
        {
            var main = Resources.Load<CharacterItem>("ItemSO/char_0_Main");
            inventory.DeployTeam(new[] { main.characterObjectData }, 0);
            inventory.SetRunCharacterCurrentHP(main.characterObjectData, 0f);
            Assert.That(inventory.TryRemoveDefeatedTeamCharacter(
                main.characterObjectData, null, out _), Is.False);
            Assert.That(inventory.IsDeathTeardown, Is.True);
            var shop = host.AddComponent<ShopManager>();
            shop.playerInventory = inventory;
            int currency = inventory.currency;
            Assert.That(inventory.CanReacquireDefeatedCharacter(main), Is.False);
            Assert.That(shop.CanPurchaseOffer(main, 1, 0), Is.False);
            Assert.That(shop.TryPurchase(main, 1, 0), Is.False);
            Assert.That(inventory.HasItem(main), Is.False);
            Assert.That(inventory.currency, Is.EqualTo(currency));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(inventory);
        }
    }
}
#endif
