namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Describes the data needed to prepare a task dispatch request payload.
/// </summary>
public sealed record TaskDispatchRequestPayloadPreparationRequest
{
    /// <summary>
    /// Gets the orchestration instance that owns the dispatch.
    /// </summary>
    public required OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the stage key that owns the task.
    /// </summary>
    public required string StageKey { get; init; }

    /// <summary>
    /// Gets the task artifact being dispatched.
    /// </summary>
    public required TaskArtifact Task { get; init; }

    /// <summary>
    /// Gets the messaging task configuration.
    /// </summary>
    public required MessagingTaskConfigurationArtifact MessagingConfiguration { get; init; }

    /// <summary>
    /// Gets the optional payload explicitly carried by the decision.
    /// </summary>
    public string Payload { get; init; }
}
