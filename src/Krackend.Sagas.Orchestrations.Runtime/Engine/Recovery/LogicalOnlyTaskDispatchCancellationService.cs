namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

internal sealed class LogicalOnlyTaskDispatchCancellationService : ITaskDispatchCancellationService
{
    public Task<TaskDispatchCancellationResult> CancelAsync(
        TaskDispatchCancellationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(TaskDispatchCancellationResult.LogicalOnly(
            "LogicalOnly",
            "The runtime fenced the durable dispatch; the configured delivery provider has no physical cancellation capability."));
}
