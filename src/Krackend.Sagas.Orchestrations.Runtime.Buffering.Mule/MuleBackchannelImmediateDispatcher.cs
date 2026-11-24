using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

internal interface IMuleBackchannelImmediateDispatcher
{
    Task DispatchDirectAsync(WorkItem workItem, CancellationToken cancellationToken);

    bool TryDispatch(WorkItem workItem);

    bool IsDispatching(string? dispatchId);
}

/// <summary>
/// Executes callbacks directly without creating another database-backed transition
/// for the healthy path. Direct ingress remains protected by the broker's unsettled
/// delivery; already-durable receipts may hand off without occupying their consumer.
/// Mule remains the durable recovery path.
/// </summary>
internal sealed class MuleBackchannelImmediateDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<MuleBackchannelImmediateDispatcher> logger,
    RuntimeForegroundActivity foregroundActivity) : BackgroundService, IMuleBackchannelImmediateDispatcher
{
    private readonly ConcurrentDictionary<string, InstanceMailbox> _mailboxes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, Task> _inFlight = new();
    private readonly ConcurrentDictionary<string, byte> _dispatchesInFlight = new(StringComparer.OrdinalIgnoreCase);
    private CancellationToken _stoppingToken;
    private int _accepting = 1;
    private long _sequence;

    public async Task DispatchDirectAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!TryEnqueue(new PendingCallback(workItem, completion, false)))
            throw new OperationCanceledException("The direct callback dispatcher is stopping.", cancellationToken);

        await completion.Task.WaitAsync(cancellationToken);
    }

    public bool TryDispatch(WorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        var dispatchId = workItem.MessageMetadata?.DispatchId;
        if (!string.IsNullOrWhiteSpace(dispatchId) && !_dispatchesInFlight.TryAdd(dispatchId, 0))
            return true;

        if (TryEnqueue(new PendingCallback(workItem, null, !string.IsNullOrWhiteSpace(dispatchId))))
            return true;
        if (!string.IsNullOrWhiteSpace(dispatchId)) _dispatchesInFlight.TryRemove(dispatchId, out _);
        return false;
    }

    public bool IsDispatching(string? dispatchId)
        => !string.IsNullOrWhiteSpace(dispatchId) && _dispatchesInFlight.ContainsKey(dispatchId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _accepting, 0);
            var remaining = _inFlight.Values.ToArray();
            if (remaining.Length > 0)
                await Task.WhenAll(remaining).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private bool TryEnqueue(PendingCallback pending)
    {
        if (Volatile.Read(ref _accepting) == 0)
            return false;

        var instanceKey = ResolveInstanceKey(pending.WorkItem);
        while (true)
        {
            var mailbox = _mailboxes.GetOrAdd(instanceKey, static _ => new InstanceMailbox());
            Interlocked.Increment(ref mailbox.References);
            if (!_mailboxes.TryGetValue(instanceKey, out var current) || !ReferenceEquals(mailbox, current))
            {
                ReleaseReference(instanceKey, mailbox);
                continue;
            }

            var startWorker = false;
            lock (mailbox.Sync)
            {
                mailbox.Pending.Enqueue(pending);
                if (!mailbox.Running)
                {
                    mailbox.Running = true;
                    startWorker = true;
                }
            }

            if (startWorker)
                StartMailboxWorker(instanceKey, mailbox);
            return true;
        }
    }

    private void StartMailboxWorker(string instanceKey, InstanceMailbox mailbox)
    {
        var id = Interlocked.Increment(ref _sequence);
        // Recovery work starts immediately and yields only on actual asynchronous I/O.
        // Task.Run would add a process-wide ThreadPool queue in front of independent
        // instance mailboxes.
        var task = ProcessMailboxAsync(instanceKey, mailbox, _stoppingToken);
        _inFlight[id] = task;
        _ = task.ContinueWith(
            (_, state) => _inFlight.TryRemove((long)state!, out _),
            id,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ProcessMailboxAsync(
        string instanceKey,
        InstanceMailbox mailbox,
        CancellationToken cancellationToken)
    {
        // Give callbacks produced by the same parallel stage one scheduler turn to
        // rendezvous. This is not a timer or throttle; unrelated instances already
        // have their own workers and run at the same time.
        await Task.Yield();
        while (true)
        {
            PendingCallback[] batch;
            lock (mailbox.Sync)
            {
                if (mailbox.Pending.Count == 0)
                {
                    mailbox.Running = false;
                    return;
                }

                batch = mailbox.Pending.ToArray();
                mailbox.Pending.Clear();
            }

            try
            {
                await ProcessBatchAsync(batch, cancellationToken);
            }
            finally
            {
                foreach (var _ in batch)
                    ReleaseReference(instanceKey, mailbox);
            }
        }
    }

    private async Task ProcessBatchAsync(
        IReadOnlyCollection<PendingCallback> batch,
        CancellationToken cancellationToken)
    {
        using var activity = foregroundActivity.Enter();
        var uniqueWork = batch
            .GroupBy(item => item.WorkItem.MessageMetadata?.DispatchId ??
                $"anonymous:{RuntimeHelpers.GetHashCode(item.WorkItem)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().WorkItem)
            .ToArray();
        RuntimeConcurrencyMetrics.CallbackBatchSize.Record(
            uniqueWork.LongLength,
            new KeyValuePair<string, object?>("expected", ResolveExpectedBatchSize(uniqueWork[0])));
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<ISagaEngine>();
            await BackchannelAction.OrchestrateBatchWhenDispatchIsReadyAsync(
                engine,
                uniqueWork,
                cancellationToken,
                batch.Any(item => item.Completion is not null)
                    ? TimeSpan.FromSeconds(2)
                    : TimeSpan.FromMilliseconds(100));

            foreach (var pending in batch)
                pending.Completion?.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            foreach (var pending in batch)
                pending.Completion?.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception,
                "Direct callback batch activation failed for instance '{InstanceId}'; scheduling durable recovery.",
                batch.First().WorkItem.MessageMetadata?.OrchestrationInstanceId);
            foreach (var pending in batch)
            {
                if (pending.Completion is not null)
                    pending.Completion.TrySetException(exception);
                else
                    await EnqueueRecoveryAsync(pending.WorkItem, cancellationToken);
            }
        }
        finally
        {
            foreach (var pending in batch.Where(item => item.OwnsDispatchReservation))
            {
                var dispatchId = pending.WorkItem.MessageMetadata?.DispatchId;
                if (!string.IsNullOrWhiteSpace(dispatchId))
                    _dispatchesInFlight.TryRemove(dispatchId, out _);
            }
        }
    }

    private static string ResolveInstanceKey(WorkItem workItem)
        => workItem.MessageMetadata?.OrchestrationInstanceId
            ?? workItem.MessageMetadata?.DispatchId
            ?? $"anonymous:{RuntimeHelpers.GetHashCode(workItem)}";

    private static int ResolveExpectedBatchSize(WorkItem workItem)
        => Math.Max(1, workItem.MessageMetadata?.CurrentTasks?.Length ?? 1);

    private void ReleaseReference(string instanceKey, InstanceMailbox mailbox)
    {
        if (Interlocked.Decrement(ref mailbox.References) != 0)
            return;

        ((ICollection<KeyValuePair<string, InstanceMailbox>>)_mailboxes)
            .Remove(new KeyValuePair<string, InstanceMailbox>(instanceKey, mailbox));
    }

    private async Task EnqueueRecoveryAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IMuleClient>();
            await client.EnqueueAsync(MuleActionKeys.BackchannelActionKey, workItem, options =>
            {
                options.Lane = MuleActionKeys.BackchannelLane;
                options.CorrelationId = workItem.MessageMetadata?.OrchestrationInstanceId ??
                                        workItem.MessageMetadata?.CorrelationId;
                options.DeduplicationKey = IntakeBufferMule.BuildBackchannelDeduplicationKey(workItem);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The pending callback receipt is the recovery source of truth.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not enqueue durable callback recovery for dispatch '{DispatchId}'. The pending receipt will be recovered by reconciliation.",
                workItem.MessageMetadata?.DispatchId);
        }
    }

    private sealed class InstanceMailbox
    {
        public object Sync { get; } = new();
        public Queue<PendingCallback> Pending { get; } = new();
        public bool Running;
        public int References;
    }

    private sealed record PendingCallback(
        WorkItem WorkItem,
        TaskCompletionSource? Completion,
        bool OwnsDispatchReservation);
}
