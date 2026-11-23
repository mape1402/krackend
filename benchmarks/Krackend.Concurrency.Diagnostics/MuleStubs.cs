// Test doubles for exercising the unmodified Mule v2.3.2 flush service without Redis/SQL.
namespace Mule
{
    internal enum DurableActionStatus { Pending, Locked, Completed, Failed, Cancelled }
    internal sealed class DurableAction
    {
        public Guid Id { get; set; }
        public DurableActionStatus Status { get; set; }
        public DateTimeOffset? CompletedOnUtc { get; set; }
        public DateTimeOffset? TerminalOnUtc { get; set; }
        public DateTimeOffset? CancelledOnUtc { get; set; }
        public string LastError { get; set; }
    }
    internal sealed record MuleCompletedAction(Guid Id, DateTimeOffset CompletedOnUtc);
    internal interface IMuleDurableStorage
    {
        Task AddRangeAsync(IReadOnlyCollection<DurableAction> actions, CancellationToken token);
        Task SaveChangesAsync(CancellationToken token);
        Task MarkCompletedAsync(Guid id, DateTimeOffset completed, CancellationToken token);
        Task MarkFailedAsync(Guid id, string error, DateTimeOffset now, DateTimeOffset? next, CancellationToken token);
    }
    internal interface IMuleBatchTerminalStorage { Task MarkCompletedRangeAsync(IReadOnlyCollection<MuleCompletedAction> actions, CancellationToken token); }
    internal interface IMuleTerminalCancellationStorage { Task<bool> MarkCancelledAsync(Guid id, DateTimeOffset now, CancellationToken token); }
}
namespace Mule.FastLane.Redis
{
    internal sealed class FastLaneRedisOptions
    {
        public int IntentFlushSize { get; set; } = 1000;
        public int CompletionFlushSize { get; set; } = 2000;
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(25);
    }
    internal sealed class RedisFastLaneBuffer
    {
        public IReadOnlyCollection<Mule.DurableAction> Terminals { get; set; } = [];
        public int Requeues;
        public Task<IReadOnlyCollection<Mule.DurableAction>> TakeIntentFlushBatchAsync(int count) => Task.FromResult<IReadOnlyCollection<Mule.DurableAction>>([]);
        public Task CompleteIntentFlushAsync(IReadOnlyCollection<Mule.DurableAction> actions) => Task.CompletedTask;
        public Task CancelIntentFlushAsync(Guid[] ids) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Mule.DurableAction>> TakeTerminalFlushBatchAsync(int count) => Task.FromResult(Terminals);
        public Task CompleteTerminalFlushAsync(IReadOnlyCollection<Mule.DurableAction> actions) { Terminals = []; return Task.CompletedTask; }
        public Task CancelTerminalFlushAsync(Guid[] ids) { Requeues++; return Task.CompletedTask; }
    }
    internal sealed class DurableStoreProbe { public Guid ExistingId; public int Committed; }
    internal sealed class TestDurableStorage(DurableStoreProbe store) : Mule.IMuleDurableStorage, Mule.IMuleBatchTerminalStorage
    {
        private int pending;
        public Task AddRangeAsync(IReadOnlyCollection<Mule.DurableAction> actions, CancellationToken token) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken token) { store.Committed += pending; pending = 0; return Task.CompletedTask; }
        public Task MarkCompletedAsync(Guid id, DateTimeOffset completed, CancellationToken token)
        {
            if (id != store.ExistingId) throw new InvalidOperationException($"Mule action '{id}' was not found.");
            pending++; return Task.CompletedTask;
        }
        public async Task MarkCompletedRangeAsync(IReadOnlyCollection<Mule.MuleCompletedAction> actions, CancellationToken token)
        { foreach (var action in actions) await MarkCompletedAsync(action.Id, action.CompletedOnUtc, token); }
        public Task MarkFailedAsync(Guid id, string error, DateTimeOffset now, DateTimeOffset? next, CancellationToken token) => Task.CompletedTask;
    }
}
