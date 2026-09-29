namespace Krackend.Sagas.Orchestrations.Client.Metadata;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationTriggerMetadataAccessor : IOrchestrationTriggerMetadataAccessor
{
    public ValueTask<OrchestrationTriggerMetadata> GetAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new OrchestrationTriggerMetadata());
}
