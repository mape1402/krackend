using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal abstract class RuntimeRepositoryBase
{
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;

    protected RuntimeRepositoryBase(RuntimeStorageDbContext dbContext, IRuntimeStorageUnitOfWork unitOfWork)
    {
        DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    protected RuntimeStorageDbContext DbContext { get; }

    protected Task SaveChanges(CancellationToken cancellationToken)
        => _unitOfWork.AutoSaveChanges ? DbContext.SaveChangesAsync(cancellationToken) : Task.CompletedTask;

    protected void DetachLocalTrackedEntity<TEntity>(DbSet<TEntity> dbSet, TEntity entity)
        where TEntity : class
    {
        var key = DbContext.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey();
        if (key is null)
        {
            return;
        }

        foreach (var local in dbSet.Local)
        {
            if (ReferenceEquals(local, entity))
            {
                continue;
            }

            var matches = key.Properties.All(property =>
                Equals(
                    property.PropertyInfo?.GetValue(local),
                    property.PropertyInfo?.GetValue(entity)));

            if (matches)
            {
                DbContext.Entry(local).State = EntityState.Detached;
                return;
            }
        }
    }
}
