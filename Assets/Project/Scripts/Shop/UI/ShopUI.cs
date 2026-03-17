using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Project.InventorySystem.Items;

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
        private ShopTab currentTab = ShopTab.Buy;

        private void Start()
        {
            if (buyTabButton != null) buyTabButton.onClick.AddListener(() => SwitchTab(ShopTab.Buy));
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
            
            SwitchTab(ShopTab.Buy);
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
            currentTab = tab;
            
            // UI feedback for tabs
            if (buyTabButton != null) buyTabButton.interactable = (tab != ShopTab.Buy);
            if (sellTabButton != null) sellTabButton.interactable = (tab != ShopTab.Sell);

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

            List<BaseItem> itemsToDisplay = (currentTab == ShopTab.Buy) 
                ? new List<BaseItem>(shopManager.currentShopItems) 
                : new List<BaseItem>(shopManager.GetSellableItems());

            // Sort by type: Consumable (0) → Character (1) → Artifact (2)
            itemsToDisplay.Sort((a, b) => ShopGridItemUI.GetSortOrder(a).CompareTo(ShopGridItemUI.GetSortOrder(b)));

            // Spawn new slots
            foreach (var item in itemsToDisplay)
            {
                GameObject slotObj = Instantiate(gridItemPrefab, gridParent);
                ShopGridItemUI slotUI = slotObj.GetComponent<ShopGridItemUI>();
                
                if (slotUI != null)
                {
                    slotUI.Init(item, OnGridItemClicked);
                    spawnedSlots.Add(slotUI);
                }
            }

            UpdateCurrencyDisplay();
        }

        private void OnGridItemClicked(BaseItem item)
        {
            if (previewPanel != null)
            {
                bool canAfford = (currentTab == ShopTab.Buy) 
                    ? (shopManager != null && shopManager.playerInventory != null && shopManager.playerInventory.CanAfford(item.price))
                    : true;

                previewPanel.ShowItem(item, currentTab, canAfford, () => ProcessTransaction(item));
            }
        }

        private void ProcessTransaction(BaseItem item)
        {
            if (shopManager == null) return;

            bool success = false;
            if (currentTab == ShopTab.Buy)
            {
                success = shopManager.TryPurchase(item);
            }
            else
            {
                success = shopManager.TrySell(item);
            }

            if (success)
            {
                RefreshUI();
                if (previewPanel != null) previewPanel.ClearPreview();
            }
            else if (currentTab == ShopTab.Buy && previewPanel != null)
            {
                // Purchase failed (insufficient funds) — update the button dynamically
                previewPanel.SetBuyable(false);
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
            if (shopManager != null && currentTab == ShopTab.Buy)
            {
                shopManager.GenerateNewShopItems();
                RefreshUI();
                if (previewPanel != null) previewPanel.ClearPreview();
            }
        }
    }
}
