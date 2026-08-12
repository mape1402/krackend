namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Result of a runtime execution-condition evaluation.
/// </summary>
public sealed class RuntimeConditionEvaluation
{
    /// <summary>
    /// Gets a value indicating whether the element can execute.
    /// </summary>
    public bool ShouldExecute { get; init; }

    /// <summary>
    /// Gets a human-readable evaluation reason.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the normalized expression.
    /// </summary>
    public string Expression { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful evaluation.
    /// </summary>
    public static RuntimeConditionEvaluation Execute(string expression = "")
        => new() { ShouldExecute = true, Expression = expression };

    /// <summary>
    /// Creates a skipped evaluation.
    /// </summary>
    public static RuntimeConditionEvaluation Skip(string expression, string reason)
        => new() { ShouldExecute = false, Expression = expression, Reason = reason };
}
