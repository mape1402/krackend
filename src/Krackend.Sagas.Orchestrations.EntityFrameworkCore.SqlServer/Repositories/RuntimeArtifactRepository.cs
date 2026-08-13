using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Repositories;

public sealed class RuntimeArtifactRepository : IRuntimeArtifactRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public RuntimeArtifactRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
    {
        var artifactVersion = artifact.Version.ToString();
        var artifactType = artifact.ArtifactType;
        var existing = await _dbContext.Artifacts.FirstOrDefaultAsync(
            x => x.Id == artifact.Id
                || (x.EnvironmentKey == artifact.EnvironmentKey
                    && x.OrchestrationDefinitionKey == artifact.OrchestrationDefinitionKey
                    && x.Version == artifactVersion
                    && x.ArtifactType == artifactType),
            cancellationToken);
        if (existing is null)
        {
            _dbContext.Artifacts.Add(RuntimeStorageMapper.ToEntity(artifact));
        }
        else
        {
            artifact.Id = existing.Id;
            _dbContext.Entry(existing).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(artifact));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateActiveArtifacts(
        string environmentKey,
        string orchestrationDefinitionKey,
        Id exceptArtifactId,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _dbContext.Artifacts
            .Where(x => x.EnvironmentKey == environmentKey
                && x.OrchestrationDefinitionKey == orchestrationDefinitionKey
                && x.Id != exceptArtifactId
                && x.IsActive)
            .ToArrayAsync(cancellationToken);

        foreach (var artifact in artifacts)
        {
            artifact.IsActive = false;
            artifact.RetiredOnUtc ??= DateTime.UtcNow;
            artifact.SupersededByArtifactId = exceptArtifactId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Artifacts.AsNoTracking().FirstAsync(x => x.Id == artifactId, cancellationToken);
        return RuntimeStorageMapper.ToDomain(entity);
    }

    public async Task<RuntimeOrchestrationArtifact> GetByVersion(
        string environmentKey,
        string orchestrationDefinitionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default)
    {
        var artifactVersion = version.ToString();
        var entity = await _dbContext.Artifacts
            .AsNoTracking()
            .OrderByDescending(x => x.ActivatedOnUtc ?? x.DeployedOnUtc)
            .FirstOrDefaultAsync(x => x.EnvironmentKey == environmentKey
                && x.OrchestrationDefinitionKey == orchestrationDefinitionKey
                && x.Version == artifactVersion
                && x.ArtifactType == "orchestration.deploy",
                cancellationToken);

        return entity is null ? null : RuntimeStorageMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(
        string environmentKey,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.Artifacts
            .AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .OrderByDescending(x => x.DeployedOnUtc)
            .ToArrayAsync(cancellationToken);

        return rows.Select(RuntimeStorageMapper.ToDomain).ToArray();
    }

    public async Task<RuntimeOrchestrationArtifact> GetActive(
        string environmentKey,
        string orchestrationDefinitionKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Artifacts
            .AsNoTracking()
            .OrderByDescending(x => x.ActivatedOnUtc ?? x.DeployedOnUtc)
            .FirstOrDefaultAsync(x => x.EnvironmentKey == environmentKey
                && x.OrchestrationDefinitionKey == orchestrationDefinitionKey
                && x.IsActive, cancellationToken);

        return entity is null ? null : RuntimeStorageMapper.ToDomain(entity);
    }
}
