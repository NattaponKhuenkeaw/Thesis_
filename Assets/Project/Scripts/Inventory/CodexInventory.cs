using System.Collections.Generic;
using UnityEngine;
using Project.InventorySystem.Items;

namespace Project.InventorySystem
{
    [CreateAssetMenu(fileName = "New Codex Inventory", menuName = "Inventory/Codex Inventory", order = 10)]
    public class CodexInventory : ScriptableObject
    {
        [Header("Master Data")]
        [Tooltip("Contains every single item that exists in the overall game.")]
        public List<BaseItem> allItems = new List<BaseItem>();

        /// <summary>
        /// Retrieves items of a specific type (e.g., only Artifacts, only Characters)
        /// </summary>
        public List<T> GetItemsByType<T>() where T : BaseItem
        {
            List<T> specificItems = new List<T>();
            foreach (var item in allItems)
            {
                if (item is T typedItem)
                {
                    specificItems.Add(typedItem);
                }
            }
            return specificItems;
        }

        /// <summary>
        /// Gets a random item from the codex.
        /// </summary>
        public BaseItem GetRandomItem()
        {
            if (allItems == null || allItems.Count == 0) return null;
            return allItems[Random.Range(0, allItems.Count)];
        }
    }
}
