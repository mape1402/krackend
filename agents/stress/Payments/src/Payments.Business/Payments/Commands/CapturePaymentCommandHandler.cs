using Pelican.Mediator;
using Krackend.Demo;
using Payments.Business.Payments.Models.Requests;
using Payments.Business.Payments.Models.Responses;
namespace Payments.Business.Payments.Commands;
public sealed class CapturePaymentCommandHandler(IDemoBusinessStore store) : IRequestHandler<CapturePaymentRequest, CapturePaymentResponse>
{
    public async Task<CapturePaymentResponse> Handle(CapturePaymentRequest request, CancellationToken cancellationToken)
    {
        var saleId = string.IsNullOrWhiteSpace(request.SaleId)
            ? (string.IsNullOrWhiteSpace(request.RequestId) ? Guid.NewGuid().ToString("N") : request.RequestId)
            : request.SaleId;
        var input = new DemoRequest { SaleId = saleId, RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? "legacy-" + saleId : request.RequestId,
            TestRunId = string.IsNullOrWhiteSpace(request.TestRunId) ? "legacy" : request.TestRunId,
            CustomerId = request.CustomerId, Total = request.Total,
            Lines = request.Lines.Count == 0 ? [new DemoLine("demo-sku", 1, request.Total)] : request.Lines };
        await store.ApplyAsync(new DemoCommand("payments.capture", input), cancellationToken);
        return new CapturePaymentResponse { SaleId = saleId, PaymentId = "payment:" + saleId, Status = "Captured" };
    }
}
