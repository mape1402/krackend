using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

namespace Krackend.Sagas.Orchestrations.Client.Pigeon;

/// <summary>
/// Maps client metadata back to the runtime metadata contract used by Pigeon interceptors.
/// </summary>
internal static class PigeonOrchestrationMetadataMapper
{
    public static OrchestratorMessageMetadata ToRuntimeMetadata(OrchestrationClientMetadata metadata)
    {
        if (metadata is null)
            return null;

        return new OrchestratorMessageMetadata
        {
            SagaId = metadata.SagaId,
            OrchestrationId = metadata.OrchestrationId,
            OrchestrationKey = metadata.OrchestrationKey,
            OrchestrationVersion = metadata.OrchestrationVersion.ToString(),
            OrchestrationInstanceId = metadata.OrchestrationInstanceId,
            TaskExecutionId = metadata.TaskExecutionId,
            DispatchId = metadata.DispatchId,
            CorrelationId = metadata.CorrelationId,
            CurrentState = metadata.CurrentState as OrchestrationRuntimeState
                ?? throw new InvalidOperationException("Client metadata current state must be an OrchestrationRuntimeState when publishing through Pigeon."),
            ResponseTopic = metadata.ResponseTopic,
            ResponseVersion = metadata.ResponseVersion.ToString(),
            Environment = metadata.Environment
        };
    }
}
