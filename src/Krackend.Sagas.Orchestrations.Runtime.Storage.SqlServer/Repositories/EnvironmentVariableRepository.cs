using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class EnvironmentVariableRepository : RuntimeRepositoryBase, IEnvironmentVariableRepository
{
    public EnvironmentVariableRepository(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
        : base(dbContext, unitOfWork)
    {
    }

    public async Task Upsert(EnvironmentVariableValue variable, CancellationToken cancellationToken = default)
    {
        var current = await DbContext.EnvironmentVariableValues.FirstOrDefaultAsync(x => x.Id == variable.Id, cancellationToken);
        if (current is null)
        {
            DbContext.EnvironmentVariableValues.Add(variable);
        }
        else
        {
            DbContext.Entry(current).CurrentValues.SetValues(variable);
        }

        await SaveChanges(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EnvironmentVariableValue>> GetByEnvironmentKey(string environmentKey, CancellationToken cancellationToken = default)
        => await DbContext.EnvironmentVariableValues.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .OrderBy(x => x.VariableKey)
            .ToArrayAsync(cancellationToken);
}
