using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
{
    private readonly RuntimeDbContext _dbContext;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;
    private readonly ILogger<RuntimeStorageUnitOfWork> _logger;
    private readonly List<RuntimeReactiveEvent> _pendingEvents = [];
    private int _deferCount;

    public RuntimeStorageUnitOfWork(RuntimeDbContext dbContext, IRuntimeReactiveEventPublisher reactiveEventPublisher,
        ILogger<RuntimeStorageUnitOfWork> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool AutoSaveChanges => _deferCount == 0;

    public async Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EntityFrameworkRuntimeStorageTransaction(transaction, this);
    }

    public IDisposable DeferAutoSave()
    {
        _deferCount++;
        return new DeferScope(this);
    }

    public async Task SaveChanges(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_dbContext.Database.CurrentTransaction is null)
            await PublishPendingEvents();
    }

    internal async Task PublishWhenCommitted(RuntimeReactiveEvent eventData)
    {
        if (!AutoSaveChanges || _dbContext.Database.CurrentTransaction is not null)
        {
            _pendingEvents.Add(eventData);
            return;
        }

        await PublishBestEffort(eventData);
    }

    private async Task PublishPendingEvents()
    {
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        foreach (var eventData in events)
            await PublishBestEffort(eventData);
    }

    private async Task PublishBestEffort(RuntimeReactiveEvent eventData)
    {
        try
        {
            await _reactiveEventPublisher.Publish(eventData, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to publish committed runtime event {EventId}.", eventData.Id);
        }
    }

    private sealed class EntityFrameworkRuntimeStorageTransaction : IRuntimeStorageTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly RuntimeStorageUnitOfWork _unitOfWork;
        private bool _committed;

        public EntityFrameworkRuntimeStorageTransaction(IDbContextTransaction transaction, RuntimeStorageUnitOfWork unitOfWork)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _unitOfWork = unitOfWork;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
            await _unitOfWork.PublishPendingEvents();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
                _unitOfWork._pendingEvents.Clear();
            await _transaction.DisposeAsync();
        }
    }

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
