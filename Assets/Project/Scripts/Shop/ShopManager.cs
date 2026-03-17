using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem;
using Project.InventorySystem.Items;
using Project.ShopSystem.UI;

namespace Project.ShopSystem
{
    public class ShopManager : MonoBehaviour
    {
        [Header("References")]
        public CodexInventory codexInventory;
        public PlayerShopInventory playerInventory;
        
        [Header("Shop Settings")]
        [Tooltip("How many items will be randomized for sale at once.")]
        public int itemsToDisplay = 3;
        
        [Tooltip("If true, only unowned items can appear in the shop.")]
        public bool excludeOwnedItems = true;

        [HideInInspector]
        public List<BaseItem> currentShopItems = new List<BaseItem>();

        private void Start()
        {
            GenerateNewShopItems();
        }

        /// <summary>
        /// Randomizes a new set of items from the Codex to display in the shop.
        /// </summary>
        public void GenerateNewShopItems()
        {
            currentShopItems.Clear();

            if (codexInventory == null || codexInventory.allItems.Count == 0)
            {
                Debug.LogWarning("CodexInventory is missing or empty.");
                return;
            }

            // Create a pool of available items to pull from
            List<BaseItem> availablePool = new List<BaseItem>();
            foreach (var item in codexInventory.allItems)
            {
                if (excludeOwnedItems && playerInventory != null && playerInventory.HasItem(item))
                {
                    // Only exclude one-time-buy items (Artifact/Character) that are already owned
                    if (ShopGridItemUI.IsOneTimeBuyItem(item))
                        continue;
                }
                
                availablePool.Add(item);
            }

            if (availablePool.Count == 0)
            {
                Debug.Log("No items available to put in shop (pool is empty).");
                return;
            }

            // Pick N random unique items
            int pullCount = Mathf.Min(itemsToDisplay, availablePool.Count);
            
            for (int i = 0; i < pullCount; i++)
            {
                int randomIndex = Random.Range(0, availablePool.Count);
                currentShopItems.Add(availablePool[randomIndex]);
                availablePool.RemoveAt(randomIndex); // Ensure we don't pick the same item twice
            }

            // Notify UI to update if necessary, or let UI pull
        }

        /// <summary>
        /// Attempts to purchase the item.
        /// </summary>
        public bool TryPurchase(BaseItem item)
        {
            if (playerInventory == null) return false;

            // Block one-time-buy items that are already owned
            if (IsItemSoldOut(item))
            {
                Debug.Log($"Already owned (one-time buy): {item.itemName}");
                if (ShopPopupText.Instance != null)
                    ShopPopupText.Instance.Show("Already Owned!");
                return false;
            }

            if (playerInventory.CanAfford(item.price))
            {
                playerInventory.SpendCurrency(item.price);
                playerInventory.AddItem(item);
                Debug.Log($"Successfully purchased: {item.itemName}");
                return true;
            }
            
            Debug.Log($"Insufficient currency to buy: {item.itemName}");
            return false;
        }

        /// <summary>
        /// Attempts to sell the item.
        /// </summary>
        public bool TrySell(BaseItem item)
        {
            if (playerInventory == null) return false;

            if (playerInventory.HasItem(item))
            {
                // Sell for half price (can be adjusted)
                int sellValue = item.price / 2;
                playerInventory.AddCurrency(sellValue);
                playerInventory.RemoveItem(item);
                Debug.Log($"Successfully sold: {item.itemName} for ${sellValue}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a list of all items currently owned by the player.
        /// </summary>
        public List<BaseItem> GetSellableItems()
        {
            if (playerInventory == null) return new List<BaseItem>();
            return playerInventory.ownedItems;
        }

        /// <summary>
        /// Returns true if the item is a one-time-buy type AND the player already owns it.
        /// </summary>
        public bool IsItemSoldOut(BaseItem item)
        {
            if (playerInventory == null) return false;
            return ShopGridItemUI.IsOneTimeBuyItem(item) && playerInventory.HasItem(item);
        }

        /// <summary>
        /// Returns how many of this item the player currently owns.
        /// For one-time-buy items: 0 or 1. For consumables: count in ownedItems list.
        /// </summary>
        public int GetOwnedCount(BaseItem item)
        {
            if (playerInventory == null) return 0;

            int count = 0;
            foreach (var owned in playerInventory.ownedItems)
            {
                if (owned == item) count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the max amount for display.
        /// One-time-buy items (Artifact/Character): 1
        /// Consumable items: maxStackSize from Item SO
        /// </summary>
        public int GetMaxCount(BaseItem item)
        {
            if (item is Item consumable)
                return consumable.maxStackSize;
            
            // Artifact and Character are always 1
            return 1;
        }
    }
}
