using System;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private TMP_Text amountText;

        [Header("Item Type Label")]
        [SerializeField] private Image itemTypeLabelPanel;
        [SerializeField] private TMP_Text itemTypeLabelText;

        [Header("Button Colors")]
        [SerializeField] private Color sellColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        [Header("Item Type Colors")]
        [SerializeField] private Color consumableTypeColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color artifactTypeColor = new Color(0.6f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color characterTypeColor = new Color(0.5f, 0.2f, 0.7f, 1f);

        private Action onActionCallback;

        public void ShowSellEntry(ShopSellEntry entry, Action onAction)
        {
            if (entry == null || entry.Item == null)
            {
                ClearPreview();
                return;
            }

            BaseItem item = entry.Item;
            onActionCallback = onAction;

            if (contentParent != null) contentParent.SetActive(true);
            if (itemNameText != null) itemNameText.text = entry.DisplayName;
            if (descriptionText != null) descriptionText.text = item.description;
            if (priceText != null) priceText.text = $"${entry.Price}";
            if (actionButtonText != null) actionButtonText.text = $"${entry.Price}";

            if (previewIcon != null)
            {
                if (item.icon != null)
                {
                    previewIcon.sprite = item.icon;
                }

                previewIcon.enabled = true;
            }

            UpdateItemTypeLabel(item);

            if (amountText != null)
            {
                amountText.text = entry.Kind == ShopSellEntryKind.Fragment
                    ? $"Fragments: {entry.Quantity}"
                    : $"Owned: {entry.Quantity}";
            }

            if (actionButton != null)
            {
                Image buttonImage = actionButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = sellColor;
                }

                actionButton.interactable = true;
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionClicked);
            }
        }

        public void ClearPreview()
        {
            onActionCallback = null;
            if (contentParent != null) contentParent.SetActive(false);
        }

        private void OnActionClicked()
        {
            onActionCallback?.Invoke();
        }

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

            if (itemTypeLabelText != null) itemTypeLabelText.text = typeName;
            if (itemTypeLabelPanel != null) itemTypeLabelPanel.color = typeColor;
        }
    }
}
