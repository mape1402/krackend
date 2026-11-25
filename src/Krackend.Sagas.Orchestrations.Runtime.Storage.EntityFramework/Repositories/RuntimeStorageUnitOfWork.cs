#nullable enable

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class RuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
{
    private readonly RuntimeDbContext _dbContext;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;
    private readonly ILogger<RuntimeStorageUnitOfWork> _logger;
    private readonly RuntimeEntityFrameworkStorageOptions _storageOptions;
    private readonly IRuntimeHotStateStore _hotState;
    private readonly RuntimeMutationLeaseScope _mutationLease;
    private readonly RuntimeHotStateProjectionActivity _hotStateProjectionActivity;
    private readonly List<RuntimeReactiveEvent> _pendingEvents = [];
    private readonly List<RuntimeHotStateMutation> _pendingHotMutations = [];
    private readonly List<RuntimeHotStateMutation> _pendingDurableHotCacheMutations = [];
    private int _deferCount;
    private bool _hotTransactionActive;

    public RuntimeStorageUnitOfWork(RuntimeDbContext dbContext, IRuntimeReactiveEventPublisher reactiveEventPublisher,
        ILogger<RuntimeStorageUnitOfWork> logger,
        IOptions<RuntimeEntityFrameworkStorageOptions> storageOptions,
        IRuntimeHotStateStore hotState,
        RuntimeMutationLeaseScope mutationLease,
        RuntimeHotStateProjectionActivity hotStateProjectionActivity)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageOptions = storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions));
        _hotState = hotState ?? throw new ArgumentNullException(nameof(hotState));
        _mutationLease = mutationLease ?? throw new ArgumentNullException(nameof(mutationLease));
        _hotStateProjectionActivity = hotStateProjectionActivity
            ?? throw new ArgumentNullException(nameof(hotStateProjectionActivity));
    }

    public bool AutoSaveChanges => _deferCount == 0;

    public bool PreferBatchedEngineActivations
        => _dbContext.Database.IsRelational() &&
           !string.Equals(
            _dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal);

    public async Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (UseHotState)
        {
            if (_hotTransactionActive) return NestedRuntimeStorageTransaction.Instance;
            _hotTransactionActive = true;
            _hotStateProjectionActivity.BeginForeground();
            return new HotStateRuntimeStorageTransaction(this);
        }
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
        var changedEntries = _dbContext.ChangeTracker.Entries()
            .Where(static entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();
        var pendingEntries = changedEntries.Length;
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
            if (!UseHotState &&
                _hotState is IRuntimeHotStateAggregateStore &&
                changedEntries.Any(static entry =>
                    entry.State == EntityState.Added && entry.Entity is OrchestrationInstance))
            {
                _pendingDurableHotCacheMutations.AddRange(changedEntries
                    .Where(entry => RuntimeHotStateEntityCodec.IsSupported(entry.Entity.GetType()))
                    .Select(RuntimeHotStateEntityCodec.CreateMutation));
            }

            if (UseHotState)
            {
                var graphMiss = _hotState is IRuntimeHotStateAggregateStore &&
                                (_mutationLease.CurrentGraph?.Records.Count ?? 0) == 0;
                var hotEntries = _dbContext.ChangeTracker.Entries()
                    .Where(entry => graphMiss ||
                        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                    .Where(entry => RuntimeHotStateEntityCodec.IsSupported(entry.Entity.GetType()))
                    .ToArray();
                var changedHotEntryCount = hotEntries.Count(static entry =>
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
                foreach (var entry in hotEntries)
                {
                    _pendingHotMutations.Add(RuntimeHotStateEntityCodec.CreateMutation(entry));
                    if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                        entry.State = entry.State == EntityState.Deleted ? EntityState.Detached : EntityState.Unchanged;
                }
                pendingEntries -= changedHotEntryCount;
                if (!_hotTransactionActive)
                {
                    await CommitHotMutations(cancellationToken);
                    if (pendingEntries == 0) await PublishPendingEvents();
                }
                if (pendingEntries == 0) return;
            }
            // Let EF finish the complete SaveChanges pipeline before issuing the
            // explicit durability command. Re-entering this DbContext from its own
            // SavedChanges interceptor keeps EF's operation active and eventually
            // creates a feedback loop under sustained concurrent load.
            using (_dbContext.DeferProviderDurability())
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
            await AwaitProviderDurabilityIfConfigured();
            await CacheDurableHotAggregateBestEffort(cancellationToken);
            await PublishPendingEvents();
        }
    }

    private bool UseHotState => _hotState.IsEnabled && _mutationLease.Current is not null;

    private async Task CommitHotMutations(CancellationToken cancellationToken)
    {
        if (_pendingHotMutations.Count == 0) return;
        var mutations = _pendingHotMutations.ToArray();
        var lease = _mutationLease.Current;
        var graph = _mutationLease.CurrentGraph;
        if (lease is null)
        {
            await _hotState.CommitAsync(mutations, cancellationToken);
        }
        else if (graph?.IsOptimisticFence == true &&
                 _hotState is IRuntimeHotStateOptimisticGraphStore optimisticStore)
        {
            await optimisticStore.CommitFencedAsync(
                mutations,
                lease.InstanceId,
                graph.Version,
                cancellationToken);
            _mutationLease.MarkReleasedByCommit(lease.LeaseId);
        }
        else
        {
            await _hotState.CommitAndReleaseAsync(
                mutations,
                lease.InstanceId,
                lease.LeaseId,
                cancellationToken);
            _mutationLease.MarkReleasedByCommit(lease.LeaseId);
        }
        _hotStateProjectionActivity.MarkPending();
        _pendingHotMutations.Clear();
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

    private async Task CacheDurableHotAggregateBestEffort(CancellationToken cancellationToken)
    {
        if (_hotState is not IRuntimeHotStateAggregateStore aggregateStore ||
            _pendingDurableHotCacheMutations.Count == 0)
            return;

        var pending = _pendingDurableHotCacheMutations.ToArray();
        _pendingDurableHotCacheMutations.Clear();
        foreach (var group in pending
                     .Where(static mutation => !string.IsNullOrWhiteSpace(mutation.InstanceId))
                     .GroupBy(static mutation => mutation.InstanceId!, StringComparer.Ordinal))
        {
            try
            {
                await aggregateStore.CacheAggregateAsync(
                    new Id(Ulid.Parse(group.Key)),
                    group.ToArray(),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Could not cache the durably created saga aggregate '{InstanceId}' in hot state.",
                    group.Key);
            }
        }
    }

    private sealed class EntityFrameworkRuntimeStorageTransaction : IRuntimePipelinedStorageTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly RuntimeStorageUnitOfWork _unitOfWork;
        private Task? _durabilityBarrier;
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
            await _unitOfWork.CacheDurableHotAggregateBestEffort(cancellationToken);

            // A forced-delayed commit is visible and atomic but not yet a safe broker
            // acknowledgement boundary. Start the physical barrier immediately; the
            // coordinator may overlap already-committed dispatch I/O with this task.
            _durabilityBarrier = _unitOfWork.AwaitProviderDurabilityIfConfigured();
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
                    _unitOfWork._pendingDurableHotCacheMutations.Clear();
                    // Rolled-back entities must not be flushed by a later error
                    // update using the same scoped DbContext.
                    _unitOfWork._dbContext.ChangeTracker.Clear();
                }
            }
        }
    }

    private sealed class HotStateRuntimeStorageTransaction(RuntimeStorageUnitOfWork unitOfWork)
        : IRuntimePipelinedStorageTransaction
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await CommitLogicalAsync(cancellationToken);
            await AwaitDurabilityAsync();
        }

        public async Task CommitLogicalAsync(CancellationToken cancellationToken = default)
        {
            if (_committed) return;
            await unitOfWork.CommitHotMutations(cancellationToken);
            _committed = true;
        }

        public async Task AwaitDurabilityAsync()
        {
            if (!_committed) throw new InvalidOperationException("The hot-state transaction has not been committed.");
            await unitOfWork.PublishPendingEvents();
        }

        public ValueTask DisposeAsync()
        {
            unitOfWork._hotTransactionActive = false;
            unitOfWork._hotStateProjectionActivity.EndForeground();
            if (!_committed)
            {
                unitOfWork._pendingHotMutations.Clear();
                unitOfWork._pendingEvents.Clear();
                unitOfWork._dbContext.ChangeTracker.Clear();
            }
            return ValueTask.CompletedTask;
        }
    }

    private Task AwaitProviderDurabilityIfConfigured()
        => _storageOptions.ProviderOptimizations?.AwaitDurabilityAsync?.Invoke(_dbContext)
            ?? Task.CompletedTask;

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
