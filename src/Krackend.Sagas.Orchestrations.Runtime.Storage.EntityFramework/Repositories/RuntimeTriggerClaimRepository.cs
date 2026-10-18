using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

/// <summary>
/// Uses a database uniqueness constraint as the cross-host trigger ownership lock.
/// </summary>
internal sealed class RuntimeTriggerClaimRepository : IRuntimeTriggerClaimRepository
{
    private readonly RuntimeDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public RuntimeTriggerClaimRepository(RuntimeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<RuntimeTriggerClaimResult> ClaimAsync(
        IReadOnlyCollection<string> identities,
        Id artifactId,
        IReadOnlyCollection<Id> replaceableArtifactIds,
        CancellationToken cancellationToken = default)
    {
        var keys = Normalize(identities);
        if (keys.Length == 0)
        {
            return RuntimeTriggerClaimResult.Acquired;
        }

        var replaceable = (replaceableArtifactIds ?? Array.Empty<Id>()).ToHashSet();
        var existingClaims = await _dbContext.RuntimeTriggerClaims
            .Where(claim => keys.Contains(claim.Identity))
            .ToArrayAsync(cancellationToken);

        var conflicting = existingClaims.FirstOrDefault(claim =>
            claim.RuntimeOrchestrationArtifactId != artifactId &&
            !replaceable.Contains(claim.RuntimeOrchestrationArtifactId));
        if (conflicting is not null)
        {
            return RuntimeTriggerClaimResult.Conflict(conflicting.RuntimeOrchestrationArtifactId);
        }

        var known = existingClaims.ToDictionary(claim => claim.Identity, StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        foreach (var key in keys)
        {
            if (known.TryGetValue(key, out var current))
            {
                current.RuntimeOrchestrationArtifactId = artifactId;
                current.UpdatedOnUtc = now;
                continue;
            }

            _dbContext.RuntimeTriggerClaims.Add(new RuntimeTriggerClaim
            {
                Identity = key,
                RuntimeOrchestrationArtifactId = artifactId,
                UpdatedOnUtc = now
            });
        }

        try
        {
            // Save now rather than at the end of projection so the unique index is a real
            // synchronization point for another runtime process attempting the same deploy.
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RuntimeTriggerClaimResult.Acquired;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries<RuntimeTriggerClaim>()
                         .Where(entry => entry.State == EntityState.Added)
                         .ToArray())
            {
                entry.State = EntityState.Detached;
            }

            var refreshedClaims = await _dbContext.RuntimeTriggerClaims
                .AsNoTracking()
                .Where(claim => keys.Contains(claim.Identity))
                .ToArrayAsync(cancellationToken);
            var collision = refreshedClaims.FirstOrDefault(claim => claim.RuntimeOrchestrationArtifactId != artifactId);
            return collision is null
                ? RuntimeTriggerClaimResult.Conflict(default)
                : RuntimeTriggerClaimResult.Conflict(collision.RuntimeOrchestrationArtifactId);
        }
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default)
    {
        var ids = (artifactIds ?? Array.Empty<Id>()).ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var claims = await _dbContext.RuntimeTriggerClaims
            .Where(claim => ids.Contains(claim.RuntimeOrchestrationArtifactId))
            .ToArrayAsync(cancellationToken);
        _dbContext.RuntimeTriggerClaims.RemoveRange(claims);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string[] Normalize(IReadOnlyCollection<string> identities)
        => (identities ?? Array.Empty<string>())
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Select(identity => identity.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
