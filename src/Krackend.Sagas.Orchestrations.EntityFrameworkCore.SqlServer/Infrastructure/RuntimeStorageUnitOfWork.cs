using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

/// <summary>
/// Entity Framework runtime storage unit of work.
/// </summary>
public sealed class RuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
{
    private readonly RuntimeStorageDbContext _dbContext;

    public RuntimeStorageUnitOfWork(RuntimeStorageDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public bool AutoSaveChanges => _dbContext.AutoSaveChanges;

    public IDisposable DeferAutoSave() => _dbContext.DeferAutoSave();

    public Task SaveChanges(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
