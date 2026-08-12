using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Result of a runtime payload transformation.
/// </summary>
public sealed class RuntimePayloadTransformation
{
    /// <summary>
    /// Gets transformed payload.
    /// </summary>
    public required JsonNode Payload { get; init; }

    /// <summary>
    /// Gets a value indicating whether a configured transformation was applied.
    /// </summary>
    public bool WasTransformed { get; init; }

    /// <summary>
    /// Gets transformation engine.
    /// </summary>
    public string Engine { get; init; } = string.Empty;
}
