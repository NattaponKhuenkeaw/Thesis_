using UnityEngine;

namespace Project.InventorySystem.Items
{
    public enum ConsumableType
    {
        None,
        LevelingPotion
    }

    [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item", order = 0)]
    public class Item : BaseItem
    {
        [Header("Item Specifics")]
        public int maxStackSize = 99;

        [Header("Consumable Properties")]
        public ConsumableType consumableType = ConsumableType.None;
        
        [Tooltip("EXP granted per unit consumed (for LevelingPotion type)")]
        public int expAmount = 0;
    }
}
