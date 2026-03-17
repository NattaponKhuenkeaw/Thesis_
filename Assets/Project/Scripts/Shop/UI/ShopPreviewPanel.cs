using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using System;

namespace Project.ShopSystem.UI
{
    public class ShopPreviewPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject contentParent;
        [SerializeField] private Image previewIcon;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonText;

        [Header("Item Type Label")]
        [SerializeField] private Image itemTypeLabelPanel;
        [SerializeField] private TMP_Text itemTypeLabelText;

        [Header("Button Colors")]
        [SerializeField] private Color buyColor = new Color(0.2f, 0.75f, 0.3f, 1f);   // Green
        [SerializeField] private Color sellColor = new Color(0.85f, 0.25f, 0.25f, 1f); // Red
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey

        [Header("Item Type Colors")]
        [SerializeField] private Color consumableTypeColor = new Color(0.55f, 0.55f, 0.55f, 1f);  // Grey
        [SerializeField] private Color artifactTypeColor   = new Color(0.6f, 0.15f, 0.15f, 1f);   // Dark Red
        [SerializeField] private Color characterTypeColor  = new Color(0.5f, 0.2f, 0.7f, 1f);     // Purple

        private BaseItem currentItem;
        private Action onActionCallback;
        private ShopUI.ShopTab currentTab;

        /// <summary>
        /// Shows the item preview. canAfford is only relevant on the Buy tab.
        /// </summary>
        public void ShowItem(BaseItem item, ShopUI.ShopTab tab, bool canAfford, Action onAction)
        {
            currentItem = item;
            currentTab = tab;
            onActionCallback = onAction;

            if (contentParent != null) contentParent.SetActive(true);

            if (itemNameText != null) itemNameText.text = item.itemName;
            if (descriptionText != null) descriptionText.text = item.description;

            // Calculate displayed price
            int displayPrice = (tab == ShopUI.ShopTab.Buy) ? item.price : (item.price / 2);

            if (priceText != null)
                priceText.text = $"${displayPrice}";

            // Action button text mirrors the price
            if (actionButtonText != null)
                actionButtonText.text = $"${displayPrice}";

            // Icon — always keep enabled, only swap sprite if available
            if (previewIcon != null)
            {
                if (item.icon != null)
                    previewIcon.sprite = item.icon;

                previewIcon.enabled = true;
            }

            // Item type label
            UpdateItemTypeLabel(item);

            // Button color & interactability
            SetButtonColor(tab);

            if (tab == ShopUI.ShopTab.Buy)
            {
                SetBuyable(canAfford);
            }
            else
            {
                // Sell is always available if the item is listed
                if (actionButton != null)
                {
                    actionButton.interactable = true;
                    actionButton.onClick.RemoveAllListeners();
                    actionButton.onClick.AddListener(OnActionClicked);
                }
            }
        }

        /// <summary>
        /// Dynamically updates the button when a transaction is not possible (e.g. insufficient funds).
        /// Pass false to disable the button and show a popup notification.
        /// </summary>
        public void SetBuyable(bool canAfford)
        {
            if (actionButton != null)
            {
                actionButton.interactable = canAfford;
                actionButton.onClick.RemoveAllListeners();

                if (canAfford)
                {
                    actionButton.onClick.AddListener(OnActionClicked);
                }
            }

            // Button text always shows the price — never replace with status text
            if (actionButtonText != null && currentItem != null)
            {
                int displayPrice = (currentTab == ShopUI.ShopTab.Buy)
                    ? currentItem.price
                    : (currentItem.price / 2);
                actionButtonText.text = $"${displayPrice}";
            }

            // Grey-out button image when not affordable
            SetButtonColor(canAfford ? currentTab : (ShopUI.ShopTab?)null);

            // Show popup for insufficient funds
            if (!canAfford && currentTab == ShopUI.ShopTab.Buy)
            {
                if (ShopPopupText.Instance != null)
                    ShopPopupText.Instance.Show("Insufficient Funds!");
            }
        }

        public void ClearPreview()
        {
            currentItem = null;
            if (contentParent != null) contentParent.SetActive(false);
        }

        private void OnActionClicked()
        {
            onActionCallback?.Invoke();
        }

        /// <summary>
        /// Sets the item type label text and panel color based on the item's concrete type.
        /// </summary>
        private void UpdateItemTypeLabel(BaseItem item)
        {
            string typeName;
            Color typeColor;

            if (item is CharacterItem)
            {
                typeName = "Character";
                typeColor = characterTypeColor;
            }
            else if (item is Artifact)
            {
                typeName = "Artifact";
                typeColor = artifactTypeColor;
            }
            else
            {
                typeName = "Consumable";
                typeColor = consumableTypeColor;
            }

            if (itemTypeLabelText != null)
                itemTypeLabelText.text = typeName;

            if (itemTypeLabelPanel != null)
                itemTypeLabelPanel.color = typeColor;
        }

        /// <summary>
        /// Sets the button image color. Null = disabled/grey.
        /// </summary>
        private void SetButtonColor(ShopUI.ShopTab? tab)
        {
            if (actionButton == null) return;

            Image btnImage = actionButton.GetComponent<Image>();
            if (btnImage == null) return;

            if (tab == null)
                btnImage.color = disabledColor;
            else if (tab == ShopUI.ShopTab.Buy)
                btnImage.color = buyColor;
            else
                btnImage.color = sellColor;
        }
    }
}
