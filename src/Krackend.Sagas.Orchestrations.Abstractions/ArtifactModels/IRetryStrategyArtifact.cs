namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable retry-strategy contract.
/// </summary>
public interface IRetryStrategyArtifact
{
    /// <summary>
    /// Gets retry strategy type.
    /// </summary>
    RetryStrategyType Type { get; }
}
