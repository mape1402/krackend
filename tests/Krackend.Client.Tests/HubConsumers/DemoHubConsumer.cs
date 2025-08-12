using Krackend.Sagas.Orchestration.Worker.Pipelines;
using Pigeon.Messaging.Consuming.Dispatching;
using Spider.Pipelines.Core;

namespace Krackend.Client.Tests.HubConsumers
{
    public class DemoHubConsumer : HubConsumer
    {
        private readonly ISpider _spider;

        public DemoHubConsumer(ISpider spider)
        {
            _spider = spider ?? throw new ArgumentNullException(nameof(spider));
        }

        [Consumer("commands.opportunities.create_opportunity")]
        public async Task Consume(object payload, CancellationToken cancellationToken = default)
            => await _spider
                        .InitBridge<IService>()
                        .Attach<object>(pipeline =>
                        {
                            pipeline
                                .DefaultForwading();
                        })
                        .ExecuteAsync(service => service.Process, payload, cancellationToken);
    }

    public interface IService
    {
        Task Process(object payload, CancellationToken cancellationToken = default);
    }

    public class DemoService : IService
    {
        public Task Process(object payload, CancellationToken cancellationToken = default)
        {
            // Simulate processing the payload
            Console.WriteLine($"Processing payload: {payload.ToString()}");
            return Task.CompletedTask;
        }
    }
}
