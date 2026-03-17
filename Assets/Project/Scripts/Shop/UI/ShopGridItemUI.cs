using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.InventorySystem.Items;
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

        [Header("Frame Colors by Type")]
        [SerializeField] private Color consumableColor = new Color(0.55f, 0.55f, 0.55f, 1f);   // Grey
        [SerializeField] private Color artifactColor   = new Color(0.6f, 0.15f, 0.15f, 1f);    // Dark Red
        [SerializeField] private Color characterColor  = new Color(0.5f, 0.2f, 0.7f, 1f);      // Purple

        private BaseItem currentItem;
        private Action<BaseItem> onClickCallback;

        public void Init(BaseItem item, Action<BaseItem> onClick)
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

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClicked);
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
            if (item is Item)          return 0; // Consumable
            if (item is CharacterItem) return 1; // Character
            if (item is Artifact)      return 2; // Artifact
            return 3; // Unknown fallback
        }

        private void OnClicked()
        {
            onClickCallback?.Invoke(currentItem);
        }
    }
}

