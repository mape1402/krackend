using Payments.Business.Payments.Models.Responses;
using Pelican.Mediator;

namespace Payments.Business.Payments.Models.Requests
{
    public sealed class CapturePaymentRequest : IRequest<CapturePaymentResponse>
    {
        public string SaleId { get; set; }

        public string CustomerId { get; set; }

        public decimal Total { get; set; }
    }
}
