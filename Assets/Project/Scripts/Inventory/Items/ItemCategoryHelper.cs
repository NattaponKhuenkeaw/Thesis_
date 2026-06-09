namespace Project.InventorySystem.Items
{
    public static class ItemCategoryHelper
    {
        public static bool IsOneTimeBuy(BaseItem item)
        {
            return (item is Artifact || item is CharacterItem);
        }

        public static int GetSortOrder(BaseItem item)
        {
            if (item is Item)          return 0; // Consumable
            if (item is CharacterItem) return 1; // Character
            if (item is Artifact)      return 2; // Artifact
            return 3; // Unknown fallback
        }
    }
}
