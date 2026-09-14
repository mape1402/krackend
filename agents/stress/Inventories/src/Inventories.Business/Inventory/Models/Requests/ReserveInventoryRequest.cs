using Inventories.Business.Inventory.Models.Responses;
using Pelican.Mediator;

namespace Inventories.Business.Inventory.Models.Requests
{
    public sealed class ReserveInventoryRequest : IRequest<ReserveInventoryResponse>
    {
        public string SaleId { get; set; }

        public string CustomerId { get; set; }

        public decimal Total { get; set; }
    }
}
