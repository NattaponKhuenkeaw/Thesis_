#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class ShopWorldCanvasLayoutTests
{
    [Test]
    public void ShopOffersKeepTextPriceAndBuyButtonInSeparateRows()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Project/Scenes/Test_Shop.unity");
        try
        {
            var canvas = scene.GetRootGameObjects().Single(root => root.name == "ShopWorldCanvas");
            var cards = canvas.GetComponentsInChildren<ShopItemListUI>(true);
            Assert.That(cards, Has.Length.EqualTo(3));
            foreach (var card in cards) card.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();

            foreach (var card in cards)
            {
                var content = card.transform.Find("ContentPanel");
                var icon = (RectTransform)content.Find("iconImage");
                var title = content.Find("ItemNext").GetComponent<TMP_Text>();
                var description = content.Find("ItemDesc").GetComponent<TMP_Text>();
                var price = (RectTransform)card.transform.Find("OfferActions/Image");
                var buy = (RectTransform)card.transform.Find("OfferActions/Button");

                Assert.That(card.GetComponent<Animator>().enabled, Is.True, "The compact offers should animate open.");
                Assert.That(card.GetComponent<Image>().color.a, Is.Zero, card.name);
                Assert.That(title.fontSharedMaterial.mainTexture, Is.SameAs(title.font.atlasTexture), card.name);
                Assert.That(description.fontSharedMaterial.mainTexture, Is.SameAs(description.font.atlasTexture), card.name);
                Assert.That(title.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis), card.name);
                Assert.That(title.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap), card.name);
                Assert.That(description.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis), card.name);
                Assert.That(Bottom(icon), Is.GreaterThan(Top((RectTransform)title.transform)), card.name + " icon/title");
                Assert.That(Bottom((RectTransform)title.transform), Is.GreaterThan(Top((RectTransform)description.transform)), card.name + " title/description");
                Assert.That(Bottom((RectTransform)title.transform), Is.GreaterThan(Top(price)), card.name + " title/price");
                Assert.That(Bottom(price), Is.GreaterThan(Top(buy)), card.name + " price/button");
                Assert.That(Bottom(buy), Is.GreaterThanOrEqualTo(Bottom((RectTransform)card.transform)), card.name + " button/card");
            }

            var orderedCards = cards.Select(card => (RectTransform)card.transform).OrderBy(Left).ToArray();
            for (int i = 1; i < orderedCards.Length; i++)
                Assert.That(Left(orderedCards[i]), Is.GreaterThan(Right(orderedCards[i - 1])), "Shop offer cards overlap");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator EnteringAnOfferKeepsBuyBelowThePriceAndLeavingHidesIt()
    {
        EditorSceneManager.OpenScene("Assets/Project/Scenes/Test_Shop.unity");
        yield return new EnterPlayMode();
        yield return null;
        var offers = Object.FindObjectsByType<ShopItemListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var artifact = Resources.Load<ArtifactDefinition>("ItemSO/Artifacts/RarityR/R_ChippedExecutionSeal");
        offers.Single(offer => offer.ItemType == ShopItemListType.Artifact).Populate(artifact, 1, artifact.price);
        var card = offers.Single(offer => offer.ItemType == ShopItemListType.Character);
        var item = Resources.Load<CharacterItem>("ItemSO/char_1_CameraGIrl");
        card.Populate(item, 1, item.price);
        var buy = card.transform.Find("OfferActions/Button").GetComponent<Button>();
        var price = (RectTransform)card.transform.Find("OfferActions/Image");
        var description = (RectTransform)card.transform.Find("ContentPanel/ItemDesc");
        var cardRect = (RectTransform)card.transform;
        var animator = card.GetComponent<Animator>();
        var player = new GameObject("Shop range test") { tag = "Player" };
        var collider = player.AddComponent<BoxCollider2D>();
        yield return new WaitForSeconds(.35f);
        float closedHeight = cardRect.rect.height;
        Assert.That(buy.gameObject.activeSelf, Is.False);

        card.SendMessage("OnTriggerEnter2D", collider);
        float deadline = Time.realtimeSinceStartup + 2f;
        while (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("opened_shopItemList"))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Offer did not finish opening.");
            Assert.That(Bottom(price), Is.GreaterThan(Top((RectTransform)buy.transform)));
            if (description.localScale.y > .05f)
                Assert.That(Bottom(description), Is.GreaterThan(Top(price)), "Description and price overlap while opening.");
            yield return null;
        }
        Assert.That(buy.isActiveAndEnabled && buy.interactable, Is.True);
        Assert.That(cardRect.rect.height, Is.GreaterThan(closedHeight + 100f));

        card.SendMessage("OnTriggerExit2D", collider);
        Assert.That(buy.gameObject.activeSelf, Is.False);
        deadline = Time.realtimeSinceStartup + 2f;
        while (animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("closed_shopItemList"))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Offer did not finish closing.");
            yield return null;
        }
        Assert.That(cardRect.rect.height, Is.EqualTo(closedHeight).Within(1f));
        card.MarkSold();
        Assert.That(card.transform.Find("Brought").gameObject.activeInHierarchy, Is.True);
        Object.Destroy(player);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    private static float Bottom(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[0].y;
    }

    private static float Top(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[1].y;
    }

    private static float Left(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[0].x;
    }

    private static float Right(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[2].x;
    }
}
#endif
