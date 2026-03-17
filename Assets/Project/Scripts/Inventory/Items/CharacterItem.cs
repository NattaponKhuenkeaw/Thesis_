using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Character Item", menuName = "Inventory/Character", order = 2)]
    public class CharacterItem : BaseItem
    {
        [Header("Character Specifics")]
        public CharacterObject characterReference;

        /// <summary>
        /// Auto-sync BaseItem fields (itemName, icon, description) from the CharacterObject reference.
        /// Call this when displaying the character to ensure data stays in sync.
        /// </summary>
        public void SyncFromCharacterReference()
        {
            if (characterReference == null) return;

            if (characterReference.metaData != null)
            {
                if (!string.IsNullOrEmpty(characterReference.metaData.displayName))
                    itemName = characterReference.metaData.displayName;

                if (characterReference.metaData.portrait != null)
                    icon = characterReference.metaData.portrait;

                if (!string.IsNullOrEmpty(characterReference.metaData.description))
                    description = characterReference.metaData.description;
            }
        }
    }
}
