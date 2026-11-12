namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

internal sealed class DefaultOrchestrationReconciliationService(
    IOrchestrationInstanceRepository instances,
    IStageExecutionRepository stages,
    ITaskExecutionRepository tasks,
    ITaskExecutionAttemptRepository attempts,
    ITaskDispatchRepository dispatches,
    IRuntimeStorageUnitOfWork unitOfWork,
    ICompensationExecutionRepository compensations,
    IExecutionTransitionRepository transitions,
    IOrchestrationInstanceCoordinator coordinator,
    IOrchestrationDispatchRecoveryCoordinator dispatchRecovery,
    IOrchestrationDispatchCancellationCoordinator cancellationCoordinator,
    IOrchestrationDispatchCancellationRecoveryCoordinator cancellations,
    IOrchestrationCompensationRecoveryCoordinator compensationRecovery,
    ITaskResponseTimeoutDeadlineResolver deadlineResolver,
    ITaskResponseTimeoutScheduler scheduler,
    IOrchestrationTimeoutProcessor timeoutProcessor,
    IOrchestrationDurableWorkInspector workInspector,
    ISagaEngine engine,
    ILogger<DefaultOrchestrationReconciliationService> logger) : IOrchestrationReconciliationService
{
    public async Task<OrchestrationReconciliationResult> ReconcileAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (!Ulid.TryParse(instanceId, out var parsed))
            return new("Invalid", [new("Intervention", "Invalid instance identifier.")]);
        var id = new Id(parsed);
        OrchestrationReconciliationResult result = null;
        var acquired = await coordinator.TryExecuteAsync(id, async token =>
        {
            try { result = await ReconcileCoreAsync(id, token); }
            catch (KeyNotFoundException) { result = new("NotFound", [new("Intervention", "The instance or a required execution record was not found.")]); }
        }, cancellationToken, waitForLease: false);
        return acquired ? result : new("Busy", [new("Busy", "The saga is being processed by another runtime.")]);
    }

    private async Task<OrchestrationReconciliationResult> ReconcileCoreAsync(Id id, CancellationToken cancellationToken)
    {
        var instance = await instances.GetById(id, cancellationToken);
        if (instance is null)
            return new("NotFound", [new("Intervention", "The saga instance was not found.")]);
        if (instance.Status is not (OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.Compensating))
            return new("NotEligible", [new("Unchanged", $"Saga status {instance.Status} is not active.")]);

        var findings = new List<OrchestrationReconciliationFinding>();
        await RecordAsync(instance, "InstanceReconciliationRequested", "Operator requested reconciliation.", cancellationToken);
        var initialStages = await stages.GetByInstanceId(id, cancellationToken);
        if (initialStages.GroupBy(stage => stage.StageKey, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            var finding = new OrchestrationReconciliationFinding("Intervention", "Multiple stage executions have the same key; progression requires manual investigation.");
            await RecordAsync(instance, "InstanceReconciliationCompleted", finding.Message, cancellationToken,
                JsonSerializer.SerializeToNode(new[] { finding }));
            return new("Intervention", [finding]);
        }
        if (instance.DispatchCancellationPending)
        {
            if (instance.Status is OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.Compensating)
            {
                await cancellationCoordinator.CancelPendingAsync(instance, "Reconciled pending saga dispatch cancellations.", cancellationToken);
                findings.Add(new("Recovered", "Pending dispatch cancellation intents were processed."));
            }
            else
            {
                var finding = new OrchestrationReconciliationFinding("Intervention", "The running saga retains a pending aggregate cancellation intent; work was not resumed.");
                await RecordAsync(instance, "InstanceReconciliationCompleted", finding.Message, cancellationToken,
                    JsonSerializer.SerializeToNode(new[] { finding }));
                return new("Intervention", [finding]);
            }
        }
        var instanceTasks = await tasks.GetByInstanceId(id, cancellationToken);
        var cancelledTasks = instanceTasks.Where(task => task.Status == TaskExecutionStatus.Cancelled).ToArray();
        var preparedCancelled = false;
        if (cancelledTasks.Length > 0 && instance.Status is (OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Idle))
        {
            var activeStage = initialStages.FirstOrDefault(stage => stage.Status == StageExecutionStatus.Running &&
                string.Equals(stage.StageKey, instance.CurrentStageKey, StringComparison.OrdinalIgnoreCase));
            if (activeStage is null || cancelledTasks.Any(task => !task.ParallelGroupId.HasValue || task.StageExecutionId != activeStage.Id))
                findings.Add(new("Intervention", "Cancelled tasks do not belong to the current running parallel stage; inspect them before resuming."));
            else
            {
                var result = await ParallelCancellationRecovery.PrepareAsync(instance, cancelledTasks, tasks, attempts,
                    dispatches, unitOfWork, cancellationCoordinator, cancellationToken);
                if (result is not null) findings.Add(new("Intervention", result.Message));
                else preparedCancelled = true;
            }
        }
        foreach (var candidate in instanceTasks)
        {
            try
            {
                // A preceding repair may have advanced the saga or changed parallel tasks.
                instance = await instances.GetById(id, cancellationToken);
                if (instance.Status is not (OrchestrationInstanceStatus.Running or OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.Compensating))
                {
                    findings.Add(new("Unchanged", $"Saga is now {instance.Status}; remaining active work was not modified."));
                    break;
                }
                var task = await tasks.GetById(candidate.Id, cancellationToken);
                var instanceStages = await stages.GetByInstanceId(id, cancellationToken);
                if (task.OrchestrationInstanceId != id ||
                    !instanceStages.Any(stage => stage.Id == task.StageExecutionId && stage.OrchestrationInstanceId == id))
                {
                    findings.Add(new("Intervention", $"Task {task.TaskKey} has no matching stage in this saga."));
                    continue;
                }
                if (instanceStages.GroupBy(stage => stage.StageKey, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                {
                    findings.Add(new("Intervention", "Multiple stage executions have the same key; progression requires manual investigation."));
                    break;
                }
                var taskAttempts = await attempts.GetByTaskExecutionId(task.Id, cancellationToken);
                var attempt = taskAttempts.OrderByDescending(x => x.AttemptNumber).FirstOrDefault();
                foreach (var priorAttempt in taskAttempts)
                {
                    if (priorAttempt.DispatchId is not { } cancellationDispatchId) continue;
                    var pendingDispatch = await dispatches.TryGetById(cancellationDispatchId, cancellationToken);
                    if (pendingDispatch?.PhysicalCancellationPending != true) continue;
                    var attempted = await cancellations.ReconcileAsync(pendingDispatch.Id, cancellationToken);
                    pendingDispatch = await dispatches.TryGetById(pendingDispatch.Id, cancellationToken);
                    var cancelled = attempted && pendingDispatch is not null && !pendingDispatch.PhysicalCancellationPending;
                    findings.Add(new(cancelled ? "Recovered" : "Intervention", $"Cancellation for {task.TaskKey} attempt {priorAttempt.AttemptNumber}: {(cancelled ? "reconciled" : "still pending")}.") );
                }
                TaskDispatch dispatch = null;
                if (attempt?.DispatchId is { } dispatchId)
                    dispatch = await dispatches.TryGetById(dispatchId, cancellationToken);

                if (task.Status == TaskExecutionStatus.Pending && attempt is not null)
                {
                    findings.Add(new("Intervention", $"Pending task {task.TaskKey} already has an execution attempt; verify its delivery before advancing."));
                    continue;
                }

                if (task.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying)
                {
                    var owner = instanceStages.FirstOrDefault(stage => stage.Id == task.StageExecutionId);
                    if (owner is null || owner.OrchestrationInstanceId != id || owner.Status != StageExecutionStatus.Running ||
                        !string.Equals(owner.StageKey, instance.CurrentStageKey, StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new("Intervention", $"Active task {task.TaskKey} does not belong to the current running stage."));
                        continue;
                    }
                }

                if (task.Status == TaskExecutionStatus.WaitingResponse)
                {
                    if (attempt is null || attempt.TaskExecutionId != task.Id ||
                        (task.LastAttemptNumber > 0 && task.LastAttemptNumber != attempt.AttemptNumber) ||
                        attempt.Status != TaskExecutionStatus.WaitingResponse || dispatch?.SentOnUtc is null ||
                        dispatch.TaskExecutionAttemptId != attempt.Id || task.WaitingSinceUtc is null ||
                        !string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new("Intervention", $"Task {task.TaskKey} has inconsistent response-wait tracking."));
                        continue;
                    }
                    var due = task.ResponseDeadlineUtc ?? await deadlineResolver.ResolveAsync(id, task.Id, task.WaitingSinceUtc.Value, cancellationToken);
                    if (due is null)
                    {
                        findings.Add(new("Waiting", $"Task {task.TaskKey} is awaiting a response and has no timeout policy."));
                        continue;
                    }
                    if (task.ResponseDeadlineUtc is null)
                    {
                        task.ResponseDeadlineUtc = due;
                        await tasks.Update(task, cancellationToken);
                    }
                    if (due <= DateTime.UtcNow)
                    {
                        var timedOut = await timeoutProcessor.ProcessTaskTimeoutAsync(task.Id, DateTime.UtcNow, cancellationToken);
                        if (timedOut.RescheduleOnUtc is { } nextDue && scheduler.UsesDurableScheduling)
                        {
                            await scheduler.ScheduleAsync(new TaskResponseTimeoutScheduleRequest
                            {
                                OrchestrationInstanceId = id, TaskExecutionId = task.Id, TaskExecutionAttemptId = attempt.Id,
                                DispatchId = dispatch.Id, AttemptNumber = attempt.AttemptNumber, DueOnUtc = nextDue
                            }, cancellationToken);
                        }
                        findings.Add(new(timedOut.Applied ? "Recovered" : "Intervention", $"Timeout for {task.TaskKey}: {(timedOut.Applied ? "processed" : "not applied")}.") );
                    }
                    else if (scheduler.UsesDurableScheduling)
                    {
                        var timerState = await workInspector.InspectTimeoutAsync(task, attempt, cancellationToken);
                        if (timerState is DurableWorkPresence.Missing or DurableWorkPresence.Terminal ||
                            (string.IsNullOrWhiteSpace(task.TimeoutActionId) && timerState == DurableWorkPresence.Present))
                        {
                            var previousActionId = task.TimeoutActionId;
                            if (timerState == DurableWorkPresence.Terminal && string.IsNullOrWhiteSpace(previousActionId))
                                previousActionId = await workInspector.FindTimeoutActionIdAsync(task, attempt, cancellationToken);
                            if (timerState == DurableWorkPresence.Terminal && string.IsNullOrWhiteSpace(previousActionId))
                            {
                                findings.Add(new("Intervention", $"Terminal timer for {task.TaskKey} has no recoverable action reference."));
                                continue;
                            }
                            await scheduler.ScheduleAsync(new TaskResponseTimeoutScheduleRequest
                            {
                                OrchestrationInstanceId = id, TaskExecutionId = task.Id, TaskExecutionAttemptId = attempt.Id,
                                DispatchId = dispatch.Id, AttemptNumber = attempt.AttemptNumber, DueOnUtc = due.Value,
                                RecoveryOfActionId = timerState == DurableWorkPresence.Terminal ? previousActionId : null
                            }, cancellationToken);
                            findings.Add(new("Recovered", $"Restored timeout scheduling for {task.TaskKey}."));
                        }
                        else if (timerState == DurableWorkPresence.Present)
                            findings.Add(new("Waiting", $"Task {task.TaskKey} is awaiting a response until {due:O}."));
                        else
                            findings.Add(new("Intervention", $"Task {task.TaskKey} is awaiting a response, but its timer state could not be verified."));
                    }
                    else findings.Add(new("Waiting", $"Task {task.TaskKey} is awaiting a response until {due:O}."));
                    continue;
                }

                if (task.Status is TaskExecutionStatus.Running or TaskExecutionStatus.Retrying)
                {
                    if (attempt is null || attempt.TaskExecutionId != task.Id ||
                        (task.LastAttemptNumber > 0 && task.LastAttemptNumber != attempt.AttemptNumber) ||
                        (dispatch is not null && dispatch.TaskExecutionAttemptId != attempt.Id))
                    {
                        findings.Add(new("Intervention", $"Task {task.TaskKey} has inconsistent attempt or dispatch tracking."));
                        continue;
                    }
                    if (dispatch is not null && string.Equals(dispatch.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase))
                    {
                        var presence = await workInspector.InspectDispatchAsync(dispatch, cancellationToken);
                        if (presence != DurableWorkPresence.Present)
                        {
                            findings.Add(new("Intervention", $"Pending dispatch for {task.TaskKey} has durable action state {presence}; delivery is uncertain and was not replayed."));
                            continue;
                        }
                        var handled = await dispatchRecovery.RecoverDispatchAsync(dispatch.Id, cancellationToken);
                        var currentDispatch = await dispatches.TryGetById(dispatch.Id, cancellationToken);
                        var accepted = handled && currentDispatch is not null &&
                            string.Equals(currentDispatch.DispatchStatus, "Enqueued", StringComparison.OrdinalIgnoreCase);
                        findings.Add(new(accepted ? "Recovered" : "Intervention", $"Pending dispatch for {task.TaskKey}: {(accepted ? "accepted by the provider" : "delivery remains uncertain or recovery failed; inspect the dispatch history")}.") );
                    }
                    else
                    {
                        var presence = dispatch is null ? DurableWorkPresence.Missing : await workInspector.InspectDispatchAsync(dispatch, cancellationToken);
                        var canStillRun = presence == DurableWorkPresence.Present && dispatch is not null &&
                            (string.Equals(dispatch.DispatchStatus, "Enqueued", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(dispatch.DispatchStatus, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(dispatch.DispatchStatus, "RetryPending", StringComparison.OrdinalIgnoreCase));
                        findings.Add(canStillRun
                            ? new("Waiting", $"Task {task.TaskKey} has an active durable delivery ({dispatch.DispatchStatus}).")
                            : new("Intervention", $"Task {task.TaskKey} is active; delivery status '{dispatch?.DispatchStatus ?? "missing"}', durable action {presence}. Verify delivery before replay."));
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not reconcile task {TaskId} of instance {InstanceId}", candidate.Id, id);
                findings.Add(new("Intervention", $"Task {candidate.TaskKey} could not be reconciled; consult runtime logs."));
            }
        }

        if (instance.Status == OrchestrationInstanceStatus.Compensating)
        {
            foreach (var compensation in await compensations.GetByInstanceId(id, cancellationToken))
            {
                try
                {
                    if (!string.Equals(compensation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(compensation.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(compensation.Status, "Skipped", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(compensation.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                            findings.Add(new("Intervention", $"Compensation {compensation.Id} has an unrecognized or active status '{compensation.Status}'."));
                        continue;
                    }
                    var presence = await workInspector.InspectCompensationAsync(compensation, cancellationToken);
                    if (presence != DurableWorkPresence.Present)
                    {
                        findings.Add(new("Intervention", $"Compensation {compensation.Id} has durable action state {presence}; delivery is uncertain and was not replayed."));
                        continue;
                    }
                    var handled = await compensationRecovery.RecoverCompensationAsync(compensation.Id, cancellationToken);
                    var current = await compensations.TryGetById(compensation.Id, cancellationToken);
                    var completed = handled && string.Equals(current?.Status, "Completed", StringComparison.OrdinalIgnoreCase);
                    findings.Add(new(completed ? "Recovered" : "Intervention", $"Compensation {compensation.Id}: {(completed ? "delivery recovered" : "delivery remains pending or failed; inspect its history")}.") );
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Could not reconcile compensation {CompensationId} of instance {InstanceId}", compensation.Id, id);
                    findings.Add(new("Intervention", $"Compensation {compensation.Id} could not be reconciled; consult runtime logs."));
                }
            }
        }

        // Re-read after repairs: a timeout can itself advance the saga. Never advance past active work.
        instance = await instances.GetById(id, cancellationToken);
        instanceTasks = await tasks.GetByInstanceId(id, cancellationToken);
        if ((instance.Status == OrchestrationInstanceStatus.Running || (instance.Status == OrchestrationInstanceStatus.Idle && preparedCancelled)) &&
            (preparedCancelled || !instanceTasks.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying)) &&
            !findings.Any(x => x.Status == "Intervention"))
        {
            await engine.OrchestrateAsync(new ForwardIntent
            {
                ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
                MessageMetadata = new OrchestrationMessageMetadata
                {
                    SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                    OrchestrationInstanceId = instance.Id.ToString(), CurrentStage = instance.CurrentStageKey,
                    CurrentTasks = string.IsNullOrWhiteSpace(instance.CurrentTaskKey) ? [] : [instance.CurrentTaskKey],
                    CorrelationId = instance.CorrelationId
                },
                Payload = instance.SnapshotPayload?.DeepClone()
            }, cancellationToken);
            if (preparedCancelled)
            {
                var currentTasks = await tasks.GetByInstanceId(id, cancellationToken);
                var cannotResume = currentTasks.Any(task => task.Status == TaskExecutionStatus.Cancelled &&
                    task.Metadata is not null && task.Metadata.TryGetValue(ParallelCancellationRecovery.ResumeMetadataKey, out var marker) &&
                    marker is JsonValue value && value.TryGetValue<bool>(out var marked) && marked &&
                    !currentTasks.Any(other => other.ParallelGroupId == task.ParallelGroupId &&
                        other.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying));
                findings.Add(cannotResume
                    ? new("Intervention", "A prepared parallel task could not resume; verify its artifact and execution state.")
                    : new("Recovered", "The orchestration engine resumed or scheduled the recoverable parallel tasks."));
            }
            else findings.Add(new("Recovered", "The orchestration engine re-evaluated the persisted execution state."));
        }
        else if (instance.Status == OrchestrationInstanceStatus.Compensating &&
            !findings.Any(x => x.Status == "Intervention") &&
            !(await compensations.GetByInstanceId(id, cancellationToken)).Any(x =>
                string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
        {
            await engine.OrchestrateAsync(new ForwardIntent
            {
                ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
                MessageMetadata = new OrchestrationMessageMetadata
                {
                    SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
                    OrchestrationInstanceId = id.ToString(), CurrentStage = instance.CurrentStageKey,
                    CorrelationId = instance.CorrelationId
                },
                Payload = instance.SnapshotPayload?.DeepClone()
            }, cancellationToken);
            findings.Add(new("Recovered", "The orchestration engine resumed compensation."));
        }
        if (findings.Count == 0) findings.Add(new("Unchanged", "No recoverable work was found."));
        var status = findings.Any(x => x.Status == "Intervention") ? "Intervention" :
            findings.Any(x => x.Status == "Recovered") ? "Recovered" : "Waiting";
        var summary = string.Join(" ", findings.Select(x => x.Message));
        instance = await instances.GetById(id, cancellationToken);
        await RecordAsync(instance, "InstanceReconciliationCompleted", summary.Length <= 4000 ? summary : summary[..3997] + "...", cancellationToken,
            JsonSerializer.SerializeToNode(findings));
        return new(status, findings);
    }

    private Task RecordAsync(OrchestrationInstance instance, string type, string message, CancellationToken cancellationToken, JsonNode payload = null)
        => transitions.Create(new ExecutionTransition
        {
            Id = Id.New(), OrchestrationInstanceId = instance.Id, TransitionType = type,
            FromStatus = instance.Status.ToString(), ToStatus = instance.Status.ToString(),
            OccurredOnUtc = DateTime.UtcNow, Message = message, Payload = payload,
            ProducedBy = nameof(DefaultOrchestrationReconciliationService)
        }, cancellationToken);
}
