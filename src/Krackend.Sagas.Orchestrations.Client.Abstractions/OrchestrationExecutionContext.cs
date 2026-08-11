namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Scoped client-side orchestration state captured around a business execution.
/// </summary>
public sealed class OrchestrationExecutionContext
{
    /// <summary>
    /// Gets the execution mode.
    /// </summary>
    public required OrchestrationClientExecutionMode Mode { get; init; }

    /// <summary>
    /// Gets orchestrator metadata when the execution is orchestrated.
    /// </summary>
    public OrchestrationClientMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the per-call output descriptor when the execution configured a manual output.
    /// </summary>
    public OrchestrationOutputDescriptor Output { get; init; }

    /// <summary>
    /// Gets a value indicating whether output can be published.
    /// </summary>
    public bool CanPublish => Metadata is not null || Output is not null;
}
