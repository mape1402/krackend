using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Replication;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

internal sealed class DefaultOrchestrationInstanceCoordinator : IOrchestrationInstanceCoordinator,
    INewOrchestrationInstanceCoordinator, IOrchestrationPostLeaseWork
{
    private static readonly AsyncLocal<Dictionary<Id, DefaultOrchestrationInstanceCoordinator>> HeldInstances = new();

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrchestrationCoordinationOptions _options;
    private readonly ILogger<DefaultOrchestrationInstanceCoordinator> _logger;
    private readonly IRuntimeStorageUnitOfWork _storageUnitOfWork;
    private readonly RuntimeMutationLeaseScope _mutationLease;
    private readonly IRuntimeHotStateStore _hotState;
    private readonly OrchestrationInstanceLocalGate _localGate;
    private readonly string _replicaBootId;
    private readonly Dictionary<Id, PostLeaseWork> _postLeaseWork = new();

    public bool HasPendingWork(Id instanceId)
        => _postLeaseWork.TryGetValue(instanceId, out var pending) && pending.HasWork;

    public bool IsWithinMutation(Id instanceId) => IsHeld(instanceId);

    public Task ExecuteAfterLeaseReleaseAsync(Id instanceId, Func<CancellationToken, Task> activation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (IsHeld(instanceId) && _postLeaseWork.TryGetValue(instanceId, out var pending))
        {
            pending.Continuations.Add(activation);
            return Task.CompletedTask;
        }
        return activation(cancellationToken);
    }

    public Task ExecuteIsolatedAfterLeaseReleaseAsync(
        Id instanceId,
        Func<IServiceProvider, CancellationToken, Task> activation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (IsHeld(instanceId) && _postLeaseWork.TryGetValue(instanceId, out var pending))
        {
            // The intent is committed before activation. The input handler still waits
            // for its durability barrier before ACK, so a crash during the overlap is
            // recovered by broker redelivery.
            pending.IsolatedActivations.Add(activation);
            return Task.CompletedTask;
        }

        return ExecuteIsolatedAsync(activation, cancellationToken);
    }

    public DefaultOrchestrationInstanceCoordinator(
        IOrchestrationInstanceRepository instanceRepository,
        IServiceScopeFactory scopeFactory,
        IOptions<OrchestrationCoordinationOptions> options,
        ILogger<DefaultOrchestrationInstanceCoordinator> logger,
        IRuntimeStorageUnitOfWork storageUnitOfWork,
        RuntimeMutationLeaseScope mutationLease = null,
        IRuntimeReplicaIdentity replicaIdentity = null,
        OrchestrationInstanceLocalGate localGate = null,
        IRuntimeHotStateStore hotState = null)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? new OrchestrationCoordinationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageUnitOfWork = storageUnitOfWork ?? throw new ArgumentNullException(nameof(storageUnitOfWork));
        _mutationLease = mutationLease ?? new RuntimeMutationLeaseScope();
        _hotState = hotState;
        _localGate = localGate ?? new OrchestrationInstanceLocalGate();
        _replicaBootId = replicaIdentity?.ReplicaBootId ?? $"process-{Environment.ProcessId}";
    }

    public async Task<bool> TryExecuteAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        bool waitForLease = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.Enabled || IsHeld(instanceId))
        {
            await operation(cancellationToken);
            return true;
        }

        var localLease = await _localGate.EnterAsync(instanceId, waitForLease, cancellationToken);
        if (localLease is null)
        {
            RuntimeConcurrencyMetrics.LeaseContention.Add(1);
            return false;
        }

        if (!_options.DistributedLeaseEnabled)
            return await TryExecuteWithLocalGateAsync(
                instanceId, operation, localLease, cancellationToken);

        if (_hotState?.IsEnabled == true && _storageUnitOfWork.PreferBatchedEngineActivations)
            return await TryExecuteWithHotStateAsync(
                instanceId, operation, localLease, cancellationToken, waitForLease);

        if (!_storageUnitOfWork.PreferBatchedEngineActivations)
            return await TryExecuteWithDurableLeaseAsync(
                instanceId, operation, localLease, cancellationToken, waitForLease);

        var leaseId = $"runtime:{_replicaBootId}:{Id.NewDistributed()}";
        var leaseDuration = ResolveLeaseDuration(_options);
        PostLeaseWork postLeaseWork = null;
        IRuntimePipelinedStorageTransaction pipelinedTransaction = null;
        try
        {
            // The conditional lease update and every state mutation participate in the
            // same transaction. SQL's row lock is the distributed mutex while the
            // transaction is active; clearing the lease before the single commit avoids
            // two extra log flushes for every callback/advance.
            await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
            using (_storageUnitOfWork.DeferAutoSave())
            {
                var acquisitionStarted = Stopwatch.GetTimestamp();
                var acquisition = await TryAcquireLeaseAsync(
                    instanceId, leaseId, leaseDuration, waitForLease, cancellationToken);
                RuntimeConcurrencyMetrics.LeaseAcquisitionMilliseconds.Record(
                    Stopwatch.GetElapsedTime(acquisitionStarted).TotalMilliseconds);

                if (acquisition.InstanceMissing)
                {
                    // A start activation coordinates an id that is intentionally not
                    // visible yet. Hold that id exactly like an existing instance so
                    // dispatch publication is deferred until the instance and its first
                    // dispatch graph have committed.
                    Push(instanceId);
                    postLeaseWork = new PostLeaseWork();
                    _postLeaseWork.Add(instanceId, postLeaseWork);
                    try
                    {
                        await operation(cancellationToken);
                    }
                    finally
                    {
                        Pop(instanceId);
                        _postLeaseWork.Remove(instanceId);
                    }

                    await _storageUnitOfWork.SaveChanges(cancellationToken);
                    pipelinedTransaction = await CommitForDispatchPipeliningAsync(
                        transaction, cancellationToken);
                }
                else if (acquisition.Lease is null)
                {
                    RuntimeConcurrencyMetrics.LeaseContention.Add(1);
                    return false;
                }
                else
                {
                    Push(instanceId);
                    postLeaseWork = new PostLeaseWork();
                    _postLeaseWork.Add(instanceId, postLeaseWork);
                    var mutationScope = _mutationLease.Enter(acquisition.Lease);
                    var mutationStarted = Stopwatch.GetTimestamp();
                    using var logScope = _logger.BeginScope(new Dictionary<string, object>
                    {
                        ["OrchestrationInstanceId"] = instanceId.ToString(),
                        ["MutationLeaseId"] = leaseId,
                        ["ReplicaBootId"] = _replicaBootId,
                        ["LeaseExpiresOnUtc"] = acquisition.Lease.ExpiresOnUtc
                    });

                    Exception mutationFailure = null;
                    try
                    {
                        await operation(cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        mutationFailure = exception;
                        throw;
                    }
                    finally
                    {
                        mutationScope.Dispose();
                        RuntimeConcurrencyMetrics.MutationMilliseconds.Record(
                            Stopwatch.GetElapsedTime(mutationStarted).TotalMilliseconds);
                        Pop(instanceId);
                        _postLeaseWork.Remove(instanceId);
                        try
                        {
                            await _instanceRepository.ReleaseLease(
                                instanceId, leaseId, CancellationToken.None);
                        }
                        catch (Exception releaseException) when (mutationFailure is not null)
                        {
                            _logger.LogWarning(
                                releaseException,
                                "Could not release orchestration lease '{leaseId}' for instance '{instanceId}' after a failed mutation.",
                                leaseId,
                                instanceId);
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await _storageUnitOfWork.SaveChanges(cancellationToken);
                    pipelinedTransaction = await CommitForDispatchPipeliningAsync(
                        transaction, cancellationToken);
                }
            }
        }
        finally
        {
            localLease.Dispose();
        }

        await ExecutePostLeaseWorkAndDurabilityAsync(
            postLeaseWork, pipelinedTransaction, cancellationToken);
        return true;
    }

    private async Task<bool> TryExecuteWithHotStateAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        IDisposable localLease,
        CancellationToken cancellationToken,
        bool waitForLease)
    {
        var leaseId = $"runtime:{_replicaBootId}:{Id.NewDistributed()}";
        var leaseDuration = ResolveLeaseDuration(_options);
        var acquisitionStarted = Stopwatch.GetTimestamp();
        (bool InstanceMissing, OrchestrationInstanceLease Lease, RuntimeHotStateLeaseGraph Graph) acquisition;
        try
        {
            if (_hotState is IRuntimeHotStateOptimisticGraphStore optimisticStore)
            {
                var graph = await optimisticStore.ReadGraphFenceAsync(instanceId, cancellationToken);
                var now = DateTime.UtcNow;
                acquisition = (
                    false,
                    new OrchestrationInstanceLease(
                        instanceId,
                        leaseId,
                        now,
                        now.Add(leaseDuration)),
                    graph);
            }
            else if (_hotState is IRuntimeHotStateLeaseGraphStore graphStore)
            {
                acquisition = await TryAcquireHotStateLeaseAsync(
                    graphStore,
                    instanceId,
                    leaseId,
                    leaseDuration,
                    waitForLease,
                    cancellationToken,
                    _options);
            }
            else
            {
                var lease = await TryAcquireLeaseAsync(
                    instanceId, leaseId, leaseDuration, waitForLease, cancellationToken);
                acquisition = (lease.InstanceMissing, lease.Lease, null);
            }
        }
        catch
        {
            localLease.Dispose();
            throw;
        }
        RuntimeConcurrencyMetrics.LeaseAcquisitionMilliseconds.Record(
            Stopwatch.GetElapsedTime(acquisitionStarted).TotalMilliseconds);
        if (acquisition.Lease is null)
        {
            localLease.Dispose();
            RuntimeConcurrencyMetrics.LeaseContention.Add(1);
            return false;
        }

        PostLeaseWork postLeaseWork = null;
        IRuntimePipelinedStorageTransaction pipelinedTransaction = null;
        var optimisticFence = acquisition.Graph?.IsOptimisticFence == true;
        var leaseHeld = !optimisticFence;
        try
        {
            using var mutationScope = _mutationLease.Enter(acquisition.Lease, acquisition.Graph);
            await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
            using (_storageUnitOfWork.DeferAutoSave())
            {
                Push(instanceId);
                postLeaseWork = new PostLeaseWork();
                _postLeaseWork.Add(instanceId, postLeaseWork);
                var mutationStarted = Stopwatch.GetTimestamp();
                using var logScope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["OrchestrationInstanceId"] = instanceId.ToString(),
                    ["MutationLeaseId"] = leaseId,
                    ["ReplicaBootId"] = _replicaBootId,
                    ["LeaseExpiresOnUtc"] = acquisition.Lease.ExpiresOnUtc
                });
                try
                {
                    await operation(cancellationToken);
                    await _storageUnitOfWork.SaveChanges(cancellationToken);
                    pipelinedTransaction = await CommitForDispatchPipeliningAsync(
                        transaction, cancellationToken);
                }
                finally
                {
                    RuntimeConcurrencyMetrics.MutationMilliseconds.Record(
                        Stopwatch.GetElapsedTime(mutationStarted).TotalMilliseconds);
                    Pop(instanceId);
                    _postLeaseWork.Remove(instanceId);
                }
            }

            if (!optimisticFence && !_mutationLease.ReleasedByCommit)
                await _instanceRepository.ReleaseLease(instanceId, leaseId, CancellationToken.None);
            leaseHeld = false;
        }
        finally
        {
            if (leaseHeld)
            {
                try { await _instanceRepository.ReleaseLease(instanceId, leaseId, CancellationToken.None); }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception,
                        "Could not release hot-state lease '{leaseId}' for instance '{instanceId}'.",
                        leaseId, instanceId);
                }
            }
            localLease.Dispose();
        }

        await ExecutePostLeaseWorkAndDurabilityAsync(
            postLeaseWork, pipelinedTransaction, cancellationToken);
        return true;
    }

    public async Task<bool> TryExecuteNewAsync(
        Id proposedInstanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        bool waitForLocalGate = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.Enabled || IsHeld(proposedInstanceId))
        {
            await operation(cancellationToken);
            return true;
        }

        var localLease = await _localGate.EnterAsync(
            proposedInstanceId, waitForLocalGate, cancellationToken);
        if (localLease is null)
        {
            RuntimeConcurrencyMetrics.LeaseContention.Add(1);
            return false;
        }

        if (_hotState?.IsEnabled == true &&
            _hotState is not IRuntimeHotStateAggregateStore &&
            _storageUnitOfWork.PreferBatchedEngineActivations)
            return await TryExecuteWithHotStateAsync(
                proposedInstanceId, operation, localLease, cancellationToken, waitForLocalGate);

        // There cannot be a durable lease for a row that does not exist yet. The unique
        // instance id and start-idempotency indexes arbitrate the extremely rare creation
        // race at commit. Avoiding the guaranteed-miss UPDATE + SELECT removes two SQL
        // commands from every healthy trigger while retaining one atomic graph commit.
        if (!_options.DistributedLeaseEnabled || !_storageUnitOfWork.PreferBatchedEngineActivations)
            return await TryExecuteWithLocalGateAsync(
                proposedInstanceId, operation, localLease, cancellationToken);

        PostLeaseWork postLeaseWork = null;
        IRuntimePipelinedStorageTransaction pipelinedTransaction = null;
        try
        {
            await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
            using (_storageUnitOfWork.DeferAutoSave())
            {
                Push(proposedInstanceId);
                postLeaseWork = new PostLeaseWork();
                _postLeaseWork.Add(proposedInstanceId, postLeaseWork);
                var mutationStarted = Stopwatch.GetTimestamp();
                try
                {
                    await operation(cancellationToken);
                }
                finally
                {
                    RuntimeConcurrencyMetrics.MutationMilliseconds.Record(
                        Stopwatch.GetElapsedTime(mutationStarted).TotalMilliseconds);
                    Pop(proposedInstanceId);
                    _postLeaseWork.Remove(proposedInstanceId);
                }

                await _storageUnitOfWork.SaveChanges(cancellationToken);
                pipelinedTransaction = await CommitForDispatchPipeliningAsync(
                    transaction, cancellationToken);
            }
        }
        finally
        {
            localLease.Dispose();
        }

        await ExecutePostLeaseWorkAndDurabilityAsync(
            postLeaseWork, pipelinedTransaction, cancellationToken);
        return true;
    }

    private static async Task<IRuntimePipelinedStorageTransaction> CommitForDispatchPipeliningAsync(
        IRuntimeStorageTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is IRuntimePipelinedStorageTransaction pipelined)
        {
            await pipelined.CommitLogicalAsync(cancellationToken);
            return pipelined;
        }

        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private async Task ExecutePostLeaseWorkAndDurabilityAsync(
        PostLeaseWork postLeaseWork,
        IRuntimePipelinedStorageTransaction pipelinedTransaction,
        CancellationToken cancellationToken)
    {
        ExceptionDispatchInfo activationFailure = null;
        try
        {
            await ExecutePostLeaseWorkAsync(postLeaseWork, cancellationToken);
        }
        catch (Exception exception)
        {
            activationFailure = ExceptionDispatchInfo.Capture(exception);
        }

        Exception durabilityFailure = null;
        if (pipelinedTransaction is not null)
        {
            try
            {
                // The input consumer must not ACK until this completes. Do not pass
                // caller cancellation after logical commit; a cancelled request must
                // not turn a durable transition into a best-effort transition.
                await pipelinedTransaction.AwaitDurabilityAsync();
            }
            catch (Exception exception)
            {
                durabilityFailure = exception;
            }
        }

        if (activationFailure is not null && durabilityFailure is not null)
            throw new AggregateException(activationFailure.SourceException, durabilityFailure);
        if (durabilityFailure is not null)
            ExceptionDispatchInfo.Capture(durabilityFailure).Throw();
        activationFailure?.Throw();
    }

    private async Task<bool> TryExecuteWithLocalGateAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        IDisposable localLease,
        CancellationToken cancellationToken)
    {
        if (_storageUnitOfWork.PreferBatchedEngineActivations)
            return await TryExecuteWithBatchedLocalGateAsync(
                instanceId, operation, localLease, cancellationToken);

        PostLeaseWork postLeaseWork = null;
        Push(instanceId);
        try
        {
            postLeaseWork = new PostLeaseWork();
            _postLeaseWork.Add(instanceId, postLeaseWork);
            var mutationStarted = Stopwatch.GetTimestamp();
            try
            {
                await operation(cancellationToken);
            }
            finally
            {
                RuntimeConcurrencyMetrics.MutationMilliseconds.Record(
                    Stopwatch.GetElapsedTime(mutationStarted).TotalMilliseconds);
                _postLeaseWork.Remove(instanceId);
                Pop(instanceId);
            }
        }
        finally
        {
            localLease.Dispose();
        }

        await ExecutePostLeaseWorkAsync(postLeaseWork, cancellationToken);
        return true;
    }

    private async Task<bool> TryExecuteWithBatchedLocalGateAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        IDisposable localLease,
        CancellationToken cancellationToken)
    {
        PostLeaseWork postLeaseWork = null;
        IRuntimePipelinedStorageTransaction pipelinedTransaction = null;
        try
        {
            // A single-runtime deployment still needs the same atomic activation and
            // dispatch/durability overlap as the distributed-lease path. Previously the
            // nested engine batch committed and physically flushed before this local gate
            // was released, serializing every next callback behind SQL log I/O.
            await using var transaction = await _storageUnitOfWork.BeginTransactionAsync(cancellationToken);
            using (_storageUnitOfWork.DeferAutoSave())
            {
                Push(instanceId);
                postLeaseWork = new PostLeaseWork();
                _postLeaseWork.Add(instanceId, postLeaseWork);
                var mutationStarted = Stopwatch.GetTimestamp();
                try
                {
                    await operation(cancellationToken);
                }
                finally
                {
                    RuntimeConcurrencyMetrics.MutationMilliseconds.Record(
                        Stopwatch.GetElapsedTime(mutationStarted).TotalMilliseconds);
                    _postLeaseWork.Remove(instanceId);
                    Pop(instanceId);
                }

                await _storageUnitOfWork.SaveChanges(cancellationToken);
                pipelinedTransaction = await CommitForDispatchPipeliningAsync(
                    transaction, cancellationToken);
            }
        }
        finally
        {
            localLease.Dispose();
        }

        await ExecutePostLeaseWorkAndDurabilityAsync(
            postLeaseWork, pipelinedTransaction, cancellationToken);
        return true;
    }

    private async Task<bool> TryExecuteWithDurableLeaseAsync(
        Id instanceId,
        Func<CancellationToken, Task> operation,
        IDisposable localLease,
        CancellationToken cancellationToken,
        bool waitForLease)
    {
        var leaseId = $"runtime:{_replicaBootId}:{Id.NewDistributed()}";
        var leaseDuration = ResolveLeaseDuration(_options);
        var acquisitionStarted = Stopwatch.GetTimestamp();
        (bool InstanceMissing, OrchestrationInstanceLease Lease) acquisition;
        try
        {
            acquisition = await TryAcquireLeaseAsync(
                instanceId, leaseId, leaseDuration, waitForLease, cancellationToken);
        }
        catch
        {
            localLease.Dispose();
            throw;
        }
        RuntimeConcurrencyMetrics.LeaseAcquisitionMilliseconds.Record(
            Stopwatch.GetElapsedTime(acquisitionStarted).TotalMilliseconds);
        if (acquisition.InstanceMissing)
        {
            try { await operation(cancellationToken); }
            finally { localLease.Dispose(); }
            return true;
        }

        if (acquisition.Lease is null)
        {
            localLease.Dispose();
            RuntimeConcurrencyMetrics.LeaseContention.Add(1);
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = RenewLeaseUntilCancelledAsync(instanceId, leaseId, leaseDuration, linkedCancellation);
        Push(instanceId);
        var leaseLost = false;
        var postLeaseWork = new PostLeaseWork();
        _postLeaseWork.Add(instanceId, postLeaseWork);
        var mutationScope = _mutationLease.Enter(acquisition.Lease);
        var mutationStarted = Stopwatch.GetTimestamp();
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OrchestrationInstanceId"] = instanceId.ToString(),
            ["MutationLeaseId"] = leaseId,
            ["ReplicaBootId"] = _replicaBootId,
            ["LeaseExpiresOnUtc"] = acquisition.Lease.ExpiresOnUtc
        });

        try
        {
            await operation(linkedCancellation.Token);
        }
        finally
        {
            mutationScope.Dispose();
            RuntimeConcurrencyMetrics.MutationMilliseconds.Record(
                Stopwatch.GetElapsedTime(mutationStarted).TotalMilliseconds);
            Pop(instanceId);
            _postLeaseWork.Remove(instanceId);
            await linkedCancellation.CancelAsync();

            try { leaseLost = await renewalTask; }
            catch (OperationCanceledException) { }

            try { await _instanceRepository.ReleaseLease(instanceId, leaseId, CancellationToken.None); }
            finally { localLease.Dispose(); }
        }

        if (leaseLost)
            throw new OperationCanceledException(
                $"The orchestration lease for instance '{instanceId}' was lost.", linkedCancellation.Token);

        cancellationToken.ThrowIfCancellationRequested();
        await ExecutePostLeaseWorkAsync(postLeaseWork, cancellationToken);
        return true;
    }

    private async Task ExecutePostLeaseWorkAsync(
        PostLeaseWork postLeaseWork,
        CancellationToken cancellationToken)
    {
        Exception activationFailure = null;
        if (postLeaseWork.IsolatedActivations.Count > 0)
        {
            var isolated = postLeaseWork.IsolatedActivations
                .Select(activation => ExecuteIsolatedAsync(activation, CancellationToken.None))
                .ToArray();

            if (postLeaseWork.Continuations.Count == 0)
            {
                // The producing transaction already committed every dispatch intent.
                // Independent transport activations own fresh scopes and must not keep
                // the trigger/callback consumer waiting on external I/O. Observe all
                // failures; the persisted intents remain eligible for recovery.
                _ = Task.WhenAll(isolated).ContinueWith(
                    task => _logger.LogWarning(task.Exception,
                        "One or more isolated post-commit activations failed; durable recovery will retry them."),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return;
            }

            try
            {
                // A registered continuation explicitly depends on all isolated work.
                // Preserve that local ordering boundary without affecting unrelated
                // instances, whose coordinators execute independently.
                await Task.WhenAll(isolated);
            }
            catch (Exception exception)
            {
                // Task.WhenAll observes every activation. Their intents are already durable,
                // so a failed activation must not prevent the remaining continuations.
                activationFailure = exception;
            }
        }

        foreach (var continuation in postLeaseWork.Continuations)
        {
            try { await continuation(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { activationFailure ??= exception; }
        }

        if (activationFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(activationFailure).Throw();
    }

    private async Task ExecuteIsolatedAsync(
        Func<IServiceProvider, CancellationToken, Task> activation,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        await activation(scope.ServiceProvider, cancellationToken);
    }

    private async Task<(bool InstanceMissing, OrchestrationInstanceLease Lease)> TryAcquireLeaseAsync(
        Id instanceId,
        string leaseId,
        TimeSpan leaseDuration,
        bool waitForLease,
        CancellationToken cancellationToken)
    {
        var waitWindow = ResolveLeaseUnavailableWait(_options);
        var retryDelay = ResolveLeaseUnavailableRetryDelay(_options);
        var deadline = waitForLease ? DateTime.UtcNow.Add(waitWindow) : DateTime.UtcNow;

        while (true)
        {
            var now = DateTime.UtcNow;
            try
            {
                var lease = await _instanceRepository.TryAcquireLease(
                    instanceId,
                    leaseId,
                    now,
                    now.Add(leaseDuration),
                    cancellationToken);
                if (lease is not null || now >= deadline)
                {
                    return (false, lease);
                }
            }
            catch (KeyNotFoundException)
            {
                return (true, null);
            }

            await Task.Delay(retryDelay, cancellationToken);
        }
    }

    private static async Task<(bool InstanceMissing, OrchestrationInstanceLease Lease, RuntimeHotStateLeaseGraph Graph)>
        TryAcquireHotStateLeaseAsync(
            IRuntimeHotStateLeaseGraphStore graphStore,
            Id instanceId,
            string leaseId,
            TimeSpan leaseDuration,
            bool waitForLease,
            CancellationToken cancellationToken,
            OrchestrationCoordinationOptions options)
    {
        var waitWindow = ResolveLeaseUnavailableWait(options);
        var retryDelay = ResolveLeaseUnavailableRetryDelay(options);
        var deadline = waitForLease ? DateTime.UtcNow.Add(waitWindow) : DateTime.UtcNow;

        while (true)
        {
            var now = DateTime.UtcNow;
            var graph = await graphStore.TryAcquireLeaseWithGraphAsync(
                instanceId,
                leaseId,
                leaseDuration,
                cancellationToken);
            if (graph is not null)
            {
                return (
                    false,
                    new OrchestrationInstanceLease(
                        instanceId,
                        leaseId,
                        now,
                        now.Add(leaseDuration)),
                    graph);
            }
            if (now >= deadline)
                return (false, null, null);

            await Task.Delay(retryDelay, cancellationToken);
        }
    }

    private async Task<bool> RenewLeaseUntilCancelledAsync(
        Id instanceId,
        string leaseId,
        TimeSpan leaseDuration,
        CancellationTokenSource linkedCancellation)
    {
        var renewalInterval = TimeSpan.FromMilliseconds(Math.Max(250, leaseDuration.TotalMilliseconds / 2));

        while (!linkedCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(renewalInterval, linkedCancellation.Token);
                var now = DateTime.UtcNow;
                await using var scope = _scopeFactory.CreateAsyncScope();
                var renewalRepository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
                var renewed = await renewalRepository.TryAcquireLease(
                    instanceId,
                    leaseId,
                    now,
                    now.Add(leaseDuration),
                    linkedCancellation.Token);

                if (renewed is null)
                {
                    _logger.LogWarning(
                        "Lost orchestration lease '{leaseId}' for instance '{instanceId}'. Cancelling the active mutation scope.",
                        leaseId,
                        instanceId);
                    await linkedCancellation.CancelAsync();
                    return true;
                }
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not renew orchestration lease '{leaseId}' for instance '{instanceId}'. Cancelling the active mutation scope.",
                    leaseId,
                    instanceId);
                await linkedCancellation.CancelAsync();
                return true;
            }
        }

        return false;
    }

    private bool IsHeld(Id instanceId)
        => HeldInstances.Value?.TryGetValue(instanceId, out var owner) == true && ReferenceEquals(owner, this);

    private void Push(Id instanceId)
    {
        var held = HeldInstances.Value is { } current
            ? new Dictionary<Id, DefaultOrchestrationInstanceCoordinator>(current)
            : new Dictionary<Id, DefaultOrchestrationInstanceCoordinator>();
        held[instanceId] = this;
        HeldInstances.Value = held;
    }

    private static void Pop(Id instanceId)
    {
        var held = HeldInstances.Value;
        if (held is null)
        {
            return;
        }

        var updated = new Dictionary<Id, DefaultOrchestrationInstanceCoordinator>(held);
        updated.Remove(instanceId);
        HeldInstances.Value = updated.Count == 0 ? null : updated;
    }

    private static TimeSpan ResolveLeaseDuration(OrchestrationCoordinationOptions options)
        => TimeSpan.FromSeconds(Math.Max(1, options.LeaseDurationSeconds));

    private static TimeSpan ResolveLeaseUnavailableWait(OrchestrationCoordinationOptions options)
        => TimeSpan.FromMilliseconds(Math.Max(0, options.LeaseUnavailableWaitMilliseconds));

    private static TimeSpan ResolveLeaseUnavailableRetryDelay(OrchestrationCoordinationOptions options)
        => TimeSpan.FromMilliseconds(Math.Max(1, options.LeaseUnavailableRetryDelayMilliseconds));

    private sealed class PostLeaseWork
    {
        public List<Func<IServiceProvider, CancellationToken, Task>> IsolatedActivations { get; } = [];
        public List<Func<CancellationToken, Task>> Continuations { get; } = [];
        public bool HasWork => IsolatedActivations.Count > 0 || Continuations.Count > 0;
    }
}
