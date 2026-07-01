using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Project.InventorySystem.Items;
using Project.ShopSystem;

namespace Project.ShopSystem.UI
{
    public class ShopUI : MonoBehaviour
    {
        public enum ShopTab { Buy, Sell }

        [Header("References")]
        public ShopManager shopManager;
        
        [Header("UI Setup")]
        [Tooltip("The main panel for the Shop UI.")]
        public GameObject shopPanel;
        public Transform gridParent;
        public GameObject gridItemPrefab;
        public ShopPreviewPanel previewPanel;
        
        [Header("Tab Controls")]
        public Button buyTabButton;
        public Button sellTabButton;
        public TMPro.TMP_Text currencyText;

        private List<ShopGridItemUI> spawnedSlots = new List<ShopGridItemUI>();
        private ShopTab currentTab = ShopTab.Sell;

        private void Start()
        {
            if (buyTabButton != null)
            {
                buyTabButton.gameObject.SetActive(false);
            }

            if (sellTabButton != null) sellTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Sell));
            
            // Initial render
            RefreshUI();
        }

        public void OpenShopUI()
        {
            Debug.Log("Shop UI is now OPEN!");
            
            GameObject target = (shopPanel != null) ? shopPanel : gameObject;

            if (target.activeSelf) return;

            target.SetActive(true);
            Project.UI.InputLockManager.Lock();
            
            SwitchTab(ShopTab.Sell);
            UpdateCurrencyDisplay();
        }

        public void CloseShopUI()
        {
            Debug.Log("Shop UI is now CLOSED!");
            
            GameObject target = (shopPanel != null) ? shopPanel : gameObject;

            if (!target.activeSelf) return;

            target.SetActive(false);
            Project.UI.InputLockManager.Unlock();
        }

        public void SwitchTab(ShopTab tab)
        {
            currentTab = ShopTab.Sell;
            
            // UI feedback for tabs
            if (buyTabButton != null) buyTabButton.interactable = false;
            if (sellTabButton != null) sellTabButton.interactable = false;

            RefreshUI();
            if (previewPanel != null) previewPanel.ClearPreview();
        }

        public void RefreshUI()
        {
            if (shopManager == null) return;
            
            // Clear old slots
            foreach (var slot in spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            spawnedSlots.Clear();

            List<ShopSellEntry> itemsToDisplay = new List<ShopSellEntry>(shopManager.GetSellableEntries());

            // Sort by type: Consumable (0), Character (1), Artifact (2).
            itemsToDisplay.Sort((a, b) =>
            {
                int sortCompare = ShopGridItemUI.GetSortOrder(a.Item).CompareTo(ShopGridItemUI.GetSortOrder(b.Item));
                if (sortCompare != 0)
                {
                    return sortCompare;
                }

                return a.Kind.CompareTo(b.Kind);
            });

            // Spawn new slots
            foreach (ShopSellEntry entry in itemsToDisplay)
            {
                GameObject slotObj = Instantiate(gridItemPrefab, gridParent);
                ShopGridItemUI slotUI = slotObj.GetComponent<ShopGridItemUI>();
                
                if (slotUI != null)
                {
                    slotUI.Init(entry, OnGridItemClicked, isSoldOut: false);
                    spawnedSlots.Add(slotUI);
                }
            }

            UpdateCurrencyDisplay();
        }

        private void OnGridItemClicked(ShopSellEntry entry)
        {
            if (previewPanel != null && entry != null)
            {
                previewPanel.ShowSellEntry(entry, () => ProcessTransaction(entry));
            }
        }

        private void ProcessTransaction(ShopSellEntry entry)
        {
            if (shopManager == null) return;

            bool success = shopManager.TrySell(entry);

            if (success)
            {
                RefreshUI();
                if (previewPanel != null) previewPanel.ClearPreview();
            }
            UpdateCurrencyDisplay();
        }

        public void UpdateCurrencyDisplay()
        {
            if (currencyText != null && shopManager != null && shopManager.playerInventory != null)
            {
                currencyText.text = $"${shopManager.playerInventory.currency}";
            }
        }

        public void ProcessReroll()
        {
            if (shopManager != null)
            {
                shopManager.TryRerollOffers();
                UpdateCurrencyDisplay();
            }
        }
    }
}
