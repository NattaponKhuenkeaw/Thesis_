#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.ShopSystem.UI;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class ShopPurchaseAudioTests
{
    private const string ClipPath = "Assets/Project/Audio/SFX/Shared_EnemyDissolve_ShopBuySuccess.wav";
    private const string FailureClipPath = "Assets/Project/Audio/SFX/mixkit-sci-fi-error-alert-898.wav";

    [Test]
    public void ShopSceneReferencesChosenPurchaseSound()
    {
        var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath("d36c77798e07625409da16f442bc42f5"));
        try
        {
            ShopManager shop = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                shop = root.GetComponentInChildren<ShopManager>(true);
                if (shop != null) break;
            }
            Assert.That(shop, Is.Not.Null);
            var song = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            Assert.That(song, Is.Not.Null);
            var settings = new SerializedObject(shop);
            Assert.That(settings.FindProperty("purchaseSuccessSfx").objectReferenceValue, Is.SameAs(song));
            Assert.That(settings.FindProperty("purchaseSuccessVolume").floatValue, Is.GreaterThan(0f));
            var failureClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FailureClipPath);
            Assert.That(failureClip, Is.Not.Null);
            Assert.That(settings.FindProperty("purchaseFailureSfx").objectReferenceValue, Is.SameAs(failureClip));
            Assert.That(settings.FindProperty("purchaseFailureVolume").floatValue, Is.GreaterThan(0f));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(100, true)]
    [TestCase(0, false)]
    public void ConfiguredAudioPreservesPurchaseResultAndCurrency(int currency, bool expectedSuccess)
    {
        var host = new GameObject("ShopPurchaseAudioTest");
        host.SetActive(false);
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var item = ScriptableObject.CreateInstance<Item>();
        try
        {
            var shop = host.AddComponent<ShopManager>();
            shop.playerInventory = inventory;
            var settings = new SerializedObject(shop);
            settings.FindProperty("purchaseSuccessSfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            settings.FindProperty("purchaseFailureSfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FailureClipPath);
            settings.ApplyModifiedPropertiesWithoutUndo();
            item.id = "purchase-audio-test-item";
            item.itemName = "Purchase audio test item";
            inventory.currency = currency;
            Assert.That(shop.TryPurchase(item, 1, 25), Is.EqualTo(expectedSuccess));
            Assert.That(inventory.currency, Is.EqualTo(expectedSuccess ? currency - 25 : currency));
            Assert.That(inventory.HasItem(item), Is.EqualTo(expectedSuccess));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void UnaffordableOfferAllowsAttemptButSoldOfferDisablesButton()
    {
        var host = new GameObject("ShopRejectedPurchaseButtonTest");
        host.SetActive(false);
        var inventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        var item = ScriptableObject.CreateInstance<Item>();
        try
        {
            var shop = host.AddComponent<ShopManager>();
            shop.playerInventory = inventory;
            inventory.currency = 0;
            item.id = "unaffordable-audio-test-item";
            var offer = host.AddComponent<ShopItemListUI>();
            var buttonHost = new GameObject("BuyButton", typeof(RectTransform), typeof(Button));
            buttonHost.transform.SetParent(host.transform, false);
            var button = buttonHost.GetComponent<Button>();
            var settings = new SerializedObject(offer);
            settings.FindProperty("shopManager").objectReferenceValue = shop;
            settings.FindProperty("buyButton").objectReferenceValue = button;
            settings.ApplyModifiedPropertiesWithoutUndo();
            offer.Populate(item, 1, 25);
            typeof(ShopItemListUI).GetMethod("ApplyOpenState", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(offer, null);
            Assert.That(shop.CanPurchaseOffer(item, 1, 25), Is.False);
            Assert.That(button.interactable, Is.True);
            Assert.That(shop.TryPurchaseOffer(offer), Is.False);
            Assert.That(offer.IsSold, Is.False);
            Assert.That(inventory.currency, Is.Zero);
            offer.MarkSold();
            Assert.That(button.interactable, Is.False);
            Assert.That(shop.TryPurchaseOffer(offer), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(item);
        }
    }
}
#endif
