namespace Krackend.Sagas.Orchestrations.Client.Publishing;

internal sealed class DefaultOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    public Task PublishAsync(object payload, string topic, string version, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("No orchestration client publisher has been configured.");
}
