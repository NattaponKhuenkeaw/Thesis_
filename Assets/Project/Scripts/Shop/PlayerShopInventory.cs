using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;

namespace Project.ShopSystem
{
    [CreateAssetMenu(fileName = "New Player Shop Inventory", menuName = "Shop/Player Shop Inventory", order = 11)]
    public class PlayerShopInventory : ScriptableObject
    {
        [Header("Player Owned Items")]
        [Tooltip("Items the player has already purchased or acquired.")]
        public List<BaseItem> ownedItems = new List<BaseItem>();

        /// <summary>
        /// Checks if the player already owns the specified item.
        /// </summary>
        public bool HasItem(BaseItem item)
        {
            return ownedItems.Contains(item);
        }

        /// <summary>
        /// Adds an item to the player's inventory.
        /// </summary>
        public void AddItem(BaseItem item)
        {
            if (!ownedItems.Contains(item))
            {
                ownedItems.Add(item);
            }
        }
        
        /// <summary>
        /// Removes an item from the player's inventory.
        /// </summary>
        public void RemoveItem(BaseItem item)
        {
            if (ownedItems.Contains(item))
            {
                ownedItems.Remove(item);
            }
        }
        
        /// <summary>
        /// Clears all items (e.g. for a new save or reset).
        /// </summary>
        public void ClearInventory()
        {
            ownedItems.Clear();
        }
    }
}
