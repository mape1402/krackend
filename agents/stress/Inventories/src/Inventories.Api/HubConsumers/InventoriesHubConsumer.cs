using Inventories.Business.Inventory.Models.Requests;
using Inventories.Business.Inventory.Models.Responses;
using Pigeon.Messaging.Consuming.Dispatching;
using Spider.Pipelines.Core;
using TurtlePath.Spider;

namespace Inventories.Api.HubConsumers
{
    public sealed class InventoriesHubConsumer : BaseHubConsumer
    {
        [Consumer("commands.inventories.stock.reserve", "1.1.0")]
        [Consumer("commands.inventories.stock.reserve", "1.0.0")]
        [Consumer("tasks.inventories.reserve.requested", "1.0.0")]
        public async Task Consume(ReserveInventoryRequest request, CancellationToken cancellationToken)
        {
            await Spider
                .AsMediator()
                .UseOrchestration<ReserveInventoryRequest, ReserveInventoryResponse>()
                .Send(request, cancellationToken);
        }
    }
}
