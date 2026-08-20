namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Client.Publishing;
using global::Pigeon.Messaging.Producing;

internal sealed class PigeonOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    private readonly IProducer _producer;

    public PigeonOrchestrationClientPublisher(IProducer producer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public async Task PublishAsync(object payload, string topic, string version, CancellationToken cancellationToken = default)
        => await _producer.PublishAsync(payload, topic, version, cancellationToken);
}
