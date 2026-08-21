namespace Krackend.Sagas.Orchestrations.Client.Metadata;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationMessageMetadataAccessor :
    IOrchestrationMessageMetadataAccessor,
    IOrchestrationMessageMetadataSetter
{
    private OrchestrationMessageMetadata _metadata = new();

    public OrchestrationMessageMetadata Get()
        => _metadata;

    public void Set(OrchestrationMessageMetadata metadata)
        => _metadata = metadata ?? new OrchestrationMessageMetadata();
}
