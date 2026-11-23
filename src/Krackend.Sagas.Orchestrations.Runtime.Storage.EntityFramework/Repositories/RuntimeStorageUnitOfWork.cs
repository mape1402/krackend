using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
{
    private readonly RuntimeDbContext _dbContext;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;
    private readonly ILogger<RuntimeStorageUnitOfWork> _logger;
    private readonly RuntimeEntityFrameworkStorageOptions _storageOptions;
    private readonly RuntimeSqlServerDurableLogFlushCoordinator _durableLogFlushCoordinator;
    private readonly List<RuntimeReactiveEvent> _pendingEvents = [];
    private int _deferCount;

    public RuntimeStorageUnitOfWork(RuntimeDbContext dbContext, IRuntimeReactiveEventPublisher reactiveEventPublisher,
        ILogger<RuntimeStorageUnitOfWork> logger,
        IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions,
        RuntimeSqlServerDurableLogFlushCoordinator durableLogFlushCoordinator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageOptions = storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions));
        _durableLogFlushCoordinator = durableLogFlushCoordinator
            ?? throw new ArgumentNullException(nameof(durableLogFlushCoordinator));
    }

    public bool AutoSaveChanges => _deferCount == 0;

    public bool PreferBatchedEngineActivations
        => !string.Equals(
            _dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal);

    public async Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return NestedRuntimeStorageTransaction.Instance;
        }

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
        // Detect once. Enumerating Entries() performs implicit detection and
        // SaveChanges performs it again; with JSON-backed runtime metadata that
        // doubled a substantial amount of CPU work on every transition.
        var autoDetectChanges = _dbContext.ChangeTracker.AutoDetectChangesEnabled;
        var detectionStarted = Stopwatch.GetTimestamp();
        if (autoDetectChanges)
            _dbContext.ChangeTracker.DetectChanges();
        RuntimeConcurrencyMetrics.StorageDetectChangesMilliseconds.Record(
            Stopwatch.GetElapsedTime(detectionStarted).TotalMilliseconds);
        _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        var pendingEntries = _dbContext.ChangeTracker.Entries()
            .Count(static entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        if (pendingEntries == 0)
        {
            // The engine activation already flushed its graph inside the coordinator's
            // transaction. Avoid entering EF's SaveChanges pipeline a second time just
            // to discover an empty tracker; the outer coordinator still owns commit.
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
            if (_dbContext.Database.CurrentTransaction is null)
                await PublishPendingEvents();
            return;
        }
        var started = Stopwatch.GetTimestamp();
        try
        {
            // Let EF finish the complete SaveChanges pipeline before issuing the
            // explicit durability command. Re-entering this DbContext from its own
            // SavedChanges interceptor keeps EF's operation active and eventually
            // creates a feedback loop under sustained concurrent load.
            using (_dbContext.DeferDurableLogFlush())
                await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            RuntimeConcurrencyMetrics.StorageSaveMilliseconds.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("entries", pendingEntries));
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
        if (_dbContext.Database.CurrentTransaction is null)
        {
            await FlushSqlServerLogIfConfigured();
            await PublishPendingEvents();
        }
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

    private sealed class EntityFrameworkRuntimeStorageTransaction : IRuntimePipelinedStorageTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly RuntimeStorageUnitOfWork _unitOfWork;
        private Task _durabilityBarrier;
        private bool _logicallyCommitted;
        private bool _durablyCommitted;

        public EntityFrameworkRuntimeStorageTransaction(
            IDbContextTransaction transaction,
            RuntimeStorageUnitOfWork unitOfWork)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _unitOfWork = unitOfWork;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await CommitLogicalAsync(cancellationToken);
                await AwaitDurabilityAsync();
            }
            finally
            {
                RuntimeConcurrencyMetrics.StorageCommitMilliseconds.Record(
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }

        public async Task CommitLogicalAsync(CancellationToken cancellationToken = default)
        {
            if (_logicallyCommitted)
                return;

            await _transaction.CommitAsync(cancellationToken);
            _logicallyCommitted = true;

            // A forced-delayed commit is visible and atomic but not yet a safe broker
            // acknowledgement boundary. Start the physical barrier immediately; the
            // coordinator may overlap already-committed dispatch I/O with this task.
            _durabilityBarrier = _unitOfWork.FlushSqlServerLogIfConfigured();
        }

        public async Task AwaitDurabilityAsync()
        {
            if (!_logicallyCommitted)
                throw new InvalidOperationException("The transaction has not been logically committed.");
            if (_durablyCommitted)
                return;

            await (_durabilityBarrier ?? Task.CompletedTask);
            _durablyCommitted = true;
            await _unitOfWork.PublishPendingEvents();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _transaction.DisposeAsync();
            }
            finally
            {
                if (!_logicallyCommitted)
                {
                    _unitOfWork._pendingEvents.Clear();
                    // Rolled-back entities must not be flushed by a later error
                    // update using the same scoped DbContext.
                    _unitOfWork._dbContext.ChangeTracker.Clear();
                }
            }
        }
    }

    private Task FlushSqlServerLogIfConfigured()
    {
        if (!_storageOptions.CoordinateSqlServerDurableLogFlushes ||
            !string.Equals(
                _dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
            return Task.CompletedTask;

        return _durableLogFlushCoordinator.FlushAsync(
            _dbContext,
            _storageOptions.SqlServerDurableLogFlushWindow,
            _storageOptions.SqlServerDurableLogFlushConcurrency);
    }

    private sealed class NestedRuntimeStorageTransaction : IRuntimeStorageTransaction
    {
        public static NestedRuntimeStorageTransaction Instance { get; } = new();

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
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
