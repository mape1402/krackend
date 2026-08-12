namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime-ready retry policy.
/// </summary>
public sealed class RuntimeRetryPolicy
{
    /// <summary>
    /// Gets maximum retries after the first attempt.
    /// </summary>
    public int MaxRetries { get; init; }

    /// <summary>
    /// Gets total attempts including the first attempt.
    /// </summary>
    public int MaxAttempts => Math.Max(1, MaxRetries + 1);

    /// <summary>
    /// Gets retry strategy type.
    /// </summary>
    public string StrategyType { get; init; } = "Fixed";
}
