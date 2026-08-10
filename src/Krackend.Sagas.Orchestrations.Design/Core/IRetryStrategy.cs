namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines the strategy contract used by retry policies in design-time models.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Gets the retry strategy type represented by this configuration.
    /// </summary>
    RetryStrategyType Type { get; }
}
