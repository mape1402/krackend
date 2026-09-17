namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;

using System.Text.Json.Nodes;

/// <summary>
/// Represents the result of evaluating an orchestration condition.
/// </summary>
public sealed record OrchestrationConditionEvaluationResult
{
    /// <summary>
    /// Gets whether the condition was evaluated successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets whether the owning element should execute.
    /// </summary>
    public bool ShouldExecute { get; init; }

    /// <summary>
    /// Gets the failure code when evaluation fails.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the failure message when evaluation fails.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets additional diagnostic metadata emitted by the condition adapter.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> Diagnostics { get; init; } =
        new Dictionary<string, JsonNode>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a successful condition result.
    /// </summary>
    /// <param name="shouldExecute">Whether the element should execute.</param>
    /// <returns>A successful condition result.</returns>
    public static OrchestrationConditionEvaluationResult Success(bool shouldExecute)
        => new()
        {
            Succeeded = true,
            ShouldExecute = shouldExecute
        };

    /// <summary>
    /// Creates a failed condition result.
    /// </summary>
    /// <param name="errorCode">Failure code.</param>
    /// <param name="errorMessage">Failure message.</param>
    /// <param name="diagnostics">Optional diagnostic metadata.</param>
    /// <returns>A failed condition result.</returns>
    public static OrchestrationConditionEvaluationResult Failure(
        string errorCode,
        string errorMessage,
        IReadOnlyDictionary<string, JsonNode> diagnostics = null)
        => new()
        {
            Succeeded = false,
            ShouldExecute = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Diagnostics = diagnostics ?? new Dictionary<string, JsonNode>(StringComparer.Ordinal)
        };
}
