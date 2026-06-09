using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
using Project.UI;
using System;

namespace Project.ShopSystem.UI
{
    public class ShopGridItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button button;
        [SerializeField] private Image attributeImage; // For elemental attribute

        [Header("Element Settings")]
        [SerializeField] private ElementSpriteResolver elementResolver;

        [Header("Sold Out Overlay")]
        [Tooltip("A panel that covers the grid item when the item is already owned (one-time buy).")]
        [SerializeField] private GameObject soldOutPanel;

        [Header("Frame Colors by Type")]
        [SerializeField] private Color consumableColor = new Color(0.55f, 0.55f, 0.55f, 1f);   // Grey
        [SerializeField] private Color artifactColor   = new Color(0.6f, 0.15f, 0.15f, 1f);    // Dark Red
        [SerializeField] private Color characterColor  = new Color(0.5f, 0.2f, 0.7f, 1f);      // Purple

        private BaseItem currentItem;
        private Action<BaseItem> onClickCallback;

        /// <summary>
        /// Initializes the grid item. Pass isSoldOut = true to show the sold-out overlay and block interaction.
        /// </summary>
        public void Init(BaseItem item, Action<BaseItem> onClick, bool isSoldOut = false)
        {
            currentItem = item;
            onClickCallback = onClick;

            if (nameText != null) nameText.text = item.itemName;

            // Icon — always keep enabled, only swap sprite if available
            if (iconImage != null)
            {
                if (item.icon != null)
                    iconImage.sprite = item.icon;
                    
                iconImage.enabled = true;
            }

            // Set frame color based on item type
            if (frameImage != null)
            {
                frameImage.color = GetItemTypeColor(item);
            }

            // Set attribute image based on item type
            if (attributeImage != null)
            {
                if (item is CharacterItem characterItem && characterItem.characterObjectData != null && elementResolver != null)
                {
                    elementResolver.ResolveImage(attributeImage, characterItem.characterObjectData.elementType);
                }
                else
                {
                    attributeImage.enabled = false;
                }
            }

            // Sold-out overlay for one-time-buy items already owned
            if (soldOutPanel != null)
            {
                soldOutPanel.SetActive(isSoldOut);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();

                if (isSoldOut)
                {
                    button.interactable = false;
                }
                else
                {
                    button.interactable = true;
                    button.onClick.AddListener(OnClicked);
                }
            }
        }

        private Color GetItemTypeColor(BaseItem item)
        {
            if (item is CharacterItem) return characterColor;
            if (item is Artifact)      return artifactColor;
            return consumableColor; // Item (consumable) or any other fallback
        }

        /// <summary>
        /// Returns a sort-order key for the item type.
        /// Order: Consumable (0) → Character (1) → Artifact (2)
        /// </summary>
        public static int GetSortOrder(BaseItem item)
        {
            return ItemCategoryHelper.GetSortOrder(item);
        }

        /// <summary>
        /// Returns true if the item is a one-time-buy type (Artifact or Character).
        /// Consumable items (Item) can be bought multiple times.
        /// </summary>
        public static bool IsOneTimeBuyItem(BaseItem item)
        {
            return ItemCategoryHelper.IsOneTimeBuy(item);
        }

        private void OnClicked()
        {
            onClickCallback?.Invoke(currentItem);
        }
    }
}
