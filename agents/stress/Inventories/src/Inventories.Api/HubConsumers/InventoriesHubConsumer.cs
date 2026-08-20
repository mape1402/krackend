using Inventories.Business.Inventory.Messages;
using Inventories.Business.Inventory.Models.Requests;
using Inventories.Business.Inventory.Models.Responses;
using Pigeon.Messaging.Consuming.Dispatching;
using Spider.Pipelines.Core;
using TurtlePath.Spider;

namespace Inventories.Api.HubConsumers
{
    public sealed class InventoriesHubConsumer : BaseHubConsumer
    {
        [Consumer("tasks.inventories.reserve.requested", "1.0.0", "inventories")]
        public Task Consume(ReserveInventoryMessage message, CancellationToken cancellationToken)
        {
            return Spider
                .AsMediator()
                .UseOrchestration<ReserveInventoryRequest, ReserveInventoryResponse>()
                .Send(new ReserveInventoryRequest
                {
                    SaleId = message.SaleId,
                    CustomerId = message.CustomerId,
                    Total = message.Total
                }, cancellationToken);
        }
    }
}
