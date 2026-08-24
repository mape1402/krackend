namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a retry strategy that uses a fixed delay for each retry attempt.
/// </summary>
public class FixedRetryStrategy : IRetryStrategy
{
    /// <summary>
    /// Gets the retry strategy type.
    /// </summary>
    public RetryStrategyType Type => RetryStrategyType.Fixed;

    /// <summary>
    /// Gets or sets the delay applied between retry attempts.
    /// </summary>
    public Duration Delay { get; set; }
}
