using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Command produced by the engine when a messaging task must be dispatched.
/// </summary>
public sealed class MessagingDispatchCommand
{
    /// <summary>
    /// Gets the runtime command identifier.
    /// </summary>
    public required string CommandId { get; init; }

    /// <summary>
    /// Gets the correlation identifier shared with external services.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the topic or queue where the task command must be sent.
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// Gets the message contract version for the outgoing task command.
    /// </summary>
    public required string MessageVersion { get; init; }

    /// <summary>
    /// Gets the business payload sent to the external service.
    /// </summary>
    public required JsonNode Payload { get; init; }

    /// <summary>
    /// Gets the stable orchestration key.
    /// </summary>
    public required string OrchestrationDefinitionKey { get; init; }

    /// <summary>
    /// Gets the deployed orchestration artifact version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets the runtime orchestration instance identifier.
    /// </summary>
    public required string OrchestrationInstanceId { get; init; }

    /// <summary>
    /// Gets the runtime task execution identifier.
    /// </summary>
    public required string TaskExecutionId { get; init; }

    /// <summary>
    /// Gets the runtime dispatch identifier.
    /// </summary>
    public required string DispatchId { get; init; }

    /// <summary>
    /// Gets the runtime environment key.
    /// </summary>
    public required string EnvironmentKey { get; init; }

    /// <summary>
    /// Gets the current stage key.
    /// </summary>
    public required string StageKey { get; init; }

    /// <summary>
    /// Gets the current task key.
    /// </summary>
    public required string TaskKey { get; init; }

    /// <summary>
    /// Gets the current task status when dispatching.
    /// </summary>
    public required string CurrentStatus { get; init; }

    /// <summary>
    /// Gets the current task attempt number.
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the task attempt started.
    /// </summary>
    public DateTime StartedOnUtc { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the dispatch metadata was produced.
    /// </summary>
    public DateTime UpdatedOnUtc { get; init; }
}
