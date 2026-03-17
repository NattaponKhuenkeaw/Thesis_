using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item", order = 0)]
    public class Item : BaseItem
    {
        [Header("Item Specifics")]
        public int maxStackSize = 99;
        public bool isConsumable = true;
    }
}
