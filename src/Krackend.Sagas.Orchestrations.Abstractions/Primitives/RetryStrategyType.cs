namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Enumerates retry strategy families supported by orchestration policies.
/// </summary>
public enum RetryStrategyType
{
    /// <summary>
    /// Uses a fixed delay between retries.
    /// </summary>
    Fixed,

    /// <summary>
    /// Uses an exponentially increasing delay between retries.
    /// </summary>
    Exponential,

    /// <summary>
    /// Uses a custom retry strategy implementation.
    /// </summary>
    Custom
}
