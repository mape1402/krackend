namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

using System.Text.Json.Nodes;

/// <summary>
/// Represents the structured payload context available to a runtime task.
/// </summary>
public sealed record OrchestrationPayloadContext
{
    /// <summary>
    /// Gets the complete accumulated context.
    /// </summary>
    public JsonNode ContextPayload { get; init; }

    /// <summary>
    /// Gets the original trigger business payload.
    /// </summary>
    public JsonNode TriggerPayload { get; init; }

    /// <summary>
    /// Gets the stage key of the task being dispatched.
    /// </summary>
    public required string StageKey { get; init; }

    /// <summary>
    /// Gets the task key being dispatched.
    /// </summary>
    public required string TaskKey { get; init; }
}
