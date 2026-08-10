namespace Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;

/// <summary>
/// Complex metadata object carried as a single value under the orchestrator metadata envelope key.
/// </summary>
public sealed class OrchestratorMessageMetadata
{
    /// <summary>
    /// Gets the saga identifier used to group related orchestration messages.
    /// </summary>
    public required string SagaId { get; init; }

    /// <summary>
    /// Gets the orchestration definition identifier or key-level identifier known to the runtime.
    /// </summary>
    public required string OrchestrationId { get; init; }

    /// <summary>
    /// Gets the stable orchestration key.
    /// </summary>
    public required string OrchestrationKey { get; init; }

    /// <summary>
    /// Gets the deployed orchestration artifact version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets the runtime orchestration instance identifier.
    /// </summary>
    public required string OrchestrationInstanceId { get; init; }

    /// <summary>
    /// Gets the task execution identifier currently being dispatched or answered.
    /// </summary>
    public required string TaskExecutionId { get; init; }

    /// <summary>
    /// Gets the outbound dispatch identifier.
    /// </summary>
    public required string DispatchId { get; init; }

    /// <summary>
    /// Gets the correlation identifier used by runtime and external services.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the current orchestration state snapshot.
    /// </summary>
    public required OrchestrationRuntimeState CurrentState { get; init; }

    /// <summary>
    /// Gets the topic or queue where the external service must publish its response.
    /// </summary>
    public required string ResponseTopic { get; init; }

    /// <summary>
    /// Gets the response message version, aligned with the deployed orchestration artifact version.
    /// </summary>
    public required string ResponseVersion { get; init; }

    /// <summary>
    /// Gets the runtime environment key that owns this orchestration execution.
    /// </summary>
    public required string Environment { get; init; }
}
