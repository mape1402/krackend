using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Command used by the runtime to continue an orchestration from a messaging back-channel response.
/// </summary>
public sealed class RuntimeMessageResponseCommand
{
    /// <summary>
    /// Gets the orchestration instance identifier carried by response metadata.
    /// </summary>
    public required string OrchestrationInstanceId { get; init; }

    /// <summary>
    /// Gets the task execution identifier carried by response metadata.
    /// </summary>
    public required string TaskExecutionId { get; init; }

    /// <summary>
    /// Gets the dispatch identifier carried by response metadata.
    /// </summary>
    public required string DispatchId { get; init; }

    /// <summary>
    /// Gets the response correlation identifier.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the business response payload.
    /// </summary>
    public JsonNode Payload { get; init; }
}
