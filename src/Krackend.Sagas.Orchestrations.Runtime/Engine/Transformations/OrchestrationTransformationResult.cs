namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;

using System.Text.Json.Nodes;

/// <summary>
/// Represents the result of a runtime payload transformation.
/// </summary>
public sealed record OrchestrationTransformationResult
{
    /// <summary>
    /// Gets a value indicating whether the transformation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the transformed business payload.
    /// </summary>
    public JsonNode Payload { get; init; }

    /// <summary>
    /// Gets the normalized error code when transformation failed.
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    /// Gets the transformation error message when it failed.
    /// </summary>
    public string ErrorMessage { get; init; }

    /// <summary>
    /// Gets transform diagnostics.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> Diagnostics { get; init; } = new Dictionary<string, JsonNode>();

    /// <summary>
    /// Creates a successful transformation result.
    /// </summary>
    /// <param name="payload">Transformed payload.</param>
    /// <returns>A successful result.</returns>
    public static OrchestrationTransformationResult Success(JsonNode payload)
        => new() { Succeeded = true, Payload = payload };

    /// <summary>
    /// Creates a failed transformation result.
    /// </summary>
    /// <param name="errorCode">Normalized error code.</param>
    /// <param name="errorMessage">Error message.</param>
    /// <param name="diagnostics">Transformation diagnostics.</param>
    /// <returns>A failed result.</returns>
    public static OrchestrationTransformationResult Failure(
        string errorCode,
        string errorMessage,
        IReadOnlyDictionary<string, JsonNode> diagnostics = null)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Diagnostics = diagnostics ?? new Dictionary<string, JsonNode>()
        };
}
