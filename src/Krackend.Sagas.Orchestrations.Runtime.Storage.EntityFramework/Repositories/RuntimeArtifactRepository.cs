using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeArtifactRepository : RuntimeRepositoryBase, IRuntimeArtifactRepository, IRuntimeArtifactUiCatalog
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

    public async Task<RuntimeArtifactGroupPage> GetGroups(int page, int pageSize, string search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = DbContext.RuntimeOrchestrationArtifacts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.OrchestrationDefinitionKey.Contains(search.Trim()));
        var groups = query.GroupBy(x => x.OrchestrationDefinitionKey)
            .Select(g => new { Key = g.Key, VersionCount = g.Count(), ActiveCount = g.Count(x => x.IsActive), Latest = g.Max(x => x.DeployedOnUtc) });
        var total = await groups.LongCountAsync(cancellationToken);
        var rows = await groups.OrderBy(x => x.Key).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new RuntimeArtifactGroupPage(rows.Select(x => new RuntimeArtifactGroup(x.Key, x.VersionCount, x.ActiveCount, x.Latest)).ToArray(), total, page, pageSize);
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
        return new RuntimeArtifactVersionPage(rows.Select(x => new RuntimeArtifactCatalogItem(
            x.Id, x.OrchestrationDefinitionKey, x.Version.ToString(), x.Status.ToString(), x.LifecycleStatus.ToString(),
            x.LastReleaseId, x.LastControlPlaneArtifactId, x.ReleaseStrategy, x.IsActive, x.IngressGeneration,
            x.ArtifactChecksum.Value, x.DeployedOnUtc, x.ActivatedOnUtc, x.ProjectionStartedOnUtc,
            x.ProjectionCompletedOnUtc, x.ProjectionFailedOnUtc, x.ProjectionError)).ToArray(), total, page, pageSize);
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
                x.Status == RuntimeOrchestrationArtifactStatus.Ready, cancellationToken)
            ?? throw new KeyNotFoundException($"Active runtime artifact '{orchestrationDefinitionKey}' was not found.");

    private async Task<RuntimeOrchestrationArtifact> GetTrackedArtifact(Id artifactId, CancellationToken cancellationToken)
        => await DbContext.RuntimeOrchestrationArtifacts.FirstOrDefaultAsync(x => x.Id == artifactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Runtime artifact '{artifactId}' was not found.");
}
