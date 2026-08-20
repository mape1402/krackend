using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class RuntimeArtifactRepository : RuntimeRepositoryBase, IRuntimeArtifactRepository
{
    public RuntimeArtifactRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
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

    public async Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default)
    {
        var artifacts = await DbContext.RuntimeOrchestrationArtifacts
            .Where(x => x.EnvironmentKey == environmentKey &&
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.Id != exceptArtifactId &&
                x.IsActive)
            .ToArrayAsync(cancellationToken);

        foreach (var artifact in artifacts)
        {
            artifact.IsActive = false;
            artifact.RetiredOnUtc = DateTime.UtcNow;
        }

        await SaveChanges(cancellationToken);
    }

    public async Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == artifactId, cancellationToken)
            ?? throw new KeyNotFoundException($"Runtime artifact '{artifactId}' was not found.");

    public async Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.EnvironmentKey == environmentKey &&
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.Version.Equals(version), cancellationToken)
            ?? throw new KeyNotFoundException($"Runtime artifact '{orchestrationDefinitionKey}' version '{version}' was not found.");

    public async Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .OrderByDescending(x => x.DeployedOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
        => await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.EnvironmentKey == environmentKey &&
                x.OrchestrationDefinitionKey == orchestrationDefinitionKey &&
                x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException($"Active runtime artifact '{orchestrationDefinitionKey}' was not found.");
}
