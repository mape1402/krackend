namespace Krackend.Sagas.Orchestrations.Client.Publishing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    public Task PublishAsync(object payload, OrchestrationReplyAddress address, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("No orchestration client publisher has been configured.");
}
