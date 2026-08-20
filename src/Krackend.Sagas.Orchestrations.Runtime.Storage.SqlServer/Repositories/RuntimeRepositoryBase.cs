using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;

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
}
