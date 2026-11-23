using Pelican.Mediator;
using Krackend.Demo;
using Inventories.Business.Inventory.Models.Requests;
using Inventories.Business.Inventory.Models.Responses;
namespace Inventories.Business.Inventory.Commands;
public sealed class ReserveInventoryCommandHandler(IDemoBusinessStore store) : IRequestHandler<ReserveInventoryRequest, ReserveInventoryResponse>
{
    public async Task<ReserveInventoryResponse> Handle(ReserveInventoryRequest request, CancellationToken cancellationToken)
    {
        var saleId = string.IsNullOrWhiteSpace(request.SaleId)
            ? (string.IsNullOrWhiteSpace(request.RequestId) ? Guid.NewGuid().ToString("N") : request.RequestId)
            : request.SaleId;
        var input = new DemoRequest { SaleId = saleId, RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? "legacy-" + saleId : request.RequestId,
            TestRunId = string.IsNullOrWhiteSpace(request.TestRunId) ? "legacy" : request.TestRunId,
            CustomerId = request.CustomerId, Total = request.Total,
            Lines = request.Lines.Count == 0 ? [new DemoLine("demo-sku", 1, request.Total)] : request.Lines };
        await store.ApplyAsync(new DemoCommand("inventories.reserve", input), cancellationToken);
        return new ReserveInventoryResponse { SaleId = saleId, ReservationId = "sale:" + saleId, Status = "Reserved" };
    }
}
