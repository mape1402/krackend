using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Executes runtime orchestration instances from intake items and correlated messaging responses.
/// </summary>
public sealed class RuntimeEngine : IRuntimeEngine
{
    private readonly ITriggerIntakeBuffer _intakeBuffer;
    private readonly ITriggerPromoter _triggerPromoter;
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IExecutionTransitionRepository _timelineRepository;
    private readonly IMessagingCommandDispatcher _messagingDispatcher;
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeEngine"/> class.
    /// </summary>
    /// <param name="dependencies">Runtime engine dependency set.</param>
    public RuntimeEngine(RuntimeEngineDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _intakeBuffer = dependencies.IntakeBuffer;
        _triggerPromoter = dependencies.TriggerPromoter;
        _artifactRepository = dependencies.ArtifactRepository;
        _stageRepository = dependencies.StageRepository;
        _taskRepository = dependencies.TaskRepository;
        _attemptRepository = dependencies.AttemptRepository;
        _dispatchRepository = dependencies.DispatchRepository;
        _instanceRepository = dependencies.InstanceRepository;
        _timelineRepository = dependencies.TimelineRepository;
        _messagingDispatcher = dependencies.MessagingDispatcher;
        _reactiveEventPublisher = dependencies.ReactiveEventPublisher;
    }

    /// <inheritdoc/>
    public async Task<RuntimeEngineProcessResult> ProcessNext(CancellationToken cancellationToken = default)
    {
        var lease = await _intakeBuffer.TryDequeue(cancellationToken);
        if (lease is null)
            return new RuntimeEngineProcessResult
            {
                Succeeded = true,
                Status = "Idle",
                Message = "No trigger intake items are pending."
            };

        try
        {
            var promotion = await _triggerPromoter.Promote(lease.Item, cancellationToken);
            await Execute(promotion, cancellationToken);
            await _intakeBuffer.MarkCompleted(lease.Item.BufferItemId, lease.LeaseId, cancellationToken);

            return new RuntimeEngineProcessResult
            {
                Succeeded = true,
                Status = promotion.Instance.Status.ToString(),
                Message = "Runtime trigger processed.",
                BufferItemId = lease.Item.BufferItemId.ToString(),
                IntakeId = promotion.Intake.Id.ToString(),
                InstanceId = promotion.Instance.Id.ToString()
            };
        }
        catch (Exception ex)
        {
            await _intakeBuffer.MarkFailed(lease.Item.BufferItemId, lease.LeaseId, ex.Message, cancellationToken);
            return new RuntimeEngineProcessResult
            {
                Succeeded = false,
                Status = "Failed",
                Message = ex.Message,
                BufferItemId = lease.Item.BufferItemId.ToString()
            };
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<RuntimeEngineProcessResult>> ProcessAll(int maxItems = 25, CancellationToken cancellationToken = default)
    {
        var results = new List<RuntimeEngineProcessResult>();
        var limit = Math.Clamp(maxItems, 1, 250);
        for (var i = 0; i < limit; i++)
        {
            var result = await ProcessNext(cancellationToken);
            if (result.Status == "Idle")
                break;

            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<RuntimeEngineProcessResult> ContinueFromResponse(RuntimeMessageResponseCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var instance = await _instanceRepository.GetById(ParseId(command.OrchestrationInstanceId, nameof(command.OrchestrationInstanceId)), cancellationToken);
        var taskExecution = await _taskRepository.GetById(ParseId(command.TaskExecutionId, nameof(command.TaskExecutionId)), cancellationToken);
        var stageExecution = await _stageRepository.GetById(taskExecution.StageExecutionId, cancellationToken);
        var attempt = await ResolveResponseAttempt(taskExecution, command, cancellationToken);

        if (!CanContinueFromResponse(instance, taskExecution, attempt))
            return DuplicateResponseIgnored(instance);

        await CompleteResponse(instance, stageExecution, taskExecution, attempt, command, cancellationToken);
        await ContinueAfterResponse(instance, stageExecution, taskExecution, cancellationToken);

        return new RuntimeEngineProcessResult
        {
            Succeeded = true,
            Status = instance.Status.ToString(),
            Message = "Runtime response processed.",
            InstanceId = instance.Id.ToString()
        };
    }

    private static bool CanContinueFromResponse(
        OrchestrationInstance instance,
        TaskExecution taskExecution,
        TaskExecutionAttempt attempt)
        => instance.Status == OrchestrationInstanceStatus.Waiting
            && taskExecution.Status == TaskExecutionStatus.WaitingResponse
            && attempt.Status == TaskExecutionStatus.WaitingResponse;

    private static RuntimeEngineProcessResult DuplicateResponseIgnored(OrchestrationInstance instance)
        => new()
        {
            Succeeded = true,
            Status = instance.Status.ToString(),
            Message = "Runtime response ignored because the correlated task is no longer waiting.",
            InstanceId = instance.Id.ToString()
        };

    private async Task Execute(TriggerPromotionResult promotion, CancellationToken cancellationToken)
    {
        var document = RuntimeArtifactDocument.Parse(
            promotion.Artifact.ArtifactPayload,
            promotion.Artifact.Version.ToString());
        var instance = promotion.Instance;
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstance(instance, "InstanceStarted", OrchestrationInstanceStatus.Created, instance.Status), cancellationToken);

        foreach (var stage in document.Stages)
        {
            var stageContext = CreateStageContext(instance, document.Version, stage);
            var stageExecution = await ExecuteStage(stageContext, cancellationToken);
            if (stageExecution.Status == StageExecutionStatus.Failed)
            {
                instance.Status = OrchestrationInstanceStatus.Failed;
                instance.FailedOnUtc = DateTime.UtcNow;
                instance.ErrorSummary = stageExecution.ErrorSummary;
                instance.LastUpdatedOnUtc = DateTime.UtcNow;
                await _instanceRepository.Update(instance, cancellationToken);
                await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceFailed", OrchestrationInstanceStatus.Running, instance.Status, stageExecution), cancellationToken);
                return;
            }

            if (instance.Status == OrchestrationInstanceStatus.Waiting)
            {
                await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceWaitingResponse", OrchestrationInstanceStatus.Running, instance.Status, stageExecution), cancellationToken);
                return;
            }
        }

        instance.Status = OrchestrationInstanceStatus.Completed;
        instance.CompletedOnUtc = DateTime.UtcNow;
        instance.FinalOutcome = "Completed";
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstance(instance, "InstanceCompleted", OrchestrationInstanceStatus.Running, instance.Status), cancellationToken);
    }

    private async Task<StageExecution> ExecuteStage(RuntimeStageExecutionContext context, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var stageExecution = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = context.Instance.Id,
            StageKey = context.Stage.Key,
            Order = context.Stage.Order,
            Status = StageExecutionStatus.Running,
            StartedOnUtc = started,
            ParallelGroupCount = 0
        };

        await _stageRepository.Create(stageExecution, cancellationToken);
        context.Instance.CurrentStageKey = context.Stage.Key;
        context.Instance.LastUpdatedOnUtc = started;
        await _instanceRepository.Update(context.Instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForStage(context.Instance, "StageStarted", StageExecutionStatus.Pending, stageExecution.Status, stageExecution), cancellationToken);

        foreach (var task in context.Stage.Tasks)
        {
            var taskContext = CreateTaskContext(context, stageExecution, task);
            var taskExecution = await ExecuteTask(taskContext, cancellationToken);

            if (taskExecution.Status == TaskExecutionStatus.Failed)
            {
                stageExecution.Status = StageExecutionStatus.Failed;
                stageExecution.FailedOnUtc = DateTime.UtcNow;
                stageExecution.ErrorSummary = taskExecution.ErrorSummary();
                await _stageRepository.Update(stageExecution, cancellationToken);
                await WriteTransition(RuntimeTransition.ForTask(context.Instance, "StageFailed", StageExecutionStatus.Running, stageExecution.Status, stageExecution, taskExecution), cancellationToken);
                return stageExecution;
            }

            if (taskExecution.Status == TaskExecutionStatus.WaitingResponse)
            {
                await _stageRepository.Update(stageExecution, cancellationToken);
                return stageExecution;
            }
        }

        stageExecution.Status = StageExecutionStatus.Completed;
        stageExecution.CompletedOnUtc = DateTime.UtcNow;
        await _stageRepository.Update(stageExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForStage(context.Instance, "StageCompleted", StageExecutionStatus.Running, stageExecution.Status, stageExecution), cancellationToken);
        return stageExecution;
    }

    private async Task<TaskExecution> ExecuteTask(RuntimeTaskExecutionContext context, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var taskExecution = CreateTaskExecution(context, started);

        await _taskRepository.Create(taskExecution, cancellationToken);
        await MarkInstanceTaskStarted(context, started, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(context.Instance, "TaskStarted", TaskExecutionStatus.Pending, taskExecution.Status, context.StageExecution, taskExecution), cancellationToken);

        if (!context.Task.IsMessaging)
            return await FailUnsupportedTask(context, taskExecution, cancellationToken);

        var attempt = await CreateAttempt(context, taskExecution, started, cancellationToken);
        var dispatch = await CreateDispatch(context, taskExecution, attempt, cancellationToken);
        var dispatchResult = await DispatchTask(context, taskExecution, attempt, dispatch, started, cancellationToken);

        if (!dispatchResult.Succeeded)
            return await FailDispatch(CreateDispatchFailure(context, taskExecution, attempt, dispatch, dispatchResult), cancellationToken);

        await MarkDispatchAccepted(dispatch, cancellationToken);
        await MarkTaskDispatchAccepted(context, taskExecution, attempt, dispatch, cancellationToken);

        if (context.Task.AwaitResponse)
            await MarkTaskWaiting(context, taskExecution, attempt, cancellationToken);
        else
            await WriteTransition(RuntimeTransition.ForAttempt(context.Instance, "TaskCompleted", TaskExecutionStatus.Running, taskExecution.Status, context.StageExecution, taskExecution, attempt), cancellationToken);

        return taskExecution;
    }

    private static TaskExecution CreateTaskExecution(RuntimeTaskExecutionContext context, DateTime started)
    {
        return new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = context.Instance.Id,
            StageExecutionId = context.StageExecution.Id,
            TaskKey = context.Task.Key,
            TaskKind = context.Task.IsMessaging ? TaskKind.Messaging : TaskKind.Plugin,
            Status = TaskExecutionStatus.Running,
            AwaitResponse = context.Task.AwaitResponse,
            StartedOnUtc = started,
            LastAttemptNumber = 1,
            CorrelationId = $"{context.Instance.CorrelationId}:{context.Task.Key}",
            Metadata = new Dictionary<string, JsonNode>
            {
                ["destination"] = context.Task.Destination ?? string.Empty,
                ["executionMode"] = context.Task.ExecutionMode ?? string.Empty
            }
        };
    }

    private static RuntimeStageExecutionContext CreateStageContext(OrchestrationInstance instance, string orchestrationVersion, RuntimeStageDocument stage)
    {
        return new RuntimeStageExecutionContext
        {
            Instance = instance,
            OrchestrationVersion = orchestrationVersion,
            Stage = stage
        };
    }

    private static RuntimeTaskExecutionContext CreateTaskContext(RuntimeStageExecutionContext context, StageExecution stageExecution, RuntimeTaskDocument task)
    {
        return new RuntimeTaskExecutionContext
        {
            Instance = context.Instance,
            OrchestrationVersion = context.OrchestrationVersion,
            StageExecution = stageExecution,
            Task = task
        };
    }

    private static RuntimeTaskDispatchFailure CreateDispatchFailure(RuntimeTaskExecutionContext context, TaskExecution taskExecution, TaskExecutionAttempt attempt, TaskDispatch dispatch, MessagingDispatchResult result)
    {
        return new RuntimeTaskDispatchFailure
        {
            Context = context,
            TaskExecution = taskExecution,
            Attempt = attempt,
            Dispatch = dispatch,
            DispatchResult = result
        };
    }

    private async Task MarkInstanceTaskStarted(RuntimeTaskExecutionContext context, DateTime started, CancellationToken cancellationToken)
    {
        context.Instance.CurrentTaskKey = context.Task.Key;
        context.Instance.LastUpdatedOnUtc = started;
        await _instanceRepository.Update(context.Instance, cancellationToken);
    }

    private async Task<TaskExecution> FailUnsupportedTask(RuntimeTaskExecutionContext context, TaskExecution taskExecution, CancellationToken cancellationToken)
    {
        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.FailedOnUtc = DateTime.UtcNow;
        taskExecution.Metadata["failureReason"] = $"Task kind '{context.Task.Kind}' is not supported in Beta 1.";
        await _taskRepository.Update(taskExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(context.Instance, "TaskFailed", TaskExecutionStatus.Running, taskExecution.Status, context.StageExecution, taskExecution), cancellationToken);
        return taskExecution;
    }

    private async Task<TaskExecutionAttempt> CreateAttempt(RuntimeTaskExecutionContext context, TaskExecution taskExecution, DateTime started, CancellationToken cancellationToken)
    {
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = taskExecution.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running,
            StartedOnUtc = started,
            RequestPayload = context.Instance.SnapshotPayload?.DeepClone() ?? new JsonObject()
        };
        await _attemptRepository.Create(attempt, cancellationToken);
        return attempt;
    }

    private async Task<TaskDispatch> CreateDispatch(RuntimeTaskExecutionContext context, TaskExecution taskExecution, TaskExecutionAttempt attempt, CancellationToken cancellationToken)
    {
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            Destination = context.Task.Destination ?? string.Empty,
            RequestPayload = attempt.RequestPayload.DeepClone(),
            DispatchStatus = "Pending",
            CommandId = Id.New().ToString(),
            CorrelationId = taskExecution.CorrelationId
        };
        await _dispatchRepository.Create(dispatch, cancellationToken);
        return dispatch;
    }

    private Task<MessagingDispatchResult> DispatchTask(RuntimeTaskExecutionContext context, TaskExecution taskExecution, TaskExecutionAttempt attempt, TaskDispatch dispatch, DateTime started, CancellationToken cancellationToken)
    {
        var command = CreateDispatchCommand(new MessagingDispatchCommandSource
        {
            Instance = context.Instance,
            OrchestrationVersion = context.OrchestrationVersion,
            StageExecution = context.StageExecution,
            TaskExecution = taskExecution,
            Attempt = attempt,
            Dispatch = dispatch,
            Task = context.Task,
            StartedOnUtc = started
        });
        return _messagingDispatcher.Dispatch(command, cancellationToken);
    }

    private async Task<TaskExecution> FailDispatch(RuntimeTaskDispatchFailure failure, CancellationToken cancellationToken)
    {
        var context = failure.Context;
        var taskExecution = failure.TaskExecution;
        var attempt = failure.Attempt;
        var dispatch = failure.Dispatch;
        var dispatchResult = failure.DispatchResult;

        dispatch.DispatchStatus = "Failed";
        dispatch.FailedOnUtc = DateTime.UtcNow;
        dispatch.FailureReason = dispatchResult.FailureReason;
        await _dispatchRepository.Update(dispatch, cancellationToken);

        attempt.Status = TaskExecutionStatus.Failed;
        attempt.FailedOnUtc = dispatch.FailedOnUtc;
        attempt.ErrorCode = "MessagingDispatchFailed";
        attempt.ErrorMessage = dispatchResult.FailureReason;
        attempt.DispatchId = dispatch.Id;
        await _attemptRepository.Update(attempt, cancellationToken);

        taskExecution.Status = TaskExecutionStatus.Failed;
        taskExecution.FailedOnUtc = dispatch.FailedOnUtc;
        taskExecution.Metadata["failureReason"] = dispatchResult.FailureReason;
        await _taskRepository.Update(taskExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForAttempt(context.Instance, "TaskFailed", TaskExecutionStatus.Running, taskExecution.Status, context.StageExecution, taskExecution, attempt), cancellationToken);
        return taskExecution;
    }

    private async Task MarkDispatchAccepted(TaskDispatch dispatch, CancellationToken cancellationToken)
    {
        dispatch.DispatchStatus = "Dispatched";
        dispatch.SentOnUtc = DateTime.UtcNow;
        dispatch.AcknowledgedOnUtc = dispatch.SentOnUtc;
        await _dispatchRepository.Update(dispatch, cancellationToken);
    }

    private async Task MarkTaskDispatchAccepted(RuntimeTaskExecutionContext context, TaskExecution taskExecution, TaskExecutionAttempt attempt, TaskDispatch dispatch, CancellationToken cancellationToken)
    {
        attempt.DispatchId = dispatch.Id;
        attempt.CompletedOnUtc = DateTime.UtcNow;
        attempt.Status = context.Task.AwaitResponse ? TaskExecutionStatus.WaitingResponse : TaskExecutionStatus.Completed;
        await _attemptRepository.Update(attempt, cancellationToken);

        taskExecution.Status = context.Task.AwaitResponse ? TaskExecutionStatus.WaitingResponse : TaskExecutionStatus.Completed;
        taskExecution.WaitingSinceUtc = context.Task.AwaitResponse ? DateTime.UtcNow : null;
        taskExecution.CompletedOnUtc = context.Task.AwaitResponse ? null : DateTime.UtcNow;
        await _taskRepository.Update(taskExecution, cancellationToken);
    }

    private async Task MarkTaskWaiting(RuntimeTaskExecutionContext context, TaskExecution taskExecution, TaskExecutionAttempt attempt, CancellationToken cancellationToken)
    {
        context.Instance.Status = OrchestrationInstanceStatus.Waiting;
        context.Instance.WaitingSinceUtc = DateTime.UtcNow;
        context.Instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(context.Instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForAttempt(context.Instance, "TaskWaitingResponse", TaskExecutionStatus.Running, taskExecution.Status, context.StageExecution, taskExecution, attempt), cancellationToken);
    }

    private async Task WriteTransition(RuntimeTransition transition, CancellationToken cancellationToken)
    {
        var occurredOnUtc = DateTime.UtcNow;
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
            OccurredOnUtc = occurredOnUtc,
            Message = transition.Type,
            Payload = transition.Payload?.DeepClone(),
            ProducedBy = "Krackend.Sagas.Orchestrations.Engine"
        };

        await _timelineRepository.Create(executionTransition, cancellationToken);
        try
        {
            await _reactiveEventPublisher.Publish(CreateReactiveEvent(transition, executionTransition), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Runtime execution state is already persisted; a live observer outage must not fail the orchestration.
        }
    }

    private static RuntimeReactiveEvent CreateReactiveEvent(RuntimeTransition transition, ExecutionTransition executionTransition)
    {
        return new RuntimeReactiveEvent
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
        };
    }

    private static string ResolveReactiveEventName(string transitionType)
    {
        return transitionType switch
        {
            "InstanceStarted" => RuntimeReactiveEventNames.OrchestrationStarted,
            "InstanceWaitingResponse" => RuntimeReactiveEventNames.OrchestrationWaiting,
            "InstanceCompleted" => RuntimeReactiveEventNames.OrchestrationCompleted,
            "InstanceFailed" => RuntimeReactiveEventNames.OrchestrationFailed,
            "StageStarted" => RuntimeReactiveEventNames.StageStarted,
            "StageCompleted" => RuntimeReactiveEventNames.StageCompleted,
            "StageFailed" => RuntimeReactiveEventNames.StageFailed,
            "TaskStarted" => RuntimeReactiveEventNames.TaskStarted,
            "TaskCompleted" => RuntimeReactiveEventNames.TaskCompleted,
            "TaskFailed" => RuntimeReactiveEventNames.TaskFailed,
            "TaskWaitingResponse" => RuntimeReactiveEventNames.TaskWaiting,
            "TaskResponseReceived" => RuntimeReactiveEventNames.TaskResponseReceived,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };
    }

    private static MessagingDispatchCommand CreateDispatchCommand(MessagingDispatchCommandSource source)
    {
        return new MessagingDispatchCommand
        {
            CommandId = source.Dispatch.CommandId,
            CorrelationId = source.TaskExecution.CorrelationId,
            Destination = source.Task.Destination ?? string.Empty,
            MessageVersion = string.IsNullOrWhiteSpace(source.Task.MessageVersion) ? "1.0.0" : source.Task.MessageVersion,
            Payload = source.Dispatch.RequestPayload.DeepClone(),
            OrchestrationDefinitionKey = source.Instance.OrchestrationDefinitionKey,
            OrchestrationVersion = source.OrchestrationVersion,
            OrchestrationInstanceId = source.Instance.Id.ToString(),
            TaskExecutionId = source.TaskExecution.Id.ToString(),
            DispatchId = source.Dispatch.Id.ToString(),
            EnvironmentKey = source.Instance.EnvironmentKey,
            StageKey = source.StageExecution.StageKey,
            TaskKey = source.TaskExecution.TaskKey,
            CurrentStatus = source.TaskExecution.Status.ToString(),
            Attempt = source.Attempt.AttemptNumber,
            StartedOnUtc = source.StartedOnUtc,
            UpdatedOnUtc = DateTime.UtcNow
        };
    }

    private async Task ContinueAfterResponse(OrchestrationInstance instance, StageExecution stageExecution, TaskExecution taskExecution, CancellationToken cancellationToken)
    {
        var artifact = await _artifactRepository.GetById(instance.RuntimeOrchestrationArtifactId, cancellationToken);
        var resume = CreateResumeContext(artifact, stageExecution, taskExecution);
        await ContinueCurrentStage(resume, instance, stageExecution, cancellationToken);
    }

    private async Task ContinueCurrentStage(RuntimeResumeContext resume, OrchestrationInstance instance, StageExecution stageExecution, CancellationToken cancellationToken)
    {
        var nextTaskIndex = resume.TaskIndex + 1;
        for (var i = nextTaskIndex; i < resume.CurrentStage.Tasks.Count; i++)
        {
            var task = resume.CurrentStage.Tasks.ElementAt(i);
            var taskExecution = await ExecuteTask(CreateTaskContext(instance, resume.Document.Version, stageExecution, task), cancellationToken);
            if (taskExecution.Status == TaskExecutionStatus.WaitingResponse)
                return;

            if (taskExecution.Status == TaskExecutionStatus.Failed)
            {
                await FailCurrentStage(instance, stageExecution, taskExecution, cancellationToken);
                return;
            }
        }

        await CompleteCurrentStage(stageExecution, instance, cancellationToken);
        await ContinueNextStages(resume, instance, cancellationToken);
    }

    private async Task ContinueNextStages(RuntimeResumeContext resume, OrchestrationInstance instance, CancellationToken cancellationToken)
    {
        var nextStageIndex = resume.StageIndex + 1;
        for (var i = nextStageIndex; i < resume.Document.Stages.Count; i++)
        {
            var stageExecution = await ExecuteStage(CreateStageContext(instance, resume.Document.Version, resume.Document.Stages.ElementAt(i)), cancellationToken);
            if (stageExecution.Status == StageExecutionStatus.Failed || instance.Status == OrchestrationInstanceStatus.Waiting)
                return;
        }

        await CompleteInstance(instance, cancellationToken);
    }

    private async Task CompleteResponse(OrchestrationInstance instance, StageExecution stageExecution, TaskExecution taskExecution, TaskExecutionAttempt attempt, RuntimeMessageResponseCommand command, CancellationToken cancellationToken)
    {
        var completedOnUtc = DateTime.UtcNow;
        attempt.Status = TaskExecutionStatus.Completed;
        attempt.CompletedOnUtc = completedOnUtc;
        attempt.ResponsePayload = command.Payload?.DeepClone();
        await _attemptRepository.Update(attempt, cancellationToken);

        taskExecution.Status = TaskExecutionStatus.Completed;
        taskExecution.CompletedOnUtc = completedOnUtc;
        taskExecution.WaitingSinceUtc = null;
        taskExecution.OutputVariablesPayload = command.Payload?.DeepClone();
        await _taskRepository.Update(taskExecution, cancellationToken);

        instance.Status = OrchestrationInstanceStatus.Running;
        instance.WaitingSinceUtc = null;
        instance.SnapshotPayload = command.Payload?.DeepClone() ?? instance.SnapshotPayload;
        instance.LastUpdatedOnUtc = completedOnUtc;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForAttemptPayload(instance, "TaskResponseReceived", TaskExecutionStatus.WaitingResponse, TaskExecutionStatus.Completed, stageExecution, taskExecution, attempt, command.Payload), cancellationToken);
        await WriteTransition(RuntimeTransition.ForAttempt(instance, "TaskCompleted", TaskExecutionStatus.WaitingResponse, taskExecution.Status, stageExecution, taskExecution, attempt), cancellationToken);
    }

    private async Task CompleteCurrentStage(StageExecution stageExecution, OrchestrationInstance instance, CancellationToken cancellationToken)
    {
        stageExecution.Status = StageExecutionStatus.Completed;
        stageExecution.CompletedOnUtc = DateTime.UtcNow;
        await _stageRepository.Update(stageExecution, cancellationToken);
        await WriteTransition(RuntimeTransition.ForStage(instance, "StageCompleted", StageExecutionStatus.Running, stageExecution.Status, stageExecution), cancellationToken);
    }

    private async Task CompleteInstance(OrchestrationInstance instance, CancellationToken cancellationToken)
    {
        instance.Status = OrchestrationInstanceStatus.Completed;
        instance.CompletedOnUtc = DateTime.UtcNow;
        instance.FinalOutcome = "Completed";
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForInstance(instance, "InstanceCompleted", OrchestrationInstanceStatus.Running, instance.Status), cancellationToken);
    }

    private async Task FailCurrentStage(OrchestrationInstance instance, StageExecution stageExecution, TaskExecution taskExecution, CancellationToken cancellationToken)
    {
        stageExecution.Status = StageExecutionStatus.Failed;
        stageExecution.FailedOnUtc = DateTime.UtcNow;
        stageExecution.ErrorSummary = taskExecution.ErrorSummary();
        await _stageRepository.Update(stageExecution, cancellationToken);

        instance.Status = OrchestrationInstanceStatus.Failed;
        instance.FailedOnUtc = DateTime.UtcNow;
        instance.ErrorSummary = stageExecution.ErrorSummary;
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _instanceRepository.Update(instance, cancellationToken);
        await WriteTransition(RuntimeTransition.ForTask(instance, "StageFailed", StageExecutionStatus.Running, stageExecution.Status, stageExecution, taskExecution), cancellationToken);
        await WriteTransition(RuntimeTransition.ForStage(instance, "InstanceFailed", OrchestrationInstanceStatus.Running, instance.Status, stageExecution), cancellationToken);
    }

    private async Task<TaskExecutionAttempt> ResolveResponseAttempt(TaskExecution taskExecution, RuntimeMessageResponseCommand command, CancellationToken cancellationToken)
    {
        var dispatchId = ParseId(command.DispatchId, nameof(command.DispatchId));
        var attempts = await _attemptRepository.GetByTaskExecutionId(taskExecution.Id, cancellationToken);
        var attempt = attempts.OrderByDescending(x => x.AttemptNumber).FirstOrDefault(x => x.DispatchId == dispatchId);
        return attempt ?? throw new InvalidOperationException($"No task attempt found for dispatch '{command.DispatchId}'.");
    }

    private static RuntimeResumeContext CreateResumeContext(RuntimeOrchestrationArtifact artifact, StageExecution stageExecution, TaskExecution taskExecution)
    {
        var document = RuntimeArtifactDocument.Parse(artifact.ArtifactPayload, artifact.Version.ToString());
        var stageIndex = document.Stages.ToList().FindIndex(x => string.Equals(x.Key, stageExecution.StageKey, StringComparison.OrdinalIgnoreCase));
        if (stageIndex < 0)
            throw new InvalidOperationException($"Stage '{stageExecution.StageKey}' was not found in artifact '{artifact.Id}'.");

        var stage = document.Stages.ElementAt(stageIndex);
        var taskIndex = stage.Tasks.ToList().FindIndex(x => string.Equals(x.Key, taskExecution.TaskKey, StringComparison.OrdinalIgnoreCase));
        if (taskIndex < 0)
            throw new InvalidOperationException($"Task '{taskExecution.TaskKey}' was not found in stage '{stage.Key}'.");

        return new RuntimeResumeContext { Document = document, CurrentStage = stage, StageIndex = stageIndex, TaskIndex = taskIndex };
    }

    private static RuntimeTaskExecutionContext CreateTaskContext(OrchestrationInstance instance, string orchestrationVersion, StageExecution stageExecution, RuntimeTaskDocument task)
    {
        return new RuntimeTaskExecutionContext
        {
            Instance = instance,
            OrchestrationVersion = orchestrationVersion,
            StageExecution = stageExecution,
            Task = task
        };
    }

    private static Id ParseId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier value is required.", parameterName);

        return new Id(Ulid.Parse(value));
    }
}
