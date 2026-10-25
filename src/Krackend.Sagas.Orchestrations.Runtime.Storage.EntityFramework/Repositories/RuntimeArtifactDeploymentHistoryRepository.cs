using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeArtifactDeploymentHistoryRepository : RuntimeRepositoryBase, IRuntimeArtifactDeploymentHistoryRepository, IRuntimeArtifactHistoryCatalog
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

    public async Task<RuntimeArtifactHistoryPage> GetPage(Id artifactId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = DbContext.RuntimeArtifactDeploymentHistory.AsNoTracking().Where(x => x.RuntimeOrchestrationArtifactId == artifactId);
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.AppliedOnUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new RuntimeArtifactHistoryPage(rows, total, page, pageSize);
    }
}
