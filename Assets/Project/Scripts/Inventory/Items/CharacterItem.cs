using UnityEngine;

namespace Project.InventorySystem.Items
{
    [CreateAssetMenu(fileName = "New Character Item", menuName = "Inventory/Character", order = 2)]
    public class CharacterItem : BaseItem
    {
        [Header("Character Specifics")]
        public CharacterObject characterObjectData;

        private void OnValidate()
        {
            SyncFromCharacterObjectData();
        }

        /// <summary>
        /// Auto-sync BaseItem fields (id, itemName, icon, description) from the CharacterObject reference.
        /// Call this when displaying the character to ensure data stays in sync.
        /// </summary>
        public void SyncFromCharacterObjectData()
        {
            if (characterObjectData == null) return;

            if (!string.IsNullOrEmpty(characterObjectData.characterId))
                id = characterObjectData.characterId;

            if (characterObjectData.metaData != null)
            {
                if (!string.IsNullOrEmpty(characterObjectData.metaData.displayName))
                    itemName = characterObjectData.metaData.displayName;

                if (characterObjectData.metaData.portrait != null)
                    icon = characterObjectData.metaData.portrait;

                if (!string.IsNullOrEmpty(characterObjectData.metaData.description))
                    description = characterObjectData.metaData.description;
            }
        }
    }
}
