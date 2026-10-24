namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal sealed class UnsupportedTaskDispatchReplayService : ITaskDispatchReplayService
{
    public Task<TaskDispatchReplayResult> ReplayAsync(
        TaskDispatchReplayRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(TaskDispatchReplayResult.Rejected(
            "Unsupported",
            "The configured runtime delivery provider does not support replaying durable dispatches."));
}
