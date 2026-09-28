namespace Krackend.Sagas.Orchestrations.Client.Metadata;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationPropagationMetadataAccessor :
    IOrchestrationPropagationMetadataAccessor,
    IOrchestrationPropagationMetadataSetter
{
    private readonly AsyncLocal<OrchestrationPropagationMetadata> _metadata = new();

    public OrchestrationPropagationMetadata Get()
        => _metadata.Value?.Clone() ?? new OrchestrationPropagationMetadata();

    public void Set(OrchestrationPropagationMetadata metadata)
        => _metadata.Value = metadata?.Clone() ?? new OrchestrationPropagationMetadata();

    public void Clear()
        => _metadata.Value = new OrchestrationPropagationMetadata();
}
