using UnityEngine;

namespace Project.InventorySystem.Items
{
    public enum ConsumableType
    {
        None,
        // LEGACY: Run Global Level rewards replaced individual EXP potions.
        LevelingPotion
    }

    [CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item", order = 0)]
    public class Item : BaseItem
    {
        [Header("Item Specifics")]
        public int maxStackSize = 99;

        [Header("Consumable Properties")]
        public ConsumableType consumableType = ConsumableType.None;
        
        [Tooltip("LEGACY: EXP granted by the disabled individual Leveling Potion flow.")]
        public int expAmount = 0;
    }
}
