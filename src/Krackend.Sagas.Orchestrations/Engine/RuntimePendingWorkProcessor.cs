using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Default pending work processor.
/// </summary>
public sealed class RuntimePendingWorkProcessor : IRuntimePendingWorkProcessor
{
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeTimeoutPolicyEvaluator _timeoutPolicyEvaluator;
    private readonly IRuntimeErrorPolicyResolver _errorPolicyResolver;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;
    private readonly IRuntimeCompensationPlanBuilder _compensationPlanBuilder = new RuntimeCompensationPlanBuilder();

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimePendingWorkProcessor"/> class.
    /// </summary>
    public RuntimePendingWorkProcessor(
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ICompensationExecutionRepository compensationRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        IRuntimeArtifactRepository artifactRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeTimeoutPolicyEvaluator timeoutPolicyEvaluator,
        IRuntimeErrorPolicyResolver errorPolicyResolver,
        IRuntimeReactiveEventPublisher reactiveEventPublisher)
    {
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _compensationRepository = compensationRepository;
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _artifactRepository = artifactRepository;
        _transitionRepository = transitionRepository;
        _timeoutPolicyEvaluator = timeoutPolicyEvaluator;
        _errorPolicyResolver = errorPolicyResolver;
        _reactiveEventPublisher = reactiveEventPublisher;
    }

    /// <inheritdoc/>
    public async Task<RuntimePendingWorkResult> ProcessDueWork(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var waitingTasks = await _taskRepository.GetWaitingResponseOlderThan(nowUtc, cancellationToken);
        var waitingAttempts = await _attemptRepository.GetWaitingResponseOlderThan(nowUtc, cancellationToken);
        var compensations = await _compensationRepository.GetPending(cancellationToken);

        foreach (var task in waitingTasks)
        {
            await TryApplyTimeout(task, nowUtc, cancellationToken);
        }

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

    private async Task<bool> TryApplyTimeout(TaskExecution taskExecution, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetById(taskExecution.OrchestrationInstanceId, cancellationToken);
        if (instance.Status != OrchestrationInstanceStatus.Waiting ||
            taskExecution.Status != TaskExecutionStatus.WaitingResponse)
            return false;

        var stageExecution = await _stageRepository.GetById(taskExecution.StageExecutionId, cancellationToken);
        var artifact = await _artifactRepository.GetById(instance.RuntimeOrchestrationArtifactId, cancellationToken);
        var document = RuntimeArtifactDocument.Parse(artifact.ArtifactPayload, artifact.Version.ToString());
        var taskDocument = FindTaskDocument(document, stageExecution.StageKey, taskExecution.TaskKey);
        var timeoutPolicy = _timeoutPolicyEvaluator.Evaluate(taskDocument.TimeoutPolicy);
        if (!timeoutPolicy.IsConfigured)
            return false;

        var attempts = await _attemptRepository.GetByTaskExecutionId(taskExecution.Id, cancellationToken);
        var attempt = attempts
            .Where(x => x.Status == TaskExecutionStatus.WaitingResponse)
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefault();

        if (string.Equals(timeoutPolicy.Behavior, nameof(TimeoutBehavior.Wait), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(timeoutPolicy.OrchestrationAction, nameof(OrchestrationActionOnTimeout.Block), StringComparison.OrdinalIgnoreCase))
        {
            if (attempt is not null)
            {
                attempt.TimedOutOnUtc = nowUtc;
                attempt.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
                attempt.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
                await _attemptRepository.Update(attempt, cancellationToken);
                await WriteTransition(RuntimeTransition.ForAttempt(
                    instance,
                    "TaskTimedOut",
                    TaskExecutionStatus.WaitingResponse,
                    attempt.Status,
                    stageExecution,
                    taskExecution,
                    attempt), cancellationToken);
            }

            taskExecution.TimedOutOnUtc = nowUtc;
            taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
            taskExecution.Metadata["timeoutAction"] = timeoutPolicy.OrchestrationAction;
            taskExecution.Metadata["timeoutPolicyApplied"] = "Blocked";
            await _taskRepository.Update(taskExecution, cancellationToken);
            await WriteTransition(RuntimeTransition.ForTask(
                instance,
                "TaskTimeoutPolicyApplied",
                TaskExecutionStatus.WaitingResponse,
                taskExecution.Status,
                stageExecution,
                taskExecution), cancellationToken);
            return true;
        }

        if (!string.Equals(timeoutPolicy.Behavior, nameof(TimeoutBehavior.Fail), StringComparison.OrdinalIgnoreCase))
            return false;

        if (attempt is not null)
        {
            attempt.Status = TaskExecutionStatus.TimedOut;
            attempt.TimedOutOnUtc = nowUtc;
            attempt.ErrorCode = string.IsNullOrWhiteSpace(timeoutPolicy.ErrorCode) ? "TaskTimeout" : timeoutPolicy.ErrorCode;
            attempt.ErrorMessage = $"Task '{taskExecution.TaskKey}' timed out after {timeoutPolicy.Timeout}.";
            await _attemptRepository.Update(attempt, cancellationToken);
            await WriteTransition(RuntimeTransition.ForAttempt(
                instance,
                "TaskTimedOut",
                TaskExecutionStatus.WaitingResponse,
                attempt.Status,
                stageExecution,
                taskExecution,
                attempt), cancellationToken);
        }

        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.TimedOutOnUtc = nowUtc;
        taskExecution.WaitingSinceUtc = null;
        taskExecution.FailedOnUtc = nowUtc;
        taskExecution.Metadata["timeoutBehavior"] = timeoutPolicy.Behavior;
        taskExecution.Metadata["timeoutErrorCode"] = string.IsNullOrWhiteSpace(timeoutPolicy.ErrorCode) ? "TaskTimeout" : timeoutPolicy.ErrorCode;
        await _taskRepository.Update(taskExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(
            instance,
            "TaskTimeoutPolicyApplied",
            TaskExecutionStatus.WaitingResponse,
            taskExecution.Status,
            stageExecution,
            taskExecution), cancellationToken);

        await ApplyTimeoutErrorPolicy(document, instance, stageExecution, taskExecution, cancellationToken);
        return true;
    }

    private async Task ApplyTimeoutErrorPolicy(
        RuntimeArtifactDocument document,
        OrchestrationInstance instance,
        StageExecution stageExecution,
        TaskExecution taskExecution,
        CancellationToken cancellationToken)
    {
        var decision = _errorPolicyResolver.Resolve(taskExecution.OnErrorPolicy);
        taskExecution.Metadata["errorPolicyApplied"] = decision.Policy.ToString();
        taskExecution.Metadata["errorPolicyAction"] = decision.Action.ToString();

        if (decision.Action == RuntimeErrorPolicyAction.Continue)
        {
            taskExecution.Status = TaskExecutionStatus.CompletedWithErrors;
            taskExecution.CompletedOnUtc = DateTime.UtcNow;
            await _taskRepository.Update(taskExecution, cancellationToken);
            await WriteTransition(RuntimeTransition.ForTask(instance, "TaskErrorPolicyApplied", TaskExecutionStatus.Failed, taskExecution.Status, stageExecution, taskExecution), cancellationToken);
            return;
        }

        await _taskRepository.Update(taskExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(instance, "TaskErrorPolicyApplied", TaskExecutionStatus.Failed, taskExecution.Status, stageExecution, taskExecution), cancellationToken);

        stageExecution.Status = StageExecutionStatus.Failed;
        stageExecution.FailedOnUtc = DateTime.UtcNow;
        stageExecution.ErrorSummary = taskExecution.ErrorSummary();
        await _stageRepository.Update(stageExecution, cancellationToken);

        if (decision.Action == RuntimeErrorPolicyAction.StartCompensation)
        {
            instance.Status = OrchestrationInstanceStatus.Compensating;
            instance.CompensationStartedOnUtc = DateTime.UtcNow;
            instance.WaitingSinceUtc = null;
            instance.LastUpdatedOnUtc = DateTime.UtcNow;
            instance.ErrorSummary = taskExecution.ErrorSummary();
            await _instanceRepository.Update(instance, cancellationToken);
            await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceCompensating", OrchestrationInstanceStatus.Waiting, instance.Status, stageExecution), cancellationToken);

            var completedTasks = await _taskRepository.GetByInstanceId(instance.Id, cancellationToken);
            var existingCompensations = await _compensationRepository.GetByInstanceId(instance.Id, cancellationToken);
            var existingSourceTaskIds = existingCompensations.Select(x => x.SourceTaskExecutionId).ToHashSet();
            var plan = _compensationPlanBuilder.Build(document, completedTasks)
                .Where(x => !existingSourceTaskIds.Contains(x.SourceTaskExecutionId))
                .ToArray();

            foreach (var item in plan)
            {
                await _compensationRepository.Create(new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    SourceTaskExecutionId = item.SourceTaskExecutionId,
                    CompensationTaskKey = item.CompensationTaskKey,
                    Status = "Pending",
                    RequestPayload = item.RequestPayload?.DeepClone(),
                    Metadata = item.Metadata.ToDictionary(x => x.Key, x => x.Value?.DeepClone())
                }, cancellationToken);

                await WriteTransition(RuntimeTransition.ForInstancePayload(
                    instance,
                    "CompensationScheduled",
                    OrchestrationInstanceStatus.Compensating,
                    OrchestrationInstanceStatus.Compensating,
                    item.Metadata.ToJsonObject()), cancellationToken);
            }

            instance.Metadata["compensationPlanCount"] = plan.Length;
            await _instanceRepository.Update(instance, cancellationToken);
            return;
        }

        instance.Status = OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = DateTime.UtcNow;
        instance.WaitingSinceUtc = null;
        instance.ErrorSummary = stageExecution.ErrorSummary;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(instance, "StageFailed", StageExecutionStatus.Running, stageExecution.Status, stageExecution, taskExecution), cancellationToken);
        await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceFailed", OrchestrationInstanceStatus.Waiting, instance.Status, stageExecution), cancellationToken);
    }

    private static RuntimeTaskDocument FindTaskDocument(RuntimeArtifactDocument document, string stageKey, string taskKey)
    {
        var stage = document.Stages.FirstOrDefault(x => string.Equals(x.Key, stageKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Stage '{stageKey}' was not found in runtime artifact.");
        return stage.Tasks.FirstOrDefault(x => string.Equals(x.Key, taskKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Task '{taskKey}' was not found in stage '{stageKey}'.");
    }

    private async Task WriteTransition(RuntimeTransition transition, CancellationToken cancellationToken)
    {
        var executionTransition = new ExecutionTransition
        {
            Id = Id.New(),
            OrchestrationInstanceId = transition.Instance.Id,
            StageExecutionId = transition.StageExecution?.Id,
            TaskExecutionId = transition.TaskExecution?.Id,
            TaskExecutionAttemptId = transition.Attempt?.Id,
            TransitionType = transition.Type,
            FromStatus = transition.FromStatus,
            ToStatus = transition.ToStatus,
            OccurredOnUtc = DateTime.UtcNow,
            Message = transition.Type,
            Payload = transition.Payload?.DeepClone(),
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        };

        await _transitionRepository.Create(executionTransition, cancellationToken);
        try
        {
            await _reactiveEventPublisher.Publish(new RuntimeReactiveEvent
            {
                Id = executionTransition.Id,
                EventName = ResolveReactiveEventName(transition.Type),
                TransitionType = transition.Type,
                EnvironmentKey = transition.Instance.EnvironmentKey,
                OrchestrationDefinitionKey = transition.Instance.OrchestrationDefinitionKey,
                OrchestrationInstanceId = transition.Instance.Id,
                CorrelationId = transition.Instance.CorrelationId,
                ExecutionKey = transition.Instance.ExecutionKey,
                StageExecutionId = transition.StageExecution?.Id,
                StageKey = transition.StageExecution?.StageKey,
                TaskExecutionId = transition.TaskExecution?.Id,
                TaskKey = transition.TaskExecution?.TaskKey,
                TaskExecutionAttemptId = transition.Attempt?.Id,
                FromStatus = executionTransition.FromStatus,
                ToStatus = executionTransition.ToStatus,
                InstanceStatus = transition.Instance.Status.ToString(),
                OccurredOnUtc = executionTransition.OccurredOnUtc,
                Message = executionTransition.Message,
                Payload = executionTransition.Payload?.DeepClone(),
                ProducedBy = executionTransition.ProducedBy
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Pending work state is persisted; live observer outages must not fail recovery processing.
        }
    }

    private static string ResolveReactiveEventName(string transitionType)
        => transitionType switch
        {
            "InstanceCompensating" => RuntimeReactiveEventNames.OrchestrationCompensating,
            "InstanceFailed" => RuntimeReactiveEventNames.OrchestrationFailed,
            "StageFailed" => RuntimeReactiveEventNames.StageFailed,
            "TaskTimedOut" => RuntimeReactiveEventNames.TaskTimedOut,
            "TaskTimeoutPolicyApplied" => RuntimeReactiveEventNames.TaskTimeoutPolicyApplied,
            "TaskErrorPolicyApplied" => RuntimeReactiveEventNames.TaskErrorPolicyApplied,
            "CompensationScheduled" => RuntimeReactiveEventNames.CompensationScheduled,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };
}
