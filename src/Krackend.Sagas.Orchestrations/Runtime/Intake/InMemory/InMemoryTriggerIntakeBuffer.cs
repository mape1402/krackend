using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

namespace Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;

/// <summary>
/// In-memory trigger intake buffer for Beta 1 and local E2E flows.
/// </summary>
public sealed class InMemoryTriggerIntakeBuffer : ITriggerIntakeBuffer
{
    private readonly object _syncRoot = new();
    private readonly Queue<Id> _pending = new();
    private readonly Dictionary<Id, BufferedItem> _items = new();
    private readonly Dictionary<string, Id> _idempotencyIndex = new(StringComparer.Ordinal);
    private readonly InMemoryTriggerIntakeBufferOptions _options;

    public InMemoryTriggerIntakeBuffer(InMemoryTriggerIntakeBufferOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<TriggerIntakeBufferResult> Enqueue(
        TriggerIntakeBufferItem item,
        CancellationToken cancellationToken = default)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.TriggerKey))
        {
            return Task.FromResult(TriggerIntakeBufferResult.Reject("Trigger key is required."));
        }

        if (string.IsNullOrWhiteSpace(item.EnvironmentKey))
        {
            return Task.FromResult(TriggerIntakeBufferResult.Reject("Environment key is required."));
        }

        if (string.IsNullOrWhiteSpace(item.PayloadJson))
        {
            return Task.FromResult(TriggerIntakeBufferResult.Reject("Payload json is required."));
        }

        lock (_syncRoot)
        {
            if (_items.Count >= _options.Capacity)
            {
                return Task.FromResult(TriggerIntakeBufferResult.Reject("In-memory intake buffer capacity reached."));
            }

            var idempotencyKey = BuildIdempotencyKey(item);
            if (idempotencyKey is not null && _idempotencyIndex.TryGetValue(idempotencyKey, out var existingId))
            {
                return Task.FromResult(TriggerIntakeBufferResult.Accept(existingId, "Duplicate intake item already accepted."));
            }

            if (item.BufferItemId == default)
            {
                item.BufferItemId = Id.New();
            }

            _items[item.BufferItemId] = new BufferedItem(item);
            _pending.Enqueue(item.BufferItemId);

            if (idempotencyKey is not null)
            {
                _idempotencyIndex[idempotencyKey] = item.BufferItemId;
            }

            return Task.FromResult(TriggerIntakeBufferResult.Accept(item.BufferItemId));
        }
    }

    public Task<TriggerIntakeBufferLease> TryDequeue(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            while (_pending.Count > 0)
            {
                var itemId = _pending.Dequeue();
                if (!_items.TryGetValue(itemId, out var buffered) || buffered.Status != BufferedItemStatus.Pending)
                {
                    continue;
                }

                buffered.Status = BufferedItemStatus.Leased;
                buffered.LeaseId = Guid.NewGuid().ToString("N");
                buffered.LeasedOnUtc = DateTime.UtcNow;

                return Task.FromResult(new TriggerIntakeBufferLease(
                    buffered.Item,
                    buffered.LeaseId,
                    buffered.LeasedOnUtc.Value));
            }

            return Task.FromResult<TriggerIntakeBufferLease>(null);
        }
    }

    public Task<TriggerIntakeBufferItem> Peek(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            foreach (var itemId in _pending)
            {
                if (_items.TryGetValue(itemId, out var buffered) && buffered.Status == BufferedItemStatus.Pending)
                {
                    return Task.FromResult(buffered.Item);
                }
            }

            return Task.FromResult<TriggerIntakeBufferItem>(null);
        }
    }

    public Task<TriggerIntakeBufferResult> MarkCompleted(
        Id bufferItemId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (!TryGetLeased(bufferItemId, leaseId, out var buffered, out var result))
            {
                return Task.FromResult(result);
            }

            buffered.Status = BufferedItemStatus.Completed;
            buffered.CompletedOnUtc = DateTime.UtcNow;
            return Task.FromResult(TriggerIntakeBufferResult.Accept(bufferItemId));
        }
    }

    public Task<TriggerIntakeBufferResult> MarkFailed(
        Id bufferItemId,
        string leaseId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (!TryGetLeased(bufferItemId, leaseId, out var buffered, out var result))
            {
                return Task.FromResult(result);
            }

            buffered.Status = BufferedItemStatus.Failed;
            buffered.FailedOnUtc = DateTime.UtcNow;
            buffered.FailureReason = reason;
            return Task.FromResult(TriggerIntakeBufferResult.Accept(bufferItemId));
        }
    }

    private bool TryGetLeased(
        Id bufferItemId,
        string leaseId,
        out BufferedItem buffered,
        out TriggerIntakeBufferResult result)
    {
        buffered = null;

        if (!_items.TryGetValue(bufferItemId, out buffered))
        {
            result = TriggerIntakeBufferResult.Reject("Intake buffer item was not found.");
            return false;
        }

        if (buffered.Status != BufferedItemStatus.Leased)
        {
            result = TriggerIntakeBufferResult.Reject("Intake buffer item is not leased.");
            return false;
        }

        if (!string.Equals(buffered.LeaseId, leaseId, StringComparison.Ordinal))
        {
            result = TriggerIntakeBufferResult.Reject("Intake buffer lease id does not match.");
            return false;
        }

        result = null;
        return true;
    }

    private static string BuildIdempotencyKey(TriggerIntakeBufferItem item)
    {
        if (string.IsNullOrWhiteSpace(item.IdempotencyKey))
        {
            return null;
        }

        return $"{item.EnvironmentKey.Trim()}::{item.IdempotencyKey.Trim()}";
    }

    private sealed class BufferedItem
    {
        public BufferedItem(TriggerIntakeBufferItem item)
        {
            Item = item;
        }

        public TriggerIntakeBufferItem Item { get; }
        public BufferedItemStatus Status { get; set; } = BufferedItemStatus.Pending;
        public string LeaseId { get; set; }
        public DateTime? LeasedOnUtc { get; set; }
        public DateTime? CompletedOnUtc { get; set; }
        public DateTime? FailedOnUtc { get; set; }
        public string FailureReason { get; set; }
    }

    private enum BufferedItemStatus
    {
        Pending = 1,
        Leased = 2,
        Completed = 3,
        Failed = 4
    }
}
