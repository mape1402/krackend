using Pelican.Mediator;
using Sales.Business.Sales.Models.Requests;
using Sales.Business.Sales.Models.Responses;

namespace Sales.Business.Sales.Commands
{
    public sealed class CreateSaleCommandHandler : IRequestHandler<CreateSaleRequest, SaleResponse>
    {
        public Task<SaleResponse> Handle(CreateSaleRequest request, CancellationToken cancellationToken)
        {
            var saleId = string.IsNullOrWhiteSpace(request.SaleId)
                ? Guid.NewGuid().ToString("N")
                : request.SaleId;

            Console.WriteLine($"Sale created flow accepted. SaleId: {saleId}, CustomerId: {request.CustomerId}, Total: {request.Total}");

            return Task.FromResult(new SaleResponse
            {
                SaleId = saleId,
                CustomerId = request.CustomerId,
                Total = request.Total,
                Status = "Created"
            });
        }
    }
}
