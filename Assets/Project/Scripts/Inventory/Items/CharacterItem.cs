using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Character Item", menuName = "Inventory/Character", order = 2)]
    public class CharacterItem : BaseItem
    {
        [Header("Character Specifics")]
        public GameObject characterPrefab;
        // Can add starting stats or character-specific logic here
    }
}
