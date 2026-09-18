namespace Krackend.Sagas.Orchestrations.Client.Metadata;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class DefaultOrchestrationExecutionResultMetadataAccessor :
    IOrchestrationExecutionResultMetadataAccessor,
    IOrchestrationExecutionResultMetadataSetter
{
    private readonly AsyncLocal<OrchestrationExecutionResultMetadata> _metadata = new();

    public OrchestrationExecutionResultMetadata Get()
        => _metadata.Value;

    public void Set(OrchestrationExecutionResultMetadata metadata)
        => _metadata.Value = metadata;

    public void Clear()
        => _metadata.Value = null;
}
