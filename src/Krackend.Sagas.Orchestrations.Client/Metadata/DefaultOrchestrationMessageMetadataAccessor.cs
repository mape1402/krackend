namespace Krackend.Sagas.Orchestrations.Client.Metadata;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationMessageMetadataAccessor :
    IOrchestrationMessageMetadataAccessor,
    IOrchestrationMessageMetadataSetter
{
    private readonly AsyncLocal<OrchestrationMessageMetadata> _metadata = new();

    public OrchestrationMessageMetadata Get()
        => _metadata.Value ?? new OrchestrationMessageMetadata();

    public void Set(OrchestrationMessageMetadata metadata)
        => _metadata.Value = metadata ?? new OrchestrationMessageMetadata();
}
