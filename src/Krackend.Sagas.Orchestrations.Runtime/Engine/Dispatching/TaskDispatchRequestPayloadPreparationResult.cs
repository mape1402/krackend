namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using System.Text.Json.Nodes;

/// <summary>
/// Represents a prepared task dispatch request payload.
/// </summary>
public sealed record TaskDispatchRequestPayloadPreparationResult
{
    /// <summary>
    /// Gets the business payload that must be sent to the task destination.
    /// </summary>
    public JsonNode Payload { get; init; }
}
