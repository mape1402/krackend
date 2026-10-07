using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    internal class DecisionControl : IDecisionControl
    {
        private const string BranchNextStageIdMetadataKey = "Branch.NextStageId";
        private const string CompensationTerminalFailureMetadataKey = "Compensation.TerminalFailure";

        private readonly IRuntimeArtifactResolver _artifactResolver;
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IOrchestrationPayloadState _payloadState;

        public DecisionControl(
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IOrchestrationPayloadState payloadState)
        {
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
        }

        public async Task<IReadOnlyCollection<IDecision>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
        {
            var callbackDecision = await TryBuildCallbackDecision(request, cancellationToken);
            if (callbackDecision is not null)
            {
                return [callbackDecision];
            }

            if (IsCallbackSignal(request.MessageMetadata) && !IsRuntimeTimeoutSignal(request))
            {
                return [];
            }

            if (string.IsNullOrWhiteSpace(request.MessageMetadata?.OrchestrationInstanceId))
            {
                return [];
            }

            var resolvedArtifact = await _artifactResolver.ResolveAsync(request.ArtifactId, cancellationToken);
            var instanceId = RuntimeIdParser.Parse(request.MessageMetadata.OrchestrationInstanceId);
            var instance = await _instanceRepository.GetById(instanceId, cancellationToken);
            if (instance.Status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Stopped
                or OrchestrationInstanceStatus.Compensating
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Aborted)
            {
                return [];
            }

            if (instance.Status == OrchestrationInstanceStatus.DeadLettered)
            {
                return [];
            }

            var stages = resolvedArtifact.Artifact.StageDefinitions
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Order)
                .ToArray();
            var metadataDescriptors = resolvedArtifact.Artifact.MetadataDescriptors ?? Array.Empty<MetadataDescriptorArtifact>();
            var stageExecutions = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);
            var currentStage = ResolveCurrentStage(stages, stageExecutions, instance);
            if (currentStage is null)
            {
                return [new CompleteInstanceDecision(instance.Id)];
            }

            var currentStageExecution = stageExecutions.FirstOrDefault(x => x.StageKey == currentStage.Key);
            if (currentStageExecution is null)
            {
                return [new StartStageDecision(instance.Id, currentStage, request.Payload?.ToJsonString())
                {
                    MetadataDescriptors = metadataDescriptors
                }];
            }

            var tasks = currentStage.TaskDefinitions
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Order)
                .ToArray();
            var taskExecutions = (await _taskRepository.GetByInstanceId(instance.Id, cancellationToken))
                .Where(x => x.StageExecutionId == currentStageExecution.Id)
                .ToArray();
            var dispatchPayload = _payloadState.GetDispatchPayload(instance, request.Payload);

            var failedTask = taskExecutions.FirstOrDefault(x =>
                x.Status is TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut);
            if (instance.Status == OrchestrationInstanceStatus.Failed && failedTask is null)
            {
                return [new DeadLetterInstanceDecision(
                    instance.Id,
                    currentStageExecution?.Id,
                    null,
                    instance.ErrorSummary ?? "Orchestration instance failed without a recoverable task decision.")];
            }

            if (failedTask is not null)
            {
                if (HasCompensationTerminalFailure(instance))
                {
                    return [];
                }

                var failedArtifact = tasks.FirstOrDefault(x => x.Key == failedTask.TaskKey);
                if (await ShouldRetryAsync(failedTask, failedArtifact, request, cancellationToken))
                {
                    return [new RetryDecision(
                        instance.Id,
                        currentStageExecution.Id,
                        failedTask.Id,
                        currentStage.Key,
                        failedArtifact,
                        dispatchPayload?.ToJsonString())
                    {
                        MetadataDescriptors = metadataDescriptors,
                        OrchestrationExecutionPolicy = resolvedArtifact.Artifact.ExecutionPolicy,
                        StageExecutionPolicy = currentStage.ExecutionPolicy
                    }];
                }

                if (failedArtifact?.OnErrorPolicy == OnErrorPolicy.StopAndCompensate)
                {
                    return [new CompensateInstanceDecision(instance.Id, request.ArtifactId, dispatchPayload?.ToJsonString())];
                }

                if (ShouldContinueAfterFailure(failedTask, failedArtifact))
                {
                    return BuildNextTaskDecisions(
                        instance,
                        stages,
                        currentStage,
                        currentStageExecution,
                        tasks,
                        taskExecutions,
                        dispatchPayload,
                        metadataDescriptors,
                        resolvedArtifact.Artifact.ExecutionPolicy);
                }

                return [new DeadLetterInstanceDecision(
                    instance.Id,
                    currentStageExecution.Id,
                    failedTask.Id,
                    ResolveDeadLetterReason(instance, failedTask))];
            }

            if (taskExecutions.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse))
            {
                return [];
            }

            return BuildNextTaskDecisions(
                instance,
                stages,
                currentStage,
                currentStageExecution,
                tasks,
                taskExecutions,
                dispatchPayload,
                metadataDescriptors,
                resolvedArtifact.Artifact.ExecutionPolicy);
        }

        private static IReadOnlyCollection<IDecision> BuildNextTaskDecisions(
            OrchestrationInstance instance,
            IReadOnlyCollection<StageArtifact> stages,
            StageArtifact currentStage,
            StageExecution currentStageExecution,
            IReadOnlyCollection<TaskArtifact> tasks,
            IReadOnlyCollection<TaskExecution> taskExecutions,
            JsonNode dispatchPayload,
            IReadOnlyCollection<MetadataDescriptorArtifact> metadataDescriptors,
            ExecutionPolicyArtifact orchestrationExecutionPolicy)
        {
            var nextTask = tasks.FirstOrDefault(task =>
                taskExecutions.All(execution => execution.TaskKey != task.Key));
            if (nextTask is null)
            {
                return [new CompleteStageDecision(instance.Id, currentStageExecution.Id, currentStage, stages)
                {
                    MetadataDescriptors = metadataDescriptors
                }];
            }

            if (nextTask.ParallelGroupId is null)
            {
                return [new DispatchTaskDecision(
                    instance.Id,
                    currentStageExecution.Id,
                    currentStage.Key,
                    nextTask,
                    dispatchPayload?.ToJsonString())
                {
                    MetadataDescriptors = metadataDescriptors,
                    OrchestrationExecutionPolicy = orchestrationExecutionPolicy,
                    StageExecutionPolicy = currentStage.ExecutionPolicy
                }];
            }

            var group = currentStage.ParallelGroups.FirstOrDefault(x => x.Id == nextTask.ParallelGroupId.Value);
            var maxParallelAgents = group?.MaxParallelAgents ?? int.MaxValue;
            var groupTasks = tasks
                .Where(x => x.ParallelGroupId == nextTask.ParallelGroupId)
                .Where(task => taskExecutions.All(execution => execution.TaskKey != task.Key))
                .Take(maxParallelAgents)
                .Select(task => new DispatchTaskDecision(
                    instance.Id,
                    currentStageExecution.Id,
                    currentStage.Key,
                    task,
                    dispatchPayload?.ToJsonString())
                {
                    MetadataDescriptors = metadataDescriptors,
                    OrchestrationExecutionPolicy = orchestrationExecutionPolicy,
                    StageExecutionPolicy = currentStage.ExecutionPolicy
                })
                .Cast<IDecision>()
                .ToArray();

            return groupTasks.Length == 0
                ? [new CompleteStageDecision(instance.Id, currentStageExecution.Id, currentStage, stages)
                {
                    MetadataDescriptors = metadataDescriptors
                }]
                : groupTasks;
        }

        private static StageArtifact ResolveCurrentStage(
            IReadOnlyCollection<StageArtifact> stages,
            IReadOnlyCollection<StageExecution> stageExecutions,
            OrchestrationInstance instance)
        {
            var branchTarget = ResolveBranchTargetStage(stages, stageExecutions, instance);
            if (branchTarget is not null)
            {
                return branchTarget;
            }

            foreach (var stage in stages)
            {
                var execution = stageExecutions.FirstOrDefault(x => x.StageKey == stage.Key);
                if (execution is null || !IsStageFinished(execution.Status))
                {
                    return stage;
                }
            }

            return null;
        }

        private static StageArtifact ResolveBranchTargetStage(
            IReadOnlyCollection<StageArtifact> stages,
            IReadOnlyCollection<StageExecution> stageExecutions,
            OrchestrationInstance instance)
        {
            var targetStageId = TryGetString(instance?.Metadata, BranchNextStageIdMetadataKey);
            if (string.IsNullOrWhiteSpace(targetStageId))
            {
                return null;
            }

            var targetStage = stages.FirstOrDefault(stage =>
                string.Equals(stage.Id.ToString(), targetStageId, StringComparison.OrdinalIgnoreCase));
            if (targetStage is null)
            {
                return null;
            }

            var execution = stageExecutions.FirstOrDefault(x =>
                string.Equals(x.StageKey, targetStage.Key, StringComparison.OrdinalIgnoreCase));

            return execution is null || !IsStageFinished(execution.Status)
                ? targetStage
                : null;
        }

        private async Task<bool> ShouldRetryAsync(
            TaskExecution failedTask,
            TaskArtifact failedArtifact,
            DecisionRequest request,
            CancellationToken cancellationToken)
        {
            var retryPolicy = ResolveRetryPolicy(failedTask, failedArtifact);
            if (ShouldSuppressRetry(failedTask))
            {
                return false;
            }

            if (retryPolicy is null || failedTask.LastAttemptNumber > retryPolicy.MaxRetries)
            {
                return false;
            }

            if (retryPolicy.RetryableErrorCodes is null || retryPolicy.RetryableErrorCodes.Count == 0)
            {
                return false;
            }

            if (await IsNonRetryableCandidateAsync(failedTask, request, cancellationToken))
            {
                return false;
            }

            var errorCode = await ResolveFailureErrorCodeAsync(failedTask, request, cancellationToken);
            return !string.IsNullOrWhiteSpace(errorCode)
                && retryPolicy.RetryableErrorCodes.Contains(errorCode, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<string> ResolveFailureErrorCodeAsync(
            TaskExecution failedTask,
            DecisionRequest request,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.ExecutionResultMetadata?.ErrorCode))
            {
                return request.ExecutionResultMetadata.ErrorCode;
            }

            var attempts = await _attemptRepository.GetByTaskExecutionId(failedTask.Id, cancellationToken);
            var lastAttempt = attempts
                .OrderByDescending(x => x.AttemptNumber)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(lastAttempt?.ErrorCode))
            {
                return lastAttempt.ErrorCode;
            }

            return TryGetString(lastAttempt?.Metadata, "ExecutionErrorCode")
                ?? TryGetString(lastAttempt?.Metadata, "PreparationErrorCode");
        }

        private async Task<bool> IsNonRetryableCandidateAsync(
            TaskExecution failedTask,
            DecisionRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ExecutionResultMetadata?.IsRetryableCandidate == false)
            {
                return true;
            }

            var attempts = await _attemptRepository.GetByTaskExecutionId(failedTask.Id, cancellationToken);
            var lastAttempt = attempts
                .OrderByDescending(x => x.AttemptNumber)
                .FirstOrDefault();

            return TryGetNullableBoolean(lastAttempt?.Metadata, "ExecutionIsRetryableCandidate") == false;
        }

        private static RetryPolicyArtifact ResolveRetryPolicy(
            TaskExecution failedTask,
            TaskArtifact failedArtifact)
        {
            if (failedTask.Status == TaskExecutionStatus.TimedOut &&
                failedArtifact?.TimeoutPolicy?.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicyArtifact reconcile)
            {
                return reconcile.RetryPolicy;
            }

            return failedArtifact?.RetryPolicy;
        }

        private static bool ShouldSuppressRetry(TaskExecution failedTask)
            => TryGetBoolean(failedTask?.Metadata, "RetrySuppressed");

        private static bool HasCompensationTerminalFailure(OrchestrationInstance instance)
            => TryGetBoolean(instance?.Metadata, CompensationTerminalFailureMetadataKey);

        private static bool ShouldContinueAfterFailure(
            TaskExecution failedTask,
            TaskArtifact failedArtifact)
        {
            if (failedArtifact?.OnErrorPolicy == OnErrorPolicy.Continue)
            {
                return true;
            }

            return failedTask.Status == TaskExecutionStatus.TimedOut &&
                failedArtifact?.TimeoutPolicy?.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicyArtifact wait &&
                wait.OrchestrationAction == OrchestrationActionOnTimeout.Continue;
        }

        private static string ResolveDeadLetterReason(
            OrchestrationInstance instance,
            TaskExecution failedTask)
        {
            if (!string.IsNullOrWhiteSpace(instance?.ErrorSummary))
            {
                return instance.ErrorSummary;
            }

            var errorCode = TryGetString(failedTask?.Metadata, "ExecutionErrorCode")
                ?? TryGetString(failedTask?.Metadata, "PreparationErrorCode")
                ?? TryGetString(failedTask?.Metadata, "TimeoutErrorCode");

            return string.IsNullOrWhiteSpace(errorCode)
                ? $"Task '{failedTask?.TaskKey}' failed and no retry, continue, or compensation policy can recover it."
                : $"Task '{failedTask?.TaskKey}' failed with '{errorCode}' and no retry, continue, or compensation policy can recover it.";
        }

        private static string TryGetString(
            IReadOnlyDictionary<string, JsonNode> metadata,
            string key)
        {
            if (metadata is null ||
                !metadata.TryGetValue(key, out var value) ||
                value is null)
            {
                return null;
            }

            return value.GetValueKind() == System.Text.Json.JsonValueKind.String
                ? value.GetValue<string>()
                : value.ToJsonString();
        }

        private async Task<IDecision> TryBuildCallbackDecision(DecisionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.MessageMetadata?.OrchestrationInstanceId) ||
                string.IsNullOrWhiteSpace(request.MessageMetadata.TaskExecutionId) ||
                string.IsNullOrWhiteSpace(request.MessageMetadata.DispatchId))
            {
                return null;
            }

            if (!TryParseId(request.MessageMetadata.OrchestrationInstanceId, out var instanceId) ||
                !TryParseId(request.MessageMetadata.TaskExecutionId, out var taskExecutionId) ||
                !TryParseId(request.MessageMetadata.DispatchId, out var dispatchId))
            {
                return null;
            }

            TaskExecution task;
            TaskDispatch dispatch;
            TaskExecutionAttempt attempt;
            OrchestrationInstance instance;
            try
            {
                instance = await _instanceRepository.GetById(instanceId, cancellationToken);
                task = await _taskRepository.GetById(taskExecutionId, cancellationToken);
                dispatch = await _dispatchRepository.GetById(dispatchId, cancellationToken);
                attempt = await _attemptRepository.GetByDispatchId(dispatchId, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }

            if (IsInstanceTerminalForCallback(instance.Status))
            {
                return null;
            }

            if (task.OrchestrationInstanceId != instanceId ||
                attempt.TaskExecutionId != task.Id ||
                dispatch.TaskExecutionAttemptId != attempt.Id ||
                (request.MessageMetadata.Attempt > 0 && request.MessageMetadata.Attempt != attempt.AttemptNumber))
            {
                return null;
            }

            if (IsCallbackResolved(task, attempt, dispatch))
            {
                return IsRecoverableLateSuccess(instance, task, attempt, dispatch, request.ExecutionResultMetadata)
                    ? BuildCompleteCallbackDecision(request, instanceId, taskExecutionId, dispatchId)
                    : null;
            }

            if (!IsCallbackReady(task, attempt, dispatch))
            {
                if (IsRecoverableLateSuccess(instance, task, attempt, dispatch, request.ExecutionResultMetadata))
                {
                    return BuildCompleteCallbackDecision(request, instanceId, taskExecutionId, dispatchId);
                }

                throw new InvalidOperationException(
                    $"Backchannel callback for dispatch '{dispatchId}' arrived before runtime state was ready. " +
                    $"Task='{task.Status}', Attempt='{attempt.Status}', Dispatch='{dispatch.DispatchStatus}'.");
            }

            return BuildCompleteCallbackDecision(request, instanceId, taskExecutionId, dispatchId);
        }

        private static CompleteCallbackDecision BuildCompleteCallbackDecision(
            DecisionRequest request,
            Id instanceId,
            Id taskExecutionId,
            Id dispatchId)
            => new(
                instanceId,
                taskExecutionId,
                dispatchId,
                request.Payload?.ToJsonString(),
                request.ExecutionResultMetadata);

        private static bool TryParseId(string value, out Id id)
        {
            id = default;
            if (string.IsNullOrWhiteSpace(value) || !Ulid.TryParse(value, out var parsed))
            {
                return false;
            }

            id = new Id(parsed);
            return true;
        }

        private static bool IsCallbackSignal(OrchestrationMessageMetadata metadata)
            => !string.IsNullOrWhiteSpace(metadata?.TaskExecutionId) ||
                !string.IsNullOrWhiteSpace(metadata?.DispatchId);

        private static bool IsRuntimeTimeoutSignal(DecisionRequest request)
            => string.Equals(request.ExecutionResultMetadata?.ErrorType, "Timeout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.ExecutionResultMetadata?.Status, "TimedOut", StringComparison.OrdinalIgnoreCase);

        private static bool IsCallbackReady(
            TaskExecution task,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch)
            => task.Status == TaskExecutionStatus.WaitingResponse &&
                attempt.Status == TaskExecutionStatus.WaitingResponse &&
                string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase);

        private static bool IsCallbackResolved(
            TaskExecution task,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch)
            => IsTerminalTaskStatus(task.Status) ||
                IsTerminalTaskStatus(attempt.Status) ||
                IsFinishedDispatchStatus(dispatch.DispatchStatus);

        private static bool IsTerminalTaskStatus(TaskExecutionStatus status)
            => status is TaskExecutionStatus.Skipped
                or TaskExecutionStatus.Completed
                or TaskExecutionStatus.CompletedWithErrors
                or TaskExecutionStatus.Failed
                or TaskExecutionStatus.TimedOut
                or TaskExecutionStatus.Cancelled
                or TaskExecutionStatus.Compensated;

        private static bool IsFinishedDispatchStatus(string status)
            => string.Equals(status, "Acknowledged", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "TimedOut", StringComparison.OrdinalIgnoreCase);

        private static bool IsStageFinished(StageExecutionStatus status)
            => status is StageExecutionStatus.Completed or StageExecutionStatus.Skipped;

        private static bool IsInstanceTerminalForCallback(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Stopped
                or OrchestrationInstanceStatus.Compensating
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Aborted;

        private static bool IsRecoverableLateSuccess(
            OrchestrationInstance instance,
            TaskExecution task,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch,
            OrchestrationExecutionResultMetadata result)
        {
            if (!IsSuccessfulCallback(result) ||
                task.Status == TaskExecutionStatus.Completed ||
                task.Status == TaskExecutionStatus.Compensated ||
                task.LastAttemptNumber > attempt.AttemptNumber)
            {
                return false;
            }

            if (instance.Status is OrchestrationInstanceStatus.DeadLettered
                or OrchestrationInstanceStatus.Failed
                or OrchestrationInstanceStatus.Running
                or OrchestrationInstanceStatus.Waiting)
            {
                return attempt.Status is TaskExecutionStatus.WaitingResponse
                        or TaskExecutionStatus.TimedOut
                        or TaskExecutionStatus.Failed
                    || IsFinishedDispatchStatus(dispatch.DispatchStatus);
            }

            return false;
        }

        private static bool IsSuccessfulCallback(OrchestrationExecutionResultMetadata result)
            => result?.Succeeded == true ||
                string.Equals(result?.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result?.Status, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result?.Status, "Success", StringComparison.OrdinalIgnoreCase);

        private static bool TryGetBoolean(
            IReadOnlyDictionary<string, JsonNode> metadata,
            string key)
            => TryGetNullableBoolean(metadata, key) == true;

        private static bool? TryGetNullableBoolean(
            IReadOnlyDictionary<string, JsonNode> metadata,
            string key)
        {
            if (metadata is null ||
                !metadata.TryGetValue(key, out var value) ||
                value is null)
            {
                return null;
            }

            try
            {
                if (value.GetValueKind() == System.Text.Json.JsonValueKind.True)
                {
                    return true;
                }

                if (value.GetValueKind() == System.Text.Json.JsonValueKind.False)
                {
                    return false;
                }

                if (value.GetValueKind() == System.Text.Json.JsonValueKind.String &&
                    bool.TryParse(value.GetValue<string>(), out var parsed))
                {
                    return parsed;
                }

                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
