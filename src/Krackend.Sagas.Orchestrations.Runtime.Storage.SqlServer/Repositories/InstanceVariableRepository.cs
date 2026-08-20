using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class InstanceVariableRepository : RuntimeRepositoryBase, IInstanceVariableRepository
{
    public InstanceVariableRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Upsert(InstanceVariable variable, CancellationToken cancellationToken = default)
    {
        var current = await DbContext.InstanceVariables.FirstOrDefaultAsync(x => x.Id == variable.Id, cancellationToken);
        if (current is null)
        {
            DbContext.InstanceVariables.Add(variable);
        }
        else
        {
            DbContext.Entry(current).CurrentValues.SetValues(variable);
        }

        await SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyCollection<InstanceVariable>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.InstanceVariables.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.Key)
            .ToArrayAsync(cancellationToken);
}
