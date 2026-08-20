namespace Payments.Business.Payments.Models.Responses
{
    public sealed class CapturePaymentResponse
    {
        public string SaleId { get; set; }

        public string PaymentId { get; set; }

        public string Status { get; set; }
    }
}
