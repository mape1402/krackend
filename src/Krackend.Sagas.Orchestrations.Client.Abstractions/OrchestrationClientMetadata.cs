namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Orchestrator metadata extracted by the client before business execution.
/// </summary>
public sealed class OrchestrationClientMetadata
{
    /// <summary>
    /// Gets the saga identifier used to group related orchestration messages.
    /// </summary>
    public required string SagaId { get; init; }

    /// <summary>
    /// Gets the orchestration identifier known to the runtime.
    /// </summary>
    public required string OrchestrationId { get; init; }

    /// <summary>
    /// Gets the stable orchestration key.
    /// </summary>
    public required string OrchestrationKey { get; init; }

    /// <summary>
    /// Gets the deployed orchestration version.
    /// </summary>
    public required OrchestrationSemanticVersion OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets the runtime orchestration instance identifier.
    /// </summary>
    public required string OrchestrationInstanceId { get; init; }

    /// <summary>
    /// Gets the current task execution identifier.
    /// </summary>
    public required string TaskExecutionId { get; init; }

    /// <summary>
    /// Gets the outbound dispatch identifier being answered.
    /// </summary>
    public required string DispatchId { get; init; }

    /// <summary>
    /// Gets the correlation identifier shared with the runtime.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the runtime orchestration state snapshot carried by the transport adapter.
    /// </summary>
    public required object CurrentState { get; init; }

    /// <summary>
    /// Gets the topic or queue where orchestration output should be published.
    /// </summary>
    public required string ResponseTopic { get; init; }

    /// <summary>
    /// Gets the response contract version expected by the runtime.
    /// </summary>
    public required OrchestrationSemanticVersion ResponseVersion { get; init; }

    /// <summary>
    /// Gets the runtime environment key that owns this orchestration execution.
    /// </summary>
    public required string Environment { get; init; }
}
