// Controlled dependencies for the unmodified Mule 2.3.2 executor and real Krackend coordinator.
namespace Mule
{
    internal readonly record struct Ulid(Guid Value);
    internal sealed class DurableAction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; }
        public string Lane { get; set; }
        public string Payload { get; set; }
        public int Attempts { get; set; }
    }
    internal interface IMuleStorage
    {
        Task MarkCompletedAsync(Guid id, DateTimeOffset completed, CancellationToken token);
        Task MarkFailedAsync(Guid id, string error, DateTimeOffset now, DateTimeOffset? next, CancellationToken token);
        Task SaveChangesAsync(CancellationToken token);
    }
    public enum MuleRecoveryMode { Polling, Scheduled }
    public enum MuleCleanupMode { Polling, Scheduled, Disabled }
    public enum MuleRetryBackoff { Fixed, Linear, Exponential }
    public sealed class MuleReplaySettings { }
    public sealed class MuleLaneSettings
    {
        public int Priority { get; set; }
        public int Weight { get; set; }
        public int? DispatchQueueCapacity { get; set; }
        public int? MaxDegreeOfParallelism { get; set; }
        public int? ExecutionQueueCapacity { get; set; }
        public MuleRetryPolicy RetryPolicy { get; set; }
        public int MaxAttempts { get; set; }
        public TimeSpan RetryDelay { get; set; }
    }
}
namespace Mule.Dispatching
{
    internal interface IMuleDispatchQueue
    { ValueTask EnqueueAsync(Mule.Ulid actionId, string lane, CancellationToken cancellationToken = default); }
    internal interface IMuleCommitNotifier
    { ValueTask NotifySavedAsync(Mule.Ulid actionId, string lane, CancellationToken cancellationToken = default); }
    internal sealed record MuleDispatchItem(Mule.Ulid ActionId, string Lane);
    internal interface IMuleActionExecutor { }
    internal sealed class MuleSchedulerSignal
    {
        public void SignalRecovery() { }
        public void SignalRecoveryAt(DateTimeOffset time) { }
        public void SignalCleanupAt(DateTimeOffset time) { }
    }
    internal interface IMuleActionHandler
    { Task ExecuteAsync(Mule.DurableAction action, IServiceProvider services, CancellationToken token); }
    internal sealed class MuleActionRegistry(IMuleActionHandler handler)
    {
        public bool TryGetHandler(string key, out IMuleActionHandler result) { result = handler; return true; }
    }
}
namespace Mule.Diagnostics
{
    internal sealed class MuleRuntimeMetrics
    {
        public void RecordExecutorSaturation(string lane) { }
        public void RecordWaitingExecution(string lane, DateTimeOffset at) { }
        public void RecordWaitingExecutionCanceled(string lane) { }
        public void RecordExecutionStarted(string lane, DateTimeOffset queued, DateTimeOffset at) { }
        public void RecordExecutionStopped(string lane) { }
        public void RecordCompleted(string lane, string key, DateTimeOffset at) { }
        public void RecordFailed(string lane, string key) { }
    }
}
