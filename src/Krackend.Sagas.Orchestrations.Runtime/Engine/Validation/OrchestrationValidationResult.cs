namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

using System.Text.Json.Nodes;

/// <summary>
/// Represents the outcome of a runtime validation.
/// </summary>
public sealed record OrchestrationValidationResult
{
    /// <summary>
    /// Gets a value indicating whether validation passed.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the normalized error code when validation failed.
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    /// Gets the validation error message when validation failed.
    /// </summary>
    public string ErrorMessage { get; init; }

    /// <summary>
    /// Gets validation diagnostics.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> Diagnostics { get; init; } = new Dictionary<string, JsonNode>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result.</returns>
    public static OrchestrationValidationResult Success()
        => new() { Succeeded = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errorCode">Normalized error code.</param>
    /// <param name="errorMessage">Error message.</param>
    /// <param name="diagnostics">Validation diagnostics.</param>
    /// <returns>A failed validation result.</returns>
    public static OrchestrationValidationResult Failure(
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
