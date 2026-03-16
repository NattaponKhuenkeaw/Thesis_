using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;

namespace Project.ShopSystem.UI
{
    public class ShopSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button buyButton;

        private BaseItem currentItem;
        private ShopManager shopManager;

        public void Init(BaseItem item, ShopManager manager)
        {
            currentItem = item;
            shopManager = manager;

            if (itemNameText != null) itemNameText.text = item.itemName;
            if (priceText != null) priceText.text = $"${item.price}";
            if (descriptionText != null) descriptionText.text = item.description;
            if (iconImage != null && item.icon != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void OnBuyClicked()
        {
            if (shopManager != null && currentItem != null)
            {
                bool success = shopManager.TryPurchase(currentItem);
                if (success)
                {
                    // Disable button to prevent buying again, or refresh shop
                    buyButton.interactable = false;
                    if (priceText != null) priceText.text = "SOLD";
                }
            }
        }
    }
}
