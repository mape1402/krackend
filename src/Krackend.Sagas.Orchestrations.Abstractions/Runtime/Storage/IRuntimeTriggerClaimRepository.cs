namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Coordinates durable ownership of incoming orchestration triggers.
/// </summary>
public interface IRuntimeTriggerClaimRepository
{
    /// <summary>
    /// Atomically claims every trigger identity for an artifact.
    /// </summary>
    /// <param name="identities">Normalized identities to claim.</param>
    /// <param name="artifactId">New or existing owning artifact.</param>
    /// <param name="replaceableArtifactIds">Owners that a Rollout may replace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acquisition result and, when relevant, the conflicting owner.</returns>
    Task<RuntimeTriggerClaimResult> ClaimAsync(
        IReadOnlyCollection<string> identities,
        Id artifactId,
        IReadOnlyCollection<Id> replaceableArtifactIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases every trigger owned by the supplied artifacts.
    /// </summary>
    /// <param name="artifactIds">Artifact identifiers whose claims are released.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReleaseAsync(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the result of an attempt to claim runtime trigger identities.
/// </summary>
public sealed record RuntimeTriggerClaimResult(bool Succeeded, Id? ConflictingArtifactId = null)
{
    /// <summary>
    /// Creates a successful acquisition result.
    /// </summary>
    public static RuntimeTriggerClaimResult Acquired { get; } = new(true);

    /// <summary>
    /// Creates a collision result.
    /// </summary>
    public static RuntimeTriggerClaimResult Conflict(Id artifactId) => new(false, artifactId);
}
