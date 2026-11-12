using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
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
            if (instance.Status == OrchestrationInstanceStatus.Compensating)
            {
                return [new CompensateInstanceDecision(
                    instance.Id,
                    request.ArtifactId,
                    _payloadState.GetDispatchPayload(instance, request.Payload)?.ToJsonString())];
            }

            if (instance.Status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Stopped
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

            var failedTasks = taskExecutions.Where(x =>
                x.Status is TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut).ToArray();
            // Failed is a deterministic terminal aggregate outcome. Transient task failures
            // remain Running until retry/continue/idle policy is evaluated below.
            if (instance.Status == OrchestrationInstanceStatus.Failed)
            {
                return [];
            }

            if (failedTasks.Length > 0)
            {
                if (HasCompensationTerminalFailure(instance))
                {
                    return [];
                }

                var resumed = BuildManualRecoveryDecision(instance, currentStage, currentStageExecution, tasks, taskExecutions,
                    dispatchPayload, metadataDescriptors, resolvedArtifact.Artifact.ExecutionPolicy);
                if (resumed is not null) return [resumed];

                var tasksByKey = tasks.ToLookup(x => x.Key, StringComparer.Ordinal);
                foreach (var candidate in failedTasks)
                {
                    if (TryGetBoolean(candidate.Metadata, ParallelCancellationRecovery.ResumeFailedMetadataKey)) continue;
                    var candidateArtifact = tasksByKey[candidate.TaskKey].FirstOrDefault();
                    if (!await ShouldRetryAsync(candidate, candidateArtifact, request, cancellationToken)) continue;
                    return [new RetryDecision(instance.Id, currentStageExecution.Id, candidate.Id, currentStage.Key,
                        candidateArtifact, dispatchPayload?.ToJsonString())
                    {
                        MetadataDescriptors = metadataDescriptors,
                        OrchestrationExecutionPolicy = resolvedArtifact.Artifact.ExecutionPolicy,
                        StageExecutionPolicy = currentStage.ExecutionPolicy
                    }];
                }

                // A failed member must not stop work already running in its parallel group.
                // Also dispatch members that have not started when a concurrency limit split
                // the group into batches. Later stages remain blocked until the join resolves.
                foreach (var groupId in failedTasks.Select(f => tasksByKey[f.TaskKey].FirstOrDefault()?.ParallelGroupId)
                    .Where(id => id.HasValue).Distinct())
                {
                    var members = tasks.Where(t => t.ParallelGroupId == groupId).ToArray();
                    var memberKeys = members.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
                    var executions = taskExecutions.Where(t => memberKeys.Contains(t.TaskKey)).ToArray();
                    if (executions.Any(t => t.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying))
                        return [];
                    var existingKeys = executions.Select(t => t.TaskKey).ToHashSet(StringComparer.Ordinal);
                    var unstarted = members.Where(t => !existingKeys.Contains(t.Key)).ToArray();
                    if (unstarted.Length > 0)
                    {
                        var limit = currentStage.ParallelGroups.FirstOrDefault(g => g.Id == groupId.Value)?.MaxParallelAgents ?? int.MaxValue;
                        var serializedPayload = dispatchPayload?.ToJsonString();
                        return unstarted.Take(limit).Select(task => (IDecision)new DispatchTaskDecision(
                            instance.Id, currentStageExecution.Id, currentStage.Key, task, serializedPayload)
                        {
                            MetadataDescriptors = metadataDescriptors,
                            OrchestrationExecutionPolicy = resolvedArtifact.Artifact.ExecutionPolicy,
                            StageExecutionPolicy = currentStage.ExecutionPolicy
                        }).ToArray();
                    }
                    if (executions.Any(t => t.Status is TaskExecutionStatus.Pending or TaskExecutionStatus.Cancelled)) return [];
                }

                var failedTask = failedTasks.FirstOrDefault(f => !ShouldContinueAfterFailure(f, tasksByKey[f.TaskKey].FirstOrDefault()));
                if (failedTask is null)
                {
                    if (taskExecutions.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying or TaskExecutionStatus.Pending or TaskExecutionStatus.Cancelled)) return [];
                    return BuildNextTaskDecisions(instance, stages, currentStage, currentStageExecution, tasks, taskExecutions,
                        dispatchPayload, metadataDescriptors, resolvedArtifact.Artifact.ExecutionPolicy);
                }
                var failedArtifact = tasksByKey[failedTask.TaskKey].FirstOrDefault();
                if (failedArtifact?.OnErrorPolicy == OnErrorPolicy.StopAndCompensate)
                {
                    return [new CompensateInstanceDecision(instance.Id, request.ArtifactId, dispatchPayload?.ToJsonString())];
                }

                // Only an unanswered command is uncertain enough to retain the saga in
                // its reconciliation window. A callback failure is an explicit terminal
                // outcome once its retry policy has been exhausted.
                if (failedTask.Status == TaskExecutionStatus.TimedOut)
                {
                    return [new EnterIdleInstanceDecision(
                        instance.Id,
                        currentStageExecution.Id,
                        failedTask.Id,
                        ResolveDeadLetterReason(instance, failedTask))];
                }

                return [new DeadLetterInstanceDecision(
                    instance.Id,
                    currentStageExecution.Id,
                    failedTask.Id,
                    ResolveDeadLetterReason(instance, failedTask))];
            }

            var recovery = BuildManualRecoveryDecision(instance, currentStage, currentStageExecution, tasks, taskExecutions,
                dispatchPayload, metadataDescriptors, resolvedArtifact.Artifact.ExecutionPolicy);
            if (recovery is not null) return [recovery];
            if (taskExecutions.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying or TaskExecutionStatus.Pending or TaskExecutionStatus.Cancelled))
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

        private static RetryDecision BuildManualRecoveryDecision(
            OrchestrationInstance instance, StageArtifact stage, StageExecution stageExecution,
            IReadOnlyCollection<TaskArtifact> tasks, IReadOnlyCollection<TaskExecution> executions, JsonNode payload,
            IReadOnlyCollection<MetadataDescriptorArtifact> metadataDescriptors, ExecutionPolicyArtifact policy)
        {
            foreach (var execution in executions.Where(x =>
                (x.Status == TaskExecutionStatus.Cancelled && TryGetBoolean(x.Metadata, ParallelCancellationRecovery.ResumeMetadataKey)) ||
                (x.Status is (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut) && TryGetBoolean(x.Metadata, ParallelCancellationRecovery.ResumeFailedMetadataKey))))
            {
                var task = tasks.FirstOrDefault(x => x.Key == execution.TaskKey);
                if (task?.ParallelGroupId is not { } groupId || execution.ParallelGroupId != groupId) continue;
                var earlierWorkIncomplete = tasks.TakeWhile(x => x.Id != task.Id)
                    .Where(x => x.ParallelGroupId != groupId)
                    .Any(earlier =>
                    {
                        var prior = executions.FirstOrDefault(x => x.TaskKey == earlier.Key);
                        return prior is null || !(prior.Status is (TaskExecutionStatus.Completed or TaskExecutionStatus.Skipped or
                            TaskExecutionStatus.CompletedWithErrors or TaskExecutionStatus.Compensated) ||
                            (prior.Status is (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut) && ShouldContinueAfterFailure(prior, earlier)));
                    });
                if (earlierWorkIncomplete) continue;
                var limit = stage.ParallelGroups.FirstOrDefault(x => x.Id == groupId)?.MaxParallelAgents ?? int.MaxValue;
                var active = executions.Count(x => x.ParallelGroupId == groupId &&
                    x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse or TaskExecutionStatus.Retrying);
                if (active >= limit) continue;
                return new RetryDecision(instance.Id, stageExecution.Id, execution.Id, stage.Key, task, payload?.ToJsonString())
                {
                    ResumeManual = true,
                    MetadataDescriptors = metadataDescriptors,
                    OrchestrationExecutionPolicy = policy,
                    StageExecutionPolicy = stage.ExecutionPolicy
                };
            }
            return null;
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
            var executedTaskKeys = taskExecutions.Select(execution => execution.TaskKey).ToHashSet(StringComparer.Ordinal);
            var nextTask = tasks.FirstOrDefault(task => !executedTaskKeys.Contains(task.Key));
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
                .Where(task => !executedTaskKeys.Contains(task.Key))
                .Take(maxParallelAgents)
                .ToArray();

            if (groupTasks.Length == 0)
                return [new CompleteStageDecision(instance.Id, currentStageExecution.Id, currentStage, stages)
                {
                    MetadataDescriptors = metadataDescriptors
                }];

            var serializedPayload = dispatchPayload?.ToJsonString();
            return groupTasks.Select(task => new DispatchTaskDecision(
                    instance.Id,
                    currentStageExecution.Id,
                    currentStage.Key,
                    task,
                    serializedPayload)
                {
                    MetadataDescriptors = metadataDescriptors,
                    OrchestrationExecutionPolicy = orchestrationExecutionPolicy,
                    StageExecutionPolicy = currentStage.ExecutionPolicy
                })
                .Cast<IDecision>()
                .ToArray();
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

            // A response timeout has no service-provided error code to classify. Once a task is
            // configured with a retry budget, each timed-out attempt consumes that budget.
            // Retryable error codes remain the policy for failures reported by the service.
            if (failedTask.Status == TaskExecutionStatus.TimedOut)
            {
                return true;
            }

            if (retryPolicy.RetryableErrorCodes is null || retryPolicy.RetryableErrorCodes.Count == 0)
            {
                return false;
            }

            var requestResult = string.IsNullOrWhiteSpace(request.MessageMetadata?.TaskExecutionId) ||
                string.Equals(request.MessageMetadata.TaskExecutionId, failedTask.Id.ToString(), StringComparison.OrdinalIgnoreCase)
                ? request.ExecutionResultMetadata : null;
            if (requestResult?.IsRetryableCandidate == false)
            {
                return false;
            }
            var attempts = await _attemptRepository.GetByTaskExecutionId(failedTask.Id, cancellationToken);
            var lastAttempt = attempts.OrderByDescending(x => x.AttemptNumber).FirstOrDefault();
            if (TryGetNullableBoolean(lastAttempt?.Metadata, "ExecutionIsRetryableCandidate") == false)
                return false;
            var errorCode = !string.IsNullOrWhiteSpace(requestResult?.ErrorCode) ? requestResult.ErrorCode
                : !string.IsNullOrWhiteSpace(lastAttempt?.ErrorCode) ? lastAttempt.ErrorCode
                : TryGetString(lastAttempt?.Metadata, "ExecutionErrorCode") ?? TryGetString(lastAttempt?.Metadata, "PreparationErrorCode");
            return !string.IsNullOrWhiteSpace(errorCode) && retryPolicy.RetryableErrorCodes.Contains(errorCode, StringComparer.OrdinalIgnoreCase);
        }

        private static RetryPolicyArtifact ResolveRetryPolicy(
            TaskExecution failedTask,
            TaskArtifact failedArtifact)
        {
            if (failedTask.Status == TaskExecutionStatus.TimedOut &&
                failedArtifact?.TimeoutPolicy?.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicyArtifact reconcile)
            {
                return reconcile.RetryPolicy ?? failedArtifact.RetryPolicy;
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
            if (instance is not null && !string.IsNullOrWhiteSpace(instance.ErrorSummary))
            {
                return instance.ErrorSummary;
            }

            var taskKey = failedTask is null ? string.Empty : failedTask.TaskKey;
            var metadata = failedTask is null ? null : failedTask.Metadata;
            var errorCode = TryGetString(metadata, "ExecutionErrorCode")
                ?? TryGetString(metadata, "PreparationErrorCode")
                ?? TryGetString(metadata, "TimeoutErrorCode");

            return string.IsNullOrWhiteSpace(errorCode)
                ? $"Task '{taskKey}' failed and no retry, continue, or compensation policy can recover it."
                : $"Task '{taskKey}' failed with '{errorCode}' and no retry, continue, or compensation policy can recover it.";
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

            // A dead-lettered saga may still receive the result of a parallel command
            // that was already sent. Record that exact outstanding attempt without
            // resuming the saga; Recover can then reuse its actual response payload.
            var lateParallelResult = instance.Status == OrchestrationInstanceStatus.DeadLettered &&
                task.ParallelGroupId.HasValue && IsCallbackReady(task, attempt, dispatch) &&
                task.LastAttemptNumber == attempt.AttemptNumber;
            var cancelledParallelResult = IsCancelledParallelCallback(instance, task, attempt, dispatch);
            if (IsInstanceTerminalForCallback(instance.Status) && !lateParallelResult && !cancelledParallelResult)
            {
                return null;
            }

            if (task.OrchestrationInstanceId != instanceId ||
                attempt.TaskExecutionId != task.Id ||
                dispatch.TaskExecutionAttemptId != attempt.Id ||
                (task.LastAttemptNumber > 0 && task.LastAttemptNumber != attempt.AttemptNumber) ||
                (request.MessageMetadata.Attempt > 0 && request.MessageMetadata.Attempt != attempt.AttemptNumber))
            {
                return null;
            }

            if (cancelledParallelResult)
                return BuildCompleteCallbackDecision(request, instanceId, taskExecutionId, dispatchId);

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

        internal static bool IsCancelledParallelCallback(OrchestrationInstance instance, TaskExecution task,
            TaskExecutionAttempt attempt, TaskDispatch dispatch)
            => instance.Status is (OrchestrationInstanceStatus.DeadLettered or OrchestrationInstanceStatus.Idle or OrchestrationInstanceStatus.Running) &&
                task.ParallelGroupId.HasValue && task.Status == TaskExecutionStatus.Cancelled &&
                attempt.Status == TaskExecutionStatus.Cancelled && task.LastAttemptNumber == attempt.AttemptNumber &&
                string.Equals(dispatch.DispatchStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                dispatch.Metadata?.ContainsKey("Recovery.DispatchCancellationReason") == true;

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
                or OrchestrationInstanceStatus.Failed
                or OrchestrationInstanceStatus.DeadLettered
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

            if (instance.Status is OrchestrationInstanceStatus.Idle
                or OrchestrationInstanceStatus.Running)
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
    }
}
