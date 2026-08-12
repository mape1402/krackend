using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Default pending work processor.
/// </summary>
public sealed class RuntimePendingWorkProcessor : IRuntimePendingWorkProcessor
{
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ICompensationExecutionRepository _compensationRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimePendingWorkProcessor"/> class.
    /// </summary>
    public RuntimePendingWorkProcessor(
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ICompensationExecutionRepository compensationRepository)
    {
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _compensationRepository = compensationRepository;
    }

    /// <inheritdoc/>
    public async Task<RuntimePendingWorkResult> ProcessDueWork(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var waitingTasks = await _taskRepository.GetWaitingResponseOlderThan(nowUtc, cancellationToken);
        var waitingAttempts = await _attemptRepository.GetWaitingResponseOlderThan(nowUtc, cancellationToken);
        var compensations = await _compensationRepository.GetPending(cancellationToken);

        var items = waitingTasks
            .Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.WaitingTaskTimeout,
                x.Id,
                x.OrchestrationInstanceId,
                x.Id,
                x.WaitingSinceUtc,
                x.Status.ToString()))
            .Concat(waitingAttempts.Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.WaitingAttemptTimeout,
                x.Id,
                null,
                x.TaskExecutionId,
                x.WaitingSinceUtc,
                x.Status.ToString())))
            .Concat(compensations.Select(x => new RuntimePendingWorkItem(
                RuntimePendingWorkTypes.PendingCompensation,
                x.Id,
                x.OrchestrationInstanceId,
                x.SourceTaskExecutionId,
                x.StartedOnUtc,
                x.Status)))
            .OrderBy(x => x.DueOnUtc ?? DateTime.MaxValue)
            .ToArray();

        return new RuntimePendingWorkResult(nowUtc, items);
    }
}
