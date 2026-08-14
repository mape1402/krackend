using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Protocol-neutral runtime task dispatch request.
/// </summary>
public sealed class RuntimeTaskDispatchRequest
{
    public required string CommandId { get; init; }

    public required string CorrelationId { get; init; }

    public required string TaskKind { get; init; }

    public required string DispatchType { get; init; }

    public required string Destination { get; init; }

    public required string MessageVersion { get; init; }

    public required JsonNode Payload { get; init; }

    public required string OrchestrationDefinitionKey { get; init; }

    public required string OrchestrationVersion { get; init; }

    public required string OrchestrationInstanceId { get; init; }

    public required string TaskExecutionId { get; init; }

    public required string DispatchId { get; init; }

    public required string EnvironmentKey { get; init; }

    public required string StageKey { get; init; }

    public required string TaskKey { get; init; }

    public required string CurrentStatus { get; init; }

    public int Attempt { get; init; }

    public DateTime StartedOnUtc { get; init; }

    public DateTime UpdatedOnUtc { get; init; }

    public Dictionary<string, JsonNode> Metadata { get; init; } = new();
}
