namespace Sales.Business.Sales.Models.Responses
{
    public sealed class SaleResponse
    {
        public string SaleId { get; set; }

        public string CustomerId { get; set; }

        public decimal Total { get; set; }

        public string Status { get; set; }
    }
}
