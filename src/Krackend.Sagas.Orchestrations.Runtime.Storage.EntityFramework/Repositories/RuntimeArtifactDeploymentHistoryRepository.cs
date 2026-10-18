using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeArtifactDeploymentHistoryRepository : RuntimeRepositoryBase, IRuntimeArtifactDeploymentHistoryRepository
{
    public RuntimeArtifactDeploymentHistoryRepository(RuntimeDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Append(RuntimeArtifactDeploymentRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        DbContext.RuntimeArtifactDeploymentHistory.Add(record);
        await SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>> GetAll(CancellationToken cancellationToken = default)
        => await DbContext.RuntimeArtifactDeploymentHistory.AsNoTracking()
            .OrderByDescending(x => x.AppliedOnUtc)
            .ThenByDescending(x => x.Id)
            .ToArrayAsync(cancellationToken);
}
