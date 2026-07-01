using Project.InventorySystem.Items;

namespace Project.ShopSystem
{
    public enum ShopSellEntryKind
    {
        Unit,
        Fragment
    }

    public sealed class ShopSellEntry
    {
        public ShopSellEntry(BaseItem item, ShopSellEntryKind kind, int quantity, int price, string displayName)
        {
            Item = item;
            Kind = kind;
            Quantity = quantity;
            Price = price;
            DisplayName = displayName;
        }

        public BaseItem Item { get; }
        public ShopSellEntryKind Kind { get; }
        public int Quantity { get; }
        public int Price { get; }
        public string DisplayName { get; }
    }
}
