namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable fixed retry strategy.
/// </summary>
public sealed record FixedRetryStrategyArtifact(Duration Delay) : IRetryStrategyArtifact
{
    /// <summary>
    /// Gets fixed strategy type.
    /// </summary>
    public RetryStrategyType Type => RetryStrategyType.Fixed;
}
