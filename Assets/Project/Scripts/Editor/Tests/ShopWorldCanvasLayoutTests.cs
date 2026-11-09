#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using Project.ShopSystem.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
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
                var price = (RectTransform)card.transform.Find("BuyPanel/Image");
                var buy = (RectTransform)card.transform.Find("BuyPanel/Button");

                Assert.That(title.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis), card.name);
                Assert.That(description.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis), card.name);
                Assert.That(Bottom(icon), Is.GreaterThan(Top((RectTransform)title.transform)), card.name + " icon/title");
                Assert.That(Bottom((RectTransform)title.transform), Is.GreaterThan(Top((RectTransform)description.transform)), card.name + " title/description");
                Assert.That(Bottom((RectTransform)description.transform), Is.GreaterThan(Top(price)), card.name + " description/price");
                Assert.That(Bottom(price), Is.GreaterThan(Top(buy)), card.name + " price/button");
                Assert.That(Bottom(buy), Is.GreaterThanOrEqualTo(Bottom((RectTransform)card.transform)), card.name + " button/card");
            }

            var orderedCards = cards.Select(card => (RectTransform)card.transform).OrderBy(Left).ToArray();
            for (int i = 1; i < orderedCards.Length; i++)
                Assert.That(Left(orderedCards[i]), Is.GreaterThan(Right(orderedCards[i - 1])), "Shop offer cards overlap");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
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
