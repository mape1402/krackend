namespace Payments.Business.Payments.Messages
{
    public sealed class CapturePaymentMessage
    {
        public string SaleId { get; set; }

        public string CustomerId { get; set; }

        public decimal Total { get; set; }
    }
}
