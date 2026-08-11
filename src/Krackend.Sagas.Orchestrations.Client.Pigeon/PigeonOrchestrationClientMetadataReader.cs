using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

namespace Krackend.Sagas.Orchestrations.Client.Pigeon;

/// <summary>
/// Reads incoming orchestration metadata from the Pigeon-backed metadata context.
/// </summary>
public sealed class PigeonOrchestrationClientMetadataReader : IOrchestrationClientMetadataReader
{
    private readonly IOrchestratorMetadataAccessor _metadataAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PigeonOrchestrationClientMetadataReader"/> class.
    /// </summary>
    public PigeonOrchestrationClientMetadataReader(IOrchestratorMetadataAccessor metadataAccessor)
    {
        _metadataAccessor = metadataAccessor ?? throw new ArgumentNullException(nameof(metadataAccessor));
    }

    /// <inheritdoc/>
    public OrchestrationClientMetadata Read()
    {
        var metadata = _metadataAccessor.Current;

        return metadata is null
            ? null
            : new OrchestrationClientMetadata
            {
                SagaId = metadata.SagaId,
                OrchestrationId = metadata.OrchestrationId,
                OrchestrationKey = metadata.OrchestrationKey,
                OrchestrationVersion = OrchestrationSemanticVersion.Parse(metadata.OrchestrationVersion),
                OrchestrationInstanceId = metadata.OrchestrationInstanceId,
                TaskExecutionId = metadata.TaskExecutionId,
                DispatchId = metadata.DispatchId,
                CorrelationId = metadata.CorrelationId,
                CurrentState = metadata.CurrentState,
                ResponseTopic = metadata.ResponseTopic,
                ResponseVersion = OrchestrationSemanticVersion.Parse(metadata.ResponseVersion),
                Environment = metadata.Environment
            };
    }
}
