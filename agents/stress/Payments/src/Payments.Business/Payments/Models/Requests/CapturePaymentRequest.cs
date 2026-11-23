using Payments.Business.Payments.Models.Responses;
using Pelican.Mediator;

namespace Payments.Business.Payments.Models.Requests
{
    public sealed class CapturePaymentRequest : IRequest<CapturePaymentResponse>
    {
        public string SaleId { get; set; }
        public string RequestId { get; set; }
        public string TestRunId { get; set; }
        public List<Krackend.Demo.DemoLine> Lines { get; set; } = [];

        public string CustomerId { get; set; }

        public decimal Total { get; set; }
    }
}
