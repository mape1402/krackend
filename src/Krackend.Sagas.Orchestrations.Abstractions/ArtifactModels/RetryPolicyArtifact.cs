namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable retry-policy contract.
/// </summary>
public sealed record RetryPolicyArtifact(
    int MaxRetries,
    RetryStrategyType StrategyType,
    IRetryStrategyArtifact Strategy,
    IReadOnlyList<string> RetryableErrorCodes,
    bool StopOnNonRetryableError);
