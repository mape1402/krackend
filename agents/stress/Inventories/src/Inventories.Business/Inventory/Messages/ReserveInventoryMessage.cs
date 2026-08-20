namespace Inventories.Business.Inventory.Messages
{
    public sealed class ReserveInventoryMessage
    {
        public string SaleId { get; set; }

        public string CustomerId { get; set; }

        public decimal Total { get; set; }
    }
}
