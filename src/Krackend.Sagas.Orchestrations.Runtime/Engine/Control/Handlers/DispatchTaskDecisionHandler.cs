using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class DispatchTaskDecisionHandler : IDecisionHandler<DispatchTaskDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IOrchestrationPayloadContextFactory _payloadContextFactory;
        private readonly IOrchestrationConditionEvaluator _conditionEvaluator;
        private readonly ITaskAttemptDispatcher _taskAttemptDispatcher;

        public DispatchTaskDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            IExecutionTransitionRepository transitionRepository,
            IOrchestrationPayloadContextFactory payloadContextFactory,
            IOrchestrationConditionEvaluator conditionEvaluator,
            ITaskAttemptDispatcher taskAttemptDispatcher)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _payloadContextFactory = payloadContextFactory ?? throw new ArgumentNullException(nameof(payloadContextFactory));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _taskAttemptDispatcher = taskAttemptDispatcher ?? throw new ArgumentNullException(nameof(taskAttemptDispatcher));
        }

        public async Task HandleAsync(DispatchTaskDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            if (IsTerminal(instance.Status) ||
                await TaskAlreadyExistsAsync(decision.StageExecutionId, decision.Task.Key, cancellationToken))
            {
                return;
            }

            var payloadContext = _payloadContextFactory.Create(
                instance,
                decision.StageKey,
                decision.Task.Key,
                decision.MetadataDescriptors);
            var condition = await _conditionEvaluator.EvaluateAsync(
                new OrchestrationConditionEvaluationRequest
                {
                    Condition = decision.Task.ExecutionCondition,
                    PayloadContext = payloadContext,
                    ElementKey = decision.Task.Key,
                    Phase = "Task"
                },
                cancellationToken);

            if (!condition.Succeeded)
            {
                await MarkConditionFailedAsync(decision, instance, condition, now, cancellationToken);
                return;
            }

            if (!condition.ShouldExecute)
            {
                await SkipTaskAsync(decision, instance, now, cancellationToken);
                return;
            }

            await _taskAttemptDispatcher.DispatchAsync(
                new TaskAttemptDispatchRequest
                {
                    Kind = TaskAttemptDispatchKind.Initial,
                    Instance = instance,
                    StageExecutionId = decision.StageExecutionId,
                    StageKey = decision.StageKey,
                    Task = decision.Task,
                    OrchestrationExecutionPolicy = decision.OrchestrationExecutionPolicy,
                    StageExecutionPolicy = decision.StageExecutionPolicy,
                    Payload = decision.Payload,
                    MetadataDescriptors = decision.MetadataDescriptors,
                    NowUtc = now
                },
                cancellationToken);
        }

        private async Task MarkConditionFailedAsync(
            DispatchTaskDecision decision,
            OrchestrationInstance instance,
            OrchestrationConditionEvaluationResult condition,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var taskExecution = new TaskExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = decision.InstanceId,
                StageExecutionId = decision.StageExecutionId,
                TaskKey = decision.Task.Key,
                TaskKind = decision.Task.Kind,
                ExecutionMode = decision.Task.ExecutionMode,
                ParallelGroupId = decision.Task.ParallelGroupId,
                Status = TaskExecutionStatus.Failed,
                WasSkipped = false,
                SkipReason = string.Empty,
                ExecutionConditionResult = null,
                OnErrorPolicy = decision.Task.OnErrorPolicy,
                AwaitResponse = false,
                StartedOnUtc = now,
                FailedOnUtc = now,
                LastAttemptNumber = 0,
                CorrelationId = string.IsNullOrWhiteSpace(instance.CorrelationId) ? Id.New().ToString() : instance.CorrelationId
            };

            taskExecution.Metadata["ConditionErrorCode"] = JsonValue.Create(condition.ErrorCode);
            taskExecution.Metadata["ConditionErrorMessage"] = JsonValue.Create(condition.ErrorMessage);
            taskExecution.Metadata["RetrySuppressed"] = JsonValue.Create(true);
            CopyDiagnostics(taskExecution.Metadata, condition.Diagnostics, "Condition");

            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.CurrentStageKey = decision.StageKey;
            instance.CurrentTaskKey = decision.Task.Key;
            instance.FailedOnUtc = now;
            instance.ErrorSummary = condition.ErrorMessage;
            instance.LastUpdatedOnUtc = now;
            instance.WaitingSinceUtc = null;

            await _taskRepository.Create(taskExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TransitionType = "TaskConditionFailed",
                FromStatus = TaskExecutionStatus.Pending.ToString(),
                ToStatus = TaskExecutionStatus.Failed.ToString(),
                OccurredOnUtc = now,
                Message = condition.ErrorMessage,
                ProducedBy = nameof(DispatchTaskDecisionHandler)
            }, cancellationToken);
        }

        private async Task SkipTaskAsync(
            DispatchTaskDecision decision,
            OrchestrationInstance instance,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var taskExecution = new TaskExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = decision.InstanceId,
                StageExecutionId = decision.StageExecutionId,
                TaskKey = decision.Task.Key,
                TaskKind = decision.Task.Kind,
                ExecutionMode = decision.Task.ExecutionMode,
                ParallelGroupId = decision.Task.ParallelGroupId,
                Status = TaskExecutionStatus.Skipped,
                WasSkipped = true,
                SkipReason = "Execution condition evaluated to false.",
                ExecutionConditionResult = false,
                OnErrorPolicy = decision.Task.OnErrorPolicy,
                AwaitResponse = false,
                StartedOnUtc = now,
                CompletedOnUtc = now,
                LastAttemptNumber = 0,
                CorrelationId = string.IsNullOrWhiteSpace(instance.CorrelationId) ? Id.New().ToString() : instance.CorrelationId
            };

            instance.Status = OrchestrationInstanceStatus.Running;
            instance.CurrentStageKey = decision.StageKey;
            instance.CurrentTaskKey = string.Empty;
            instance.LastUpdatedOnUtc = now;
            instance.WaitingSinceUtc = null;

            await _taskRepository.Create(taskExecution, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = decision.StageExecutionId,
                TaskExecutionId = taskExecution.Id,
                TransitionType = "TaskSkipped",
                FromStatus = TaskExecutionStatus.Pending.ToString(),
                ToStatus = TaskExecutionStatus.Skipped.ToString(),
                OccurredOnUtc = now,
                Message = $"Task '{decision.Task.Key}' skipped because its execution condition evaluated to false.",
                ProducedBy = nameof(DispatchTaskDecisionHandler)
            }, cancellationToken);
        }

        private static void CopyDiagnostics(
            IDictionary<string, JsonNode> metadata,
            IReadOnlyDictionary<string, JsonNode> diagnostics,
            string prefix)
        {
            if (diagnostics is null)
            {
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                metadata[$"{prefix}.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
            }
        }

        private async Task<bool> TaskAlreadyExistsAsync(
            Id stageExecutionId,
            string taskKey,
            CancellationToken cancellationToken)
        {
            try
            {
                await _taskRepository.GetByStageAndKey(stageExecutionId, taskKey, cancellationToken);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private static bool IsTerminal(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.Failed
                or OrchestrationInstanceStatus.DeadLettered
                or OrchestrationInstanceStatus.Compensating
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Aborted
                or OrchestrationInstanceStatus.Stopped;
    }
}
