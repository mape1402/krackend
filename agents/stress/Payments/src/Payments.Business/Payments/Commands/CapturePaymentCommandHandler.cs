using Payments.Business.Payments.Models.Requests;
using Payments.Business.Payments.Models.Responses;
using Pelican.Mediator;

namespace Payments.Business.Payments.Commands
{
    public sealed class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentRequest, CapturePaymentResponse>
    {
        public Task<CapturePaymentResponse> Handle(CapturePaymentRequest request, CancellationToken cancellationToken)
        {
            var paymentId = Guid.NewGuid().ToString("N");

            Console.WriteLine($"Payment captured. SaleId: {request.SaleId}, PaymentId: {paymentId}, Total: {request.Total}");

            return Task.FromResult(new CapturePaymentResponse
            {
                SaleId = request.SaleId,
                PaymentId = paymentId,
                Status = "Captured"
            });
        }
    }
}
