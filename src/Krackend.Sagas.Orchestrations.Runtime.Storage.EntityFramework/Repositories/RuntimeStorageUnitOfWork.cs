using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
{
    private readonly RuntimeDbContext _dbContext;
    private int _deferCount;

    public RuntimeStorageUnitOfWork(RuntimeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public bool AutoSaveChanges => _deferCount == 0;

    public IDisposable DeferAutoSave()
    {
        _deferCount++;
        return new DeferScope(this);
    }

    public Task SaveChanges(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    private sealed class DeferScope : IDisposable
    {
        private readonly RuntimeStorageUnitOfWork _unitOfWork;
        private bool _disposed;

        public DeferScope(RuntimeStorageUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _unitOfWork._deferCount--;
            _disposed = true;
        }
    }
}
