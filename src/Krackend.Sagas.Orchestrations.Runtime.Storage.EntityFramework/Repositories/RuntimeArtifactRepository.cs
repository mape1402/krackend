using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeArtifactRepository : RuntimeRepositoryBase, IRuntimeArtifactRepository, IRuntimeArtifactUiCatalog, IRuntimeArtifactLifetimePolicyRepository, IRuntimeArtifactRolloutTransitionRepository, IArchivedRecoveryActivationRepository, IRuntimeArtifactLifecycleFenceRepository, IArchivedRecoveryRetirementRepository, IArchivedRecoveryBatchRepository
{
    private readonly IRuntimeIngressConfigurationRepository _ingressConfigurationRepository;

    public RuntimeArtifactRepository(
        RuntimeDbContext dbContext,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeIngressConfigurationRepository ingressConfigurationRepository)
        : base(dbContext, unitOfWork)
    {
        _ingressConfigurationRepository = ingressConfigurationRepository ?? throw new ArgumentNullException(nameof(ingressConfigurationRepository));
    }

    public async Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
    {
        var current = await DbContext.RuntimeOrchestrationArtifacts.FirstOrDefaultAsync(x => x.Id == artifact.Id, cancellationToken);
        if (current is null)
        {
            DbContext.RuntimeOrchestrationArtifacts.Add(artifact);
        }
        else
        {
            DbContext.Entry(current).CurrentValues.SetValues(artifact);
        }

        await SaveChanges(cancellationToken);
    }

    public async Task MarkProjectionStarted(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration)
        {
            return;
        }

        artifact.Status = RuntimeOrchestrationArtifactStatus.Pending;
        artifact.ProjectionStartedOnUtc = DateTime.UtcNow;
        artifact.ProjectionCompletedOnUtc = null;
        artifact.ProjectionFailedOnUtc = null;
        artifact.ProjectionError = null;
        await SaveChanges(cancellationToken);
    }

    public async Task MarkReady(
        Id artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration)
        {
            return;
        }

        var now = DateTime.UtcNow;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Ready;
        artifact.ActivatedOnUtc = now;
        artifact.ProjectionCompletedOnUtc = now;
        artifact.ProjectionFailedOnUtc = null;
        artifact.ProjectionError = null;
        await SaveChanges(cancellationToken);
    }

    public async Task MarkProjectionFailed(
        Id artifactId,
        long ingressGeneration,
        string error,
        CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration)
        {
            return;
        }

        artifact.Status = RuntimeOrchestrationArtifactStatus.Failed;
        artifact.ProjectionFailedOnUtc = DateTime.UtcNow;
        artifact.ProjectionError = error;
        await SaveChanges(cancellationToken);
    }

    public async Task<bool> TryMarkRolloutProjectionStarted(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration || !artifact.IsActive ||
            artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active ||
            artifact.Status is not (RuntimeOrchestrationArtifactStatus.Pending or RuntimeOrchestrationArtifactStatus.Failed))
            return false;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Pending;
        artifact.ProjectionStartedOnUtc = DateTime.UtcNow;
        artifact.ProjectionCompletedOnUtc = null;
        artifact.ProjectionFailedOnUtc = null;
        artifact.ProjectionError = null;
        await SaveChanges(cancellationToken);
        return true;
    }

    public async Task<bool> TryMarkRolloutReady(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration || !artifact.IsActive ||
            artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active ||
            artifact.Status is not (RuntimeOrchestrationArtifactStatus.Pending or RuntimeOrchestrationArtifactStatus.Failed))
            return false;
        var now = DateTime.UtcNow;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Ready;
        artifact.ActivatedOnUtc = now;
        artifact.ProjectionCompletedOnUtc = now;
        artifact.ProjectionFailedOnUtc = null;
        artifact.ProjectionError = null;
        await SaveChanges(cancellationToken);
        return true;
    }

    public async Task<bool> TryFenceRollbackTarget(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration || !artifact.IsActive ||
            artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Active ||
            artifact.Status != RuntimeOrchestrationArtifactStatus.Ready)
            return false;
        // A real write is required: MongoDB does not lock a document for a no-op update.
        artifact.IngressGeneration = checked(ingressGeneration + 1);
        await SaveChanges(cancellationToken);
        return true;
    }

    public async Task<bool> TryFenceLifecycle(Id artifactId, long expectedGeneration, CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != expectedGeneration) return false;
        artifact.IngressGeneration = checked(expectedGeneration + 1);
        await SaveChanges(cancellationToken);
        return true;
    }

    public async Task<long?> TryActivateArchivedBackchannelsAsync(Id artifactId, long expectedGeneration, CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IsActive || artifact.Status != RuntimeOrchestrationArtifactStatus.Retired ||
            artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Archived || artifact.IngressGeneration != expectedGeneration)
            return null;
        var configurations = await DbContext.RuntimeIngressConfigurations.Where(x => x.RuntimeOrchestrationArtifactId == artifactId)
            .ToArrayAsync(cancellationToken);
        if (configurations.Any(x => x.IngressKind == IngressKind.Trigger && x.IsActive)) return null;
        var backchannels = configurations.Where(x => x.IngressKind == IngressKind.Backchannel &&
            x.MessageDisposition == IngressMessageDisposition.Process).ToArray();
        if (backchannels.Length == 0) return null;
        var now = DateTime.UtcNow;
        foreach (var backchannel in backchannels)
        {
            backchannel.IsActive = true;
            backchannel.DeactivatedOnUtc = null;
            backchannel.UpdatedOnUtc = now;
        }
        artifact.IsActive = true;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Ready;
        artifact.IngressGeneration = checked(expectedGeneration + 1);
        await SaveChanges(cancellationToken);
        return artifact.IngressGeneration;
    }

    public async Task<IReadOnlyCollection<ArchivedRecoveryCandidate>> GetArchivedRecoveryCandidates(int skip, int take, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking()
            .Where(x => x.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Archived && x.IsActive &&
                x.Status == RuntimeOrchestrationArtifactStatus.Ready)
            .OrderBy(x => x.Id).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 1000))
            .Select(x => new ArchivedRecoveryCandidate(x.Id, x.IngressGeneration)).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Id>> GetRetiredArchivedIds(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default)
    {
        if (artifactIds is null || artifactIds.Count == 0) return [];
        return await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking()
            .Where(x => artifactIds.Contains(x.Id) && x.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Archived &&
                !x.IsActive && x.Status == RuntimeOrchestrationArtifactStatus.Retired)
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ArchivedRecoveryCandidate>> FenceArchivedRecoveries(IReadOnlyCollection<ArchivedRecoveryCandidate> candidates, CancellationToken cancellationToken = default)
    {
        if (candidates is null || candidates.Count == 0) return [];
        var expected = candidates.ToDictionary(x => x.ArtifactId, x => x.IngressGeneration);
        var ids = expected.Keys.ToArray();
        var artifacts = await DbContext.RuntimeOrchestrationArtifacts.Where(x => ids.Contains(x.Id)).ToArrayAsync(cancellationToken);
        var fenced = new List<ArchivedRecoveryCandidate>(artifacts.Length);
        foreach (var artifact in artifacts)
        {
            if (!expected.TryGetValue(artifact.Id, out var generation) || artifact.IngressGeneration != generation ||
                !artifact.IsActive || artifact.Status != RuntimeOrchestrationArtifactStatus.Ready ||
                artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Archived) continue;
            artifact.IngressGeneration = checked(generation + 1);
            fenced.Add(new ArchivedRecoveryCandidate(artifact.Id, artifact.IngressGeneration));
        }
        await SaveChanges(cancellationToken);
        return fenced;
    }

    public async Task<IReadOnlyCollection<Id>> RetireArchivedRecoveries(IReadOnlyCollection<ArchivedRecoveryCandidate> candidates, CancellationToken cancellationToken = default)
    {
        if (candidates is null || candidates.Count == 0) return [];
        var expected = candidates.ToDictionary(x => x.ArtifactId, x => x.IngressGeneration);
        var ids = expected.Keys.ToArray();
        var artifacts = await DbContext.RuntimeOrchestrationArtifacts.Where(x => ids.Contains(x.Id)).ToArrayAsync(cancellationToken);
        var ingress = await DbContext.RuntimeIngressConfigurations.Where(x => ids.Contains(x.RuntimeOrchestrationArtifactId) && x.IsActive)
            .ToArrayAsync(cancellationToken);
        var byArtifact = ingress.ToLookup(x => x.RuntimeOrchestrationArtifactId);
        var now = DateTime.UtcNow;
        var retired = new List<Id>(artifacts.Length);
        foreach (var artifact in artifacts)
        {
            if (!expected.TryGetValue(artifact.Id, out var generation) || artifact.IngressGeneration != generation ||
                !artifact.IsActive || artifact.Status != RuntimeOrchestrationArtifactStatus.Ready ||
                artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Archived) continue;
            var configurations = byArtifact[artifact.Id].ToArray();
            if (configurations.Any(x => x.IngressKind != IngressKind.Backchannel)) continue;
            foreach (var configuration in configurations)
            {
                configuration.IsActive = false;
                configuration.DeactivatedOnUtc = now;
                configuration.UpdatedOnUtc = now;
            }
            artifact.IsActive = false;
            artifact.Status = RuntimeOrchestrationArtifactStatus.Retired;
            artifact.IngressGeneration = checked(generation + 1);
            retired.Add(artifact.Id);
        }
        await SaveChanges(cancellationToken);
        return retired;
    }

    public async Task<bool> TryMarkRolloutStandupFailed(Id artifactId, long ingressGeneration, string error, CancellationToken cancellationToken = default)
    {
        var artifact = await GetTrackedArtifact(artifactId, cancellationToken);
        if (artifact.IngressGeneration != ingressGeneration || !artifact.IsActive ||
            artifact.Status != RuntimeOrchestrationArtifactStatus.Pending)
            return false;
        artifact.Status = RuntimeOrchestrationArtifactStatus.Failed;
        artifact.ProjectionFailedOnUtc = DateTime.UtcNow;
        artifact.ProjectionError = error;
        await SaveChanges(cancellationToken);
        return true;
    }

    public async Task DeactivateActiveArtifacts(string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default)
    {
        var artifacts = await DbContext.RuntimeOrchestrationArtifacts
            .Where(x => x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.Id != exceptArtifactId &&
                x.IsActive)
            .ToArrayAsync(cancellationToken);
        var deactivatedArtifactIds = new List<Id>();

        foreach (var artifact in artifacts)
        {
            artifact.IsActive = false;
            artifact.Status = RuntimeOrchestrationArtifactStatus.Retired;
            artifact.RetiredOnUtc = DateTime.UtcNow;
            deactivatedArtifactIds.Add(artifact.Id);
        }

        await SaveChanges(cancellationToken);
        await _ingressConfigurationRepository.DeactivateForArtifactsAsync(deactivatedArtifactIds, cancellationToken);
    }

    public async Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == artifactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Runtime artifact '{artifactId}' was not found.");

    public async Task<RuntimeOrchestrationArtifact> GetByVersion(string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.Version.Equals(version), cancellationToken)
            ?? throw new KeyNotFoundException($"Runtime artifact '{orchestrationDefinitionKey}' version '{version}' was not found.");

    public async Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking()
            .OrderByDescending(x => x.DeployedOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RuntimeArtifactLifetimePolicyItem>> GetLifetimePolicyPage(
        int skip, int take, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Id).Skip(Math.Max(0, skip)).Take(Math.Max(1, take))
            .Select(x => new RuntimeArtifactLifetimePolicyItem(x.Id, x.ArtifactPayload)).ToArrayAsync(cancellationToken);

    public async Task<RuntimeArtifactGroupPage> GetGroups(int page, int pageSize, string search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = DbContext.RuntimeOrchestrationArtifacts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.OrchestrationDefinitionKey.Contains(search.Trim()));
        try
        {
            var groups = query.GroupBy(x => x.OrchestrationDefinitionKey)
                .Select(g => new { Key = g.Key, VersionCount = g.Count(), ActiveCount = g.Count(x => x.IsActive), Latest = g.Max(x => x.DeployedOnUtc) });
            var total = await groups.LongCountAsync(cancellationToken);
            var rows = await groups.OrderBy(x => x.Key).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
            return new RuntimeArtifactGroupPage(rows.Select(x => new RuntimeArtifactGroup(x.Key, x.VersionCount, x.ActiveCount, x.Latest)).ToArray(), total, page, pageSize);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase))
        {
            var keys = query.Select(x => x.OrchestrationDefinitionKey).Distinct();
            var total = await keys.LongCountAsync(cancellationToken);
            var pageKeys = await keys.OrderBy(x => x).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
            var rows = new List<RuntimeArtifactGroup>(pageKeys.Length);
            foreach (var key in pageKeys)
            {
                var versions = query.Where(x => x.OrchestrationDefinitionKey == key);
                var versionCount = await versions.CountAsync(cancellationToken);
                var activeCount = await versions.CountAsync(x => x.IsActive, cancellationToken);
                var latest = await versions.OrderByDescending(x => x.DeployedOnUtc).Select(x => x.DeployedOnUtc).FirstAsync(cancellationToken);
                rows.Add(new RuntimeArtifactGroup(key, versionCount, activeCount, latest));
            }
            return new RuntimeArtifactGroupPage(rows, total, page, pageSize);
        }
    }

    public async Task<RuntimeArtifactVersionPage> GetVersions(string orchestrationKey, int page, int pageSize, string search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().Where(x => x.OrchestrationDefinitionKey == orchestrationKey);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (Ulid.TryParse(term, out var parsedId))
            {
                var id = new Id(parsedId);
                query = query.Where(x => x.Id == id || x.LastReleaseId.Contains(term) || x.LastControlPlaneArtifactId.Contains(term));
            }
            else
            {
                var parts = term.Split('.');
                if (parts.Length == 3 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor) && int.TryParse(parts[2], out var patch))
                {
                    var version = new SemanticVersion(major, minor, patch);
                    query = query.Where(x => x.Version.Equals(version) || x.LastReleaseId.Contains(term) || x.LastControlPlaneArtifactId.Contains(term));
                }
                else query = query.Where(x => x.LastReleaseId.Contains(term) || x.LastControlPlaneArtifactId.Contains(term));
            }
        }
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.DeployedOnUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.OrchestrationDefinitionKey, x.Version, x.Status, x.LifecycleStatus,
                x.LastReleaseId, x.LastControlPlaneArtifactId, x.ReleaseStrategy, x.IsActive, x.IngressGeneration,
                x.ArtifactChecksum, x.DeployedOnUtc, x.ActivatedOnUtc, x.ProjectionStartedOnUtc,
                x.ProjectionCompletedOnUtc, x.ProjectionFailedOnUtc, x.ProjectionError }).ToArrayAsync(cancellationToken);
        var artifactIds = rows.Select(x => x.Id).ToArray();
        var triggers = await DbContext.RuntimeIngressConfigurations.AsNoTracking()
            .Where(x => artifactIds.Contains(x.RuntimeOrchestrationArtifactId) && x.IngressKind == IngressKind.Trigger)
            .Select(x => new { x.RuntimeOrchestrationArtifactId, x.IsActive, x.MessageDisposition })
            .ToArrayAsync(cancellationToken);
        var counts = triggers.GroupBy(x => x.RuntimeOrchestrationArtifactId).ToDictionary(x => x.Key,
            x => (Total: x.Count(), Active: x.Count(t => t.IsActive && t.MessageDisposition == IngressMessageDisposition.Process)));
        return new RuntimeArtifactVersionPage(rows.Select(x => new RuntimeArtifactCatalogItem(
            x.Id, x.OrchestrationDefinitionKey, x.Version.ToString(), x.Status.ToString(), x.LifecycleStatus.ToString(),
            x.LastReleaseId, x.LastControlPlaneArtifactId, x.ReleaseStrategy, x.IsActive, x.IngressGeneration,
            x.ArtifactChecksum.Value, x.DeployedOnUtc, x.ActivatedOnUtc, x.ProjectionStartedOnUtc,
            x.ProjectionCompletedOnUtc, x.ProjectionFailedOnUtc, x.ProjectionError)
            { ActiveTriggerCount = counts.GetValueOrDefault(x.Id).Active, TotalTriggerCount = counts.GetValueOrDefault(x.Id).Total }).ToArray(), total, page, pageSize);
    }

    public async Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking()
            .Where(x => x.IsActive &&
                x.Status == RuntimeOrchestrationArtifactStatus.Ready)
            .OrderByDescending(x => x.DeployedOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<RuntimeOrchestrationArtifact> GetActive(string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.IsActive &&
                x.LifecycleStatus == RuntimeOrchestrationLifecycleStatus.Active &&
                x.Status == RuntimeOrchestrationArtifactStatus.Ready, cancellationToken)
            ?? throw new KeyNotFoundException($"Active runtime artifact '{orchestrationDefinitionKey}' was not found.");

    private async Task<RuntimeOrchestrationArtifact> GetTrackedArtifact(Id artifactId, CancellationToken cancellationToken)
        => await DbContext.RuntimeOrchestrationArtifacts.FirstOrDefaultAsync(x => x.Id == artifactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Runtime artifact '{artifactId}' was not found.");
}
