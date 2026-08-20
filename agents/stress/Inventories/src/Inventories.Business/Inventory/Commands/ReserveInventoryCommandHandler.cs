using Inventories.Business.Inventory.Models.Requests;
using Inventories.Business.Inventory.Models.Responses;
using Pelican.Mediator;

namespace Inventories.Business.Inventory.Commands
{
    public sealed class ReserveInventoryCommandHandler : IRequestHandler<ReserveInventoryRequest, ReserveInventoryResponse>
    {
        public Task<ReserveInventoryResponse> Handle(ReserveInventoryRequest request, CancellationToken cancellationToken)
        {
            var reservationId = Guid.NewGuid().ToString("N");

            Console.WriteLine($"Inventory reserved. SaleId: {request.SaleId}, ReservationId: {reservationId}");

            return Task.FromResult(new ReserveInventoryResponse
            {
                SaleId = request.SaleId,
                ReservationId = reservationId,
                Status = "Reserved"
            });
        }
    }
}
