using Pelican.Mediator;
using Krackend.Demo;
using Sales.Business.Sales.Models.Requests;
using Sales.Business.Sales.Models.Responses;
namespace Sales.Business.Sales.Commands;
public sealed class CreateSaleCommandHandler(IDemoBusinessStore store) : IRequestHandler<CreateSaleRequest, SaleResponse>
{
    public async Task<SaleResponse> Handle(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        var saleId = string.IsNullOrWhiteSpace(request.SaleId)
            ? (string.IsNullOrWhiteSpace(request.RequestId) ? Guid.NewGuid().ToString("N") : request.RequestId)
            : request.SaleId;
        var input = new DemoRequest { SaleId = saleId, RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? "legacy-" + saleId : request.RequestId,
            TestRunId = string.IsNullOrWhiteSpace(request.TestRunId) ? "legacy" : request.TestRunId,
            CustomerId = request.CustomerId, Total = request.Total,
            Lines = request.Lines.Count == 0 ? [new DemoLine("demo-sku", 1, request.Total)] : request.Lines };
        await store.ApplyAsync(new DemoCommand("sales.create", input), cancellationToken);
        return new SaleResponse { SaleId = saleId, CustomerId = request.CustomerId, Total = request.Total, Status = "Created" };
    }
}
