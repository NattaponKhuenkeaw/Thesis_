using Project.InventorySystem.Items;

namespace Project.ShopSystem
{
    public enum ShopSellEntryKind
    {
        Unit,
        Fragment,
        ArtifactInstance
    }

    public sealed class ShopSellEntry
    {
        public ShopSellEntry(BaseItem item, ShopSellEntryKind kind, int quantity, int price, string displayName, string artifactInstanceId = null)
        {
            Item = item;
            Kind = kind;
            Quantity = quantity;
            Price = price;
            DisplayName = displayName;
            ArtifactInstanceId = artifactInstanceId;
        }

        public BaseItem Item { get; }
        public ShopSellEntryKind Kind { get; }
        public int Quantity { get; }
        public int Price { get; }
        public string DisplayName { get; }
        public string ArtifactInstanceId { get; }
    }
}
