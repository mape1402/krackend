namespace Sales.Business.Sales.Events
{
    public sealed class SaleCreatedMessage
    {
        public string SaleId { get; set; }

        public string CustomerId { get; set; }

        public decimal Total { get; set; }

        public DateTime OccurredOnUtc { get; set; }
    }
}
