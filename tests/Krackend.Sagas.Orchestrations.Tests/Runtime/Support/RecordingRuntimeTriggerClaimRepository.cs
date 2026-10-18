namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class RecordingRuntimeTriggerClaimRepository : IRuntimeTriggerClaimRepository
{
    private readonly Dictionary<string, Id> claims = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ClaimRequest> ClaimRequests => claimRequests.ToArray();

    public IReadOnlyCollection<IReadOnlyCollection<Id>> ReleaseRequests => releaseRequests.ToArray();

    private readonly List<ClaimRequest> claimRequests = [];
    private readonly List<IReadOnlyCollection<Id>> releaseRequests = [];

    public RuntimeTriggerClaimResult NextResult { get; set; } = RuntimeTriggerClaimResult.Acquired;

    public Task<RuntimeTriggerClaimResult> ClaimAsync(
        IReadOnlyCollection<string> identities,
        Id artifactId,
        IReadOnlyCollection<Id> replaceableArtifactIds,
        CancellationToken cancellationToken = default)
    {
        var normalized = (identities ?? Array.Empty<string>())
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Select(identity => identity.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var replaceable = (replaceableArtifactIds ?? Array.Empty<Id>()).ToArray();
        claimRequests.Add(new ClaimRequest(normalized, artifactId, replaceable));

        if (!NextResult.Succeeded)
        {
            return Task.FromResult(NextResult);
        }

        foreach (var identity in normalized)
        {
            claims[identity] = artifactId;
        }

        return Task.FromResult(RuntimeTriggerClaimResult.Acquired);
    }

    public Task ReleaseAsync(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default)
    {
        var ids = (artifactIds ?? Array.Empty<Id>()).ToArray();
        releaseRequests.Add(ids);
        foreach (var identity in claims.Where(claim => ids.Contains(claim.Value)).Select(claim => claim.Key).ToArray())
        {
            claims.Remove(identity);
        }

        return Task.CompletedTask;
    }

    internal sealed record ClaimRequest(
        IReadOnlyCollection<string> Identities,
        Id ArtifactId,
        IReadOnlyCollection<Id> ReplaceableArtifactIds);
}
