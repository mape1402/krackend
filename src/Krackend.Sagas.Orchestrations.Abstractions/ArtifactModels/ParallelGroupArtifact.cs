namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable parallel-group contract.
/// </summary>
public sealed record ParallelGroupArtifact(
    Id Id,
    ParallelJoinPolicy JoinPolicy,
    int? MaxParallelAgents);
