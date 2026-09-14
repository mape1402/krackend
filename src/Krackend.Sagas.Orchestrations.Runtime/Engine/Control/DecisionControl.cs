using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
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
            if (instance.Status is OrchestrationInstanceStatus.Completed or OrchestrationInstanceStatus.Compensating or OrchestrationInstanceStatus.Compensated)
            {
                return [];
            }

            var stages = resolvedArtifact.Artifact.StageDefinitions.OrderBy(x => x.Order).ToArray();
            var stageExecutions = await _stageRepository.GetByInstanceId(instance.Id, cancellationToken);
            var currentStage = ResolveCurrentStage(stages, stageExecutions);
            if (currentStage is null)
            {
                return [new CompleteInstanceDecision(instance.Id)];
            }

            var currentStageExecution = stageExecutions.FirstOrDefault(x => x.StageKey == currentStage.Key);
            if (currentStageExecution is null)
            {
                return [new StartStageDecision(instance.Id, currentStage, request.Payload?.ToJsonString())];
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
            if (failedTask is not null)
            {
                var failedArtifact = tasks.FirstOrDefault(x => x.Key == failedTask.TaskKey);
                if (await ShouldRetryAsync(failedTask, failedArtifact, request, cancellationToken))
                {
                    return [new RetryDecision(
                        instance.Id,
                        currentStageExecution.Id,
                        failedTask.Id,
                        currentStage.Key,
                        failedArtifact,
                        dispatchPayload?.ToJsonString())];
                }

                if (failedArtifact?.OnErrorPolicy == OnErrorPolicy.StopAndCompensate)
                {
                    return [new CompensateInstanceDecision(instance.Id, request.ArtifactId, dispatchPayload?.ToJsonString())];
                }

                if (ShouldContinueAfterFailure(failedTask, failedArtifact))
                {
                    return BuildNextTaskDecisions(instance, currentStage, currentStageExecution, tasks, taskExecutions, dispatchPayload);
                }

                return [];
            }

            if (taskExecutions.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse))
            {
                return [];
            }

            return BuildNextTaskDecisions(instance, currentStage, currentStageExecution, tasks, taskExecutions, dispatchPayload);
        }

        private static IReadOnlyCollection<IDecision> BuildNextTaskDecisions(
            OrchestrationInstance instance,
            StageArtifact currentStage,
            StageExecution currentStageExecution,
            IReadOnlyCollection<TaskArtifact> tasks,
            IReadOnlyCollection<TaskExecution> taskExecutions,
            JsonNode dispatchPayload)
        {
            var nextTask = tasks.FirstOrDefault(task =>
                taskExecutions.All(execution => execution.TaskKey != task.Key));
            if (nextTask is null)
            {
                return [new CompleteStageDecision(instance.Id, currentStageExecution.Id)];
            }

            if (nextTask.ParallelGroupId is null)
            {
                return [new DispatchTaskDecision(
                    instance.Id,
                    currentStageExecution.Id,
                    currentStage.Key,
                    nextTask,
                    dispatchPayload?.ToJsonString())];
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
                    dispatchPayload?.ToJsonString()))
                .Cast<IDecision>()
                .ToArray();

            return groupTasks.Length == 0 ? [new CompleteStageDecision(instance.Id, currentStageExecution.Id)] : groupTasks;
        }

        private static StageArtifact ResolveCurrentStage(
            IReadOnlyCollection<StageArtifact> stages,
            IReadOnlyCollection<StageExecution> stageExecutions)
        {
            foreach (var stage in stages)
            {
                var execution = stageExecutions.FirstOrDefault(x => x.StageKey == stage.Key);
                if (execution is null || execution.Status != StageExecutionStatus.Completed)
                {
                    return stage;
                }
            }

            return null;
        }

        private async Task<bool> ShouldRetryAsync(
            TaskExecution failedTask,
            TaskArtifact failedArtifact,
            DecisionRequest request,
            CancellationToken cancellationToken)
        {
            var retryPolicy = ResolveRetryPolicy(failedTask, failedArtifact);
            if (retryPolicy is null || failedTask.LastAttemptNumber > retryPolicy.MaxRetries)
            {
                return false;
            }

            if (retryPolicy.RetryableErrorCodes is null || retryPolicy.RetryableErrorCodes.Count == 0)
            {
                return true;
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
            try
            {
                task = await _taskRepository.GetById(taskExecutionId, cancellationToken);
                dispatch = await _dispatchRepository.GetById(dispatchId, cancellationToken);
                attempt = await _attemptRepository.GetByDispatchId(dispatchId, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }

            if (task.Status != TaskExecutionStatus.WaitingResponse ||
                task.OrchestrationInstanceId != instanceId ||
                attempt.Status != TaskExecutionStatus.WaitingResponse ||
                !string.Equals(dispatch.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase) ||
                attempt.TaskExecutionId != task.Id ||
                dispatch.TaskExecutionAttemptId != attempt.Id ||
                (request.MessageMetadata.Attempt > 0 && request.MessageMetadata.Attempt != attempt.AttemptNumber))
            {
                return null;
            }

            var payload = request.Payload?.ToJsonString();

            return new CompleteCallbackDecision(
                instanceId,
                taskExecutionId,
                dispatchId,
                payload,
                request.ExecutionResultMetadata);
        }

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
    }
}
