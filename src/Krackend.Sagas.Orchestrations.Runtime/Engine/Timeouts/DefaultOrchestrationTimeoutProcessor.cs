using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class DefaultOrchestrationTimeoutProcessor : IOrchestrationTimeoutProcessor, IOrchestrationTimeoutBatchProcessor, IOrchestrationTimeoutContinuationProcessor
{
    private const string DefaultTimeoutErrorCode = "TaskTimedOut";
    private const string TimeoutObservedOnUtcMetadataKey = "TimeoutObservedOnUtc";
    private const string TimeoutGraceExpiresOnUtcMetadataKey = "TimeoutGraceExpiresOnUtc";

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly IRuntimeArtifactResolver _artifactResolver;
    private readonly ISagaEngine _sagaEngine;
    private readonly IOrchestrationInstanceCoordinator _coordinator;
    private readonly IRuntimeAdmissionController _admissionController;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private readonly IOrchestrationDurableWorkInspector _workInspector;
    private readonly OrchestrationTimeoutRepairCursor _repairCursor;
    private readonly RuntimeForegroundActivity _foregroundActivity;
    private readonly OrchestrationTimeoutOptions _timeoutOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DefaultOrchestrationTimeoutProcessor> _logger;

    public DefaultOrchestrationTimeoutProcessor(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IExecutionTransitionRepository transitionRepository,
        IRuntimeArtifactResolver artifactResolver,
        ISagaEngine sagaEngine,
        IOrchestrationInstanceCoordinator coordinator,
        IRuntimeAdmissionController admissionController,
        IRuntimeStorageUnitOfWork unitOfWork,
        IOrchestrationDurableWorkInspector workInspector = null,
        OrchestrationTimeoutRepairCursor repairCursor = null,
        ILogger<DefaultOrchestrationTimeoutProcessor> logger = null,
        RuntimeForegroundActivity foregroundActivity = null,
        IOptions<OrchestrationTimeoutOptions> timeoutOptions = null,
        IServiceScopeFactory serviceScopeFactory = null)
    {
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
        _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workInspector = workInspector;
        _repairCursor = repairCursor ?? new OrchestrationTimeoutRepairCursor();
        _logger = logger ?? NullLogger<DefaultOrchestrationTimeoutProcessor>.Instance;
        _foregroundActivity = foregroundActivity ?? new RuntimeForegroundActivity();
        _timeoutOptions = timeoutOptions?.Value ?? new OrchestrationTimeoutOptions();
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TimeoutProcessing, cancellationToken);

        if (ShouldYieldRecovery())
            return 0;

        await _repairCursor.Gate.WaitAsync(cancellationToken);
        try
        {
            if (ShouldYieldRecovery())
                return 0;

            var take = Math.Clamp(_timeoutOptions.RecoveryBatchSize, 1, 1_000);
            var pageReader = _taskRepository as IResponseTimeoutRepairPageReader;
            if (pageReader is null && !_repairCursor.WarnedMissingPaging)
            {
                _logger.LogWarning("The configured task repository does not support paged timeout repair; inconsistent records can block later candidates.");
                _repairCursor.WarnedMissingPaging = true;
            }
            var waitingTasks = pageReader is null
                ? await _taskRepository.GetResponseTimeoutsDue(utcNow, take, cancellationToken)
                : await pageReader.GetResponseTimeoutsDuePage(utcNow, _repairCursor.DueOffset, take, cancellationToken);
            var processed = 0;
            var unresolved = 0;
            var groups = waitingTasks
                .GroupBy(task => task.OrchestrationInstanceId)
                .ToArray();
            var maxParallelism = Math.Clamp(
                _timeoutOptions.RecoveryMaxDegreeOfParallelism,
                1,
                64);

            if (_serviceScopeFactory is null || maxParallelism == 1)
            {
                var batch = new OrchestrationTimeoutBatchContext();
                foreach (var group in groups)
                {
                    foreach (var waitingTask in group)
                    {
                        if (ShouldYieldRecovery())
                            break;
                        await ProcessCandidateAsync(
                            this,
                            batch,
                            waitingTask,
                            utcNow,
                            cancellationToken,
                            applied => processed += applied ? 1 : 0,
                            ignored => unresolved += ignored ? 1 : 0);
                    }
                }
            }
            else
            {
                await Parallel.ForEachAsync(
                    groups,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxParallelism,
                        CancellationToken = cancellationToken
                    },
                    async (group, token) =>
                    {
                        if (ShouldYieldRecovery())
                            return;

                        await using var scope = _serviceScopeFactory.CreateAsyncScope();
                        var scopedProcessor = scope.ServiceProvider
                            .GetRequiredService<IOrchestrationTimeoutProcessor>();
                        var batch = new OrchestrationTimeoutBatchContext();
                        foreach (var waitingTask in group)
                        {
                            if (ShouldYieldRecovery())
                                break;
                            await ProcessCandidateAsync(
                                scopedProcessor,
                                batch,
                                waitingTask,
                                utcNow,
                                token,
                                applied =>
                                {
                                    if (applied) Interlocked.Increment(ref processed);
                                },
                                ignored =>
                                {
                                    if (ignored) Interlocked.Increment(ref unresolved);
                                });
                        }
                    });
            }
            if (pageReader is not null)
                _repairCursor.DueOffset = waitingTasks.Count < take || unresolved > int.MaxValue - _repairCursor.DueOffset
                    ? 0 : _repairCursor.DueOffset + unresolved;
            return processed;

            async Task ProcessCandidateAsync(
                IOrchestrationTimeoutProcessor processor,
                OrchestrationTimeoutBatchContext batch,
                TaskExecution waitingTask,
                DateTime observedOnUtc,
                CancellationToken token,
                Action<bool> recordApplied,
                Action<bool> recordIgnored)
            {
                Exception failure = null;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    IServiceScope retryScope = null;
                    try
                    {
                        var currentProcessor = processor;
                        if (attempt > 0 && _serviceScopeFactory is not null)
                        {
                            retryScope = _serviceScopeFactory.CreateScope();
                            currentProcessor = retryScope.ServiceProvider
                                .GetRequiredService<IOrchestrationTimeoutProcessor>();
                        }

                        var result = currentProcessor is IOrchestrationTimeoutBatchProcessor batchProcessor
                            ? await batchProcessor.ProcessTaskTimeoutAsync(
                                waitingTask.Id,
                                observedOnUtc,
                                batch,
                                token)
                            : await currentProcessor.ProcessTaskTimeoutAsync(
                                waitingTask.Id,
                                observedOnUtc,
                                token);
                        recordApplied(result.Applied);
                        recordIgnored(!result.Applied);
                        return;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (
                        attempt < 2 && _serviceScopeFactory is not null && IsOptimisticConflict(exception))
                    {
                        failure = exception;
                        if (ShouldYieldRecovery())
                            return;
                        await Task.Yield();
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                        break;
                    }
                    finally
                    {
                        retryScope?.Dispose();
                    }
                }

                recordIgnored(true);
                _logger.LogWarning(failure,
                    "Could not process overdue response timeout for task '{TaskId}' after bounded conflict recovery. Conflicting entities: {ConflictEntities}.",
                    waitingTask.Id,
                    DescribeOptimisticConflict(failure));
            }
        }
        finally { _repairCursor.Gate.Release(); }
    }

    private bool ShouldYieldRecovery()
        => _foregroundActivity.ShouldYieldRecovery(TimeSpan.FromMilliseconds(Math.Clamp(
            _timeoutOptions.RecoveryQuietPeriodMilliseconds,
            0,
            60_000)));

    private static bool IsOptimisticConflict(Exception exception)
        => exception is RuntimeStateConflictException ||
           string.Equals(
               exception.GetType().FullName,
               "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException",
               StringComparison.Ordinal) ||
           exception.InnerException is not null && IsOptimisticConflict(exception.InnerException);

    private static string DescribeOptimisticConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.Equals(
                    current.GetType().FullName,
                    "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException",
                    StringComparison.Ordinal))
                continue;

            if (current.GetType().GetProperty("Entries")?.GetValue(current)
                is not System.Collections.IEnumerable entries)
                return "unknown";

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var entity = entry?.GetType().GetProperty("Entity")?.GetValue(entry);
                if (entity is not null)
                    names.Add(entity.GetType().Name);
            }
            return names.Count == 0 ? "unknown" : string.Join(", ", names.Order());
        }

        return exception.GetType().Name;
    }

    public async Task ResumeTimedOutAsync(Id instanceId, Id taskId, Id attemptId, Id dispatchId, int attemptNumber,
        CancellationToken cancellationToken = default)
    {
        var acquired = await _coordinator.TryExecuteAsync(instanceId, async token =>
        {
            OrchestrationInstance instance;
            TaskExecution task;
            TaskExecutionAttempt attempt;
            try
            {
                instance = await _instanceRepository.GetById(instanceId, token);
                task = await _taskRepository.GetById(taskId, token);
                attempt = await _attemptRepository.GetById(attemptId, token);
            }
            catch (KeyNotFoundException) { return; }
            var dispatch = await _dispatchRepository.TryGetById(dispatchId, token);
            if (instance.Status != OrchestrationInstanceStatus.Running || task.OrchestrationInstanceId != instanceId ||
                task.Status != TaskExecutionStatus.TimedOut || task.LastAttemptNumber != attemptNumber ||
                attempt.TaskExecutionId != taskId || attempt.AttemptNumber != attemptNumber ||
                attempt.Status != TaskExecutionStatus.TimedOut || attempt.DispatchId != dispatchId ||
                dispatch is null || dispatch.TaskExecutionAttemptId != attemptId ||
                !string.Equals(dispatch.DispatchStatus, "TimedOut", StringComparison.OrdinalIgnoreCase))
                return;

            // The timeout transition already committed. Resume from persisted state without
            // applying the timeout again or accepting a timer for an older attempt.
            await _sagaEngine.OrchestrateAsync(new ForwardIntent
            {
                ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
                IngressTransport = IngressTransport.Messaging,
                MessageMetadata = new OrchestrationMessageMetadata
                {
                    SagaId = GetSagaId(instance), OrchestrationInstanceId = instance.Id.ToString(),
                    CorrelationId = instance.CorrelationId, CurrentStage = instance.CurrentStageKey
                },
                Payload = instance.SnapshotPayload?.DeepClone()
            }, token);
        }, cancellationToken, waitForLease: false);
        if (!acquired) throw new OrchestrationInstanceLeaseUnavailableException(instanceId);
    }

    public async Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutAsync(
        Id taskExecutionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => await ProcessTaskTimeoutAsync(taskExecutionId, utcNow, null, cancellationToken);

    public async Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutAsync(
        Id orchestrationInstanceId,
        Id taskExecutionId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TimeoutProcessing, cancellationToken);

        TaskResponseTimeoutProcessingResult result = TaskResponseTimeoutProcessingResult.Ignored;
        var acquired = await _coordinator.TryExecuteAsync(
            orchestrationInstanceId,
            async token =>
            {
                TaskExecution task;
                try
                {
                    task = await _taskRepository.GetById(taskExecutionId, token);
                }
                catch (KeyNotFoundException)
                {
                    return;
                }

                if (task.OrchestrationInstanceId != orchestrationInstanceId)
                    return;

                result = await TryProcessTaskCoreAsync(task, utcNow, null, token);
            },
            cancellationToken,
            waitForLease: false);

        return acquired ? result : new TaskResponseTimeoutProcessingResult(false, RetryRequired: true);
    }

    public async Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutAsync(
        Id taskExecutionId,
        DateTime utcNow,
        OrchestrationTimeoutBatchContext batch,
        CancellationToken cancellationToken = default)
    {
        var defaultResolver = _artifactResolver as DefaultRuntimeArtifactResolver;
        var previousBatch = defaultResolver?.TimeoutBatch;
        if (defaultResolver is not null) defaultResolver.TimeoutBatch = batch;
        try
        {
            return await ProcessTaskTimeoutInBatchAsync(taskExecutionId, utcNow, batch, cancellationToken);
        }
        finally
        {
            if (defaultResolver is not null) defaultResolver.TimeoutBatch = previousBatch;
        }
    }

    private async Task<TaskResponseTimeoutProcessingResult> ProcessTaskTimeoutInBatchAsync(
        Id taskExecutionId, DateTime utcNow, OrchestrationTimeoutBatchContext batch, CancellationToken cancellationToken)
    {
        await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.TimeoutProcessing, cancellationToken);

        TaskResponseTimeoutProcessingResult result = TaskResponseTimeoutProcessingResult.Ignored;
        TaskExecution task;
        try
        {
            task = await _taskRepository.GetById(taskExecutionId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return result;
        }

        var acquired = await _coordinator.TryExecuteAsync(
            task.OrchestrationInstanceId,
            async token => result = await TryProcessTaskCoreAsync(task, utcNow, batch, token),
            cancellationToken,
            waitForLease: false);

        return acquired ? result : new TaskResponseTimeoutProcessingResult(false, RetryRequired: true);
    }

    private async Task<TaskResponseTimeoutProcessingResult> TryProcessTaskCoreAsync(
        TaskExecution waitingTask,
        DateTime utcNow,
        OrchestrationTimeoutBatchContext batch,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetById(waitingTask.Id, cancellationToken);
        if (task.Status == TaskExecutionStatus.TimedOut)
        {
            return new TaskResponseTimeoutProcessingResult(false) { ResumeRequired = true };
        }
        if (task.Status != TaskExecutionStatus.WaitingResponse ||
            (task.WaitingSinceUtc is null && task.ResponseDeadlineUtc is null))
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var instance = await _instanceRepository.GetById(task.OrchestrationInstanceId, cancellationToken);
        if (IsTerminal(instance.Status))
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var stage = await _stageRepository.GetById(task.StageExecutionId, cancellationToken);
        var resolvedArtifact = batch is null
            ? await _artifactResolver.ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), cancellationToken)
            : await batch.ResolveAsync(instance.RuntimeOrchestrationArtifactId, _artifactResolver, cancellationToken);
        var taskArtifact = resolvedArtifact.Artifact.StageDefinitions
            .FirstOrDefault(x => string.Equals(x.Key, stage.StageKey, StringComparison.OrdinalIgnoreCase))?
            .TaskDefinitions
            .FirstOrDefault(x => string.Equals(x.Key, task.TaskKey, StringComparison.OrdinalIgnoreCase));
        var timeoutPolicy = taskArtifact?.TimeoutPolicy;
        if (timeoutPolicy is null)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var dueOnUtc = task.ResponseDeadlineUtc ?? task.WaitingSinceUtc.Value.Add(timeoutPolicy.Timeout.Value);
        if (dueOnUtc > utcNow)
        {
            return new TaskResponseTimeoutProcessingResult(false, RetryRequired: true);
        }

        var attempts = await _attemptRepository.GetByTaskExecutionId(task.Id, cancellationToken);
        var attempt = attempts
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefault();
        if (attempt is null || attempt.Status != TaskExecutionStatus.WaitingResponse ||
            (task.LastAttemptNumber > 0 && task.LastAttemptNumber != attempt.AttemptNumber))
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        var dispatch = attempt.DispatchId.HasValue
            ? await _dispatchRepository.TryGetById(attempt.DispatchId.Value, cancellationToken)
            : null;
        if (dispatch?.SentOnUtc is null || dispatch.TaskExecutionAttemptId != attempt.Id ||
            !string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase))
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        if ((dispatch.CallbackPending && dispatch.CallbackAcceptedOnUtc <= dueOnUtc) ||
            (_workInspector is not null && await _workInspector.InspectPendingResponseAsync(
                dispatch, attempt, dueOnUtc, cancellationToken) == DurableWorkPresence.Present))
        {
            // The broker delivery was already accepted on time. Keep the timeout recoverable
            // while the durable callback finishes instead of declaring a false timeout.
            return new TaskResponseTimeoutProcessingResult(false, RetryRequired: true) { WaitingForResponseProcessing = true };
        }

        var waitGraceResult = await TryApplyWaitGraceAsync(instance, task, timeoutPolicy, utcNow, cancellationToken);
        if (waitGraceResult == WaitGraceResult.Waiting)
        {
            return TaskResponseTimeoutProcessingResult.Ignored;
        }

        if (waitGraceResult == WaitGraceResult.Applied)
        {
            return new TaskResponseTimeoutProcessingResult(true, task.ResponseDeadlineUtc);
        }

        var errorCode = ResolveTimeoutErrorCode(timeoutPolicy);
        var errorMessage = $"Task '{task.TaskKey}' timed out after waiting {timeoutPolicy.Timeout.Value}.";

        ApplyTimeoutState(instance, task, attempt, dispatch, timeoutPolicy, errorCode, errorMessage, utcNow);

        await PersistTimeoutAsync(
            instance,
            task,
            attempt,
            dispatch,
            errorMessage,
            utcNow,
            cancellationToken);

        await _sagaEngine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = GetSagaId(instance),
                OrchestrationInstanceId = instance.Id.ToString(),
                CurrentStage = stage.StageKey,
                CurrentTasks = [task.TaskKey],
                CorrelationId = instance.CorrelationId,
                TaskExecutionId = task.Id.ToString(),
                DispatchId = dispatch?.Id.ToString(),
                Attempt = attempt.AttemptNumber
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                Status = "TimedOut",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                ErrorType = "Timeout",
                CompletedOnUtc = utcNow
            },
            Payload = instance.SnapshotPayload?.DeepClone()
        }, cancellationToken);

        return new TaskResponseTimeoutProcessingResult(true);
    }

    private async Task<WaitGraceResult> TryApplyWaitGraceAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TimeoutPolicyArtifact timeoutPolicy,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (timeoutPolicy.TimeoutBehaviorPolicy is not WaitTimeoutBehaviorPolicyArtifact wait)
        {
            return WaitGraceResult.NotApplicable;
        }

        if (TryGetDateTime(task.Metadata, TimeoutGraceExpiresOnUtcMetadataKey, out var graceExpiresOnUtc))
        {
            return graceExpiresOnUtc > utcNow ? WaitGraceResult.Waiting : WaitGraceResult.NotApplicable;
        }

        graceExpiresOnUtc = utcNow.Add(wait.WaitingTime.Value);
        task.Metadata[TimeoutObservedOnUtcMetadataKey] = JsonValue.Create(utcNow);
        task.Metadata[TimeoutGraceExpiresOnUtcMetadataKey] = JsonValue.Create(graceExpiresOnUtc);
        task.ResponseDeadlineUtc = graceExpiresOnUtc;
        instance.LastUpdatedOnUtc = utcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await _taskRepository.Update(task, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TransitionType = "TaskTimeoutPolicyApplied",
                FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                ToStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                OccurredOnUtc = utcNow,
                Message = $"Wait timeout policy applied for task '{task.TaskKey}' until {graceExpiresOnUtc:O}.",
                ProducedBy = nameof(DefaultOrchestrationTimeoutProcessor)
            }, cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return WaitGraceResult.Applied;
    }

    private async Task PersistTimeoutAsync(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        string errorMessage,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            if (dispatch is not null)
            {
                await _dispatchRepository.Update(dispatch, cancellationToken);
            }

            await _attemptRepository.Update(attempt, cancellationToken);
            await _taskRepository.Update(task, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.NewDistributed(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = "TaskTimedOut",
                FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                ToStatus = TaskExecutionStatus.TimedOut.ToString(),
                OccurredOnUtc = utcNow,
                Message = errorMessage,
                ProducedBy = nameof(DefaultOrchestrationTimeoutProcessor)
            }, cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static void ApplyTimeoutState(
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        TaskDispatch dispatch,
        TimeoutPolicyArtifact timeoutPolicy,
        string errorCode,
        string errorMessage,
        DateTime utcNow)
    {
        task.Status = TaskExecutionStatus.TimedOut;
        task.WaitingSinceUtc = null;
        task.ResponseDeadlineUtc = null;
        task.TimeoutActionId = null;
        task.TimeoutActionLane = null;
        task.FailedOnUtc = utcNow;
        task.TimedOutOnUtc = utcNow;
        task.Metadata["TimeoutBehavior"] = JsonValue.Create(timeoutPolicy.TimeoutBehavior.ToString());
        task.Metadata["TimeoutErrorCode"] = JsonValue.Create(errorCode);

        attempt.Status = TaskExecutionStatus.TimedOut;
        attempt.WaitingSinceUtc = null;
        attempt.FailedOnUtc = utcNow;
        attempt.TimedOutOnUtc = utcNow;
        attempt.ErrorCode = errorCode;
        attempt.ErrorMessage = errorMessage;
        attempt.Metadata["ExecutionSucceeded"] = JsonValue.Create(false);
        attempt.Metadata["ExecutionStatus"] = JsonValue.Create("TimedOut");
        attempt.Metadata["ExecutionErrorCode"] = JsonValue.Create(errorCode);
        attempt.Metadata["ExecutionErrorMessage"] = JsonValue.Create(errorMessage);
        attempt.Metadata["ExecutionErrorType"] = JsonValue.Create("Timeout");
        attempt.Metadata["ExecutionCompletedOnUtc"] = JsonValue.Create(utcNow);

        if (dispatch is not null)
        {
            dispatch.DispatchStatus = "TimedOut";
            dispatch.FailedOnUtc = utcNow;
            dispatch.FailureReason = errorMessage;
        }

        // A timeout belongs to this task attempt. The aggregate instance stays Running while
        // DecisionControl evaluates retries, continuation, compensation, or its idle window.
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.WaitingSinceUtc = null;
        instance.IdleSinceUtc = null;
        instance.IdleExpiresOnUtc = null;
        instance.FailedOnUtc = null;
        instance.ErrorSummary = errorMessage;
        instance.LastUpdatedOnUtc = utcNow;
    }

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensating
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Aborted
            or OrchestrationInstanceStatus.Failed;

    private static bool IsContinueTimeout(TimeoutPolicyArtifact timeoutPolicy)
        => timeoutPolicy.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicyArtifact wait &&
            wait.OrchestrationAction == OrchestrationActionOnTimeout.Continue;

    private static string ResolveTimeoutErrorCode(TimeoutPolicyArtifact timeoutPolicy)
        => timeoutPolicy.TimeoutBehaviorPolicy is FailTimeoutBehaviorPolicyArtifact fail &&
            !string.IsNullOrWhiteSpace(fail.ErrorCode)
            ? fail.ErrorCode
            : DefaultTimeoutErrorCode;

    private static bool TryGetDateTime(
        IReadOnlyDictionary<string, JsonNode> metadata,
        string key,
        out DateTime value)
    {
        value = default;
        if (metadata is null ||
            !metadata.TryGetValue(key, out var node) ||
            node is null)
        {
            return false;
        }

        try
        {
            value = node.GetValue<DateTime>();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
        }

        try
        {
            return DateTime.TryParse(
                node.GetValue<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string GetSagaId(OrchestrationInstance instance)
        => string.IsNullOrWhiteSpace(instance.SagaId)
            ? instance.Id.ToString()
            : instance.SagaId;

    private enum WaitGraceResult
    {
        NotApplicable,
        Waiting,
        Applied
    }
}
