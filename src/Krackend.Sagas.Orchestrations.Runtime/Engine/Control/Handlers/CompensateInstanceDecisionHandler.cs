using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompensateInstanceDecisionHandler : IDecisionHandler<CompensateInstanceDecision>
    {
        private const string CompensationTerminalFailureMetadataKey = "Compensation.TerminalFailure";

        private readonly IRuntimeArtifactResolver _artifactResolver;
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ICompensationExecutionRepository _compensationRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IRemoteCommandDispatcher _dispatcher;
        private readonly ITaskRuntimeAdapterRegistry _adapterRegistry;
        private readonly IOrchestrationConditionEvaluator _conditionEvaluator;
        private readonly IOrchestrationPayloadContextFactory _payloadContextFactory;
        private readonly IOrchestrationPropagationMetadataStore _propagationMetadataStore;

        public CompensateInstanceDecisionHandler(
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            ITaskExecutionRepository taskRepository,
            ICompensationExecutionRepository compensationRepository,
            IExecutionTransitionRepository transitionRepository,
            IRemoteCommandDispatcher dispatcher,
            ITaskRuntimeAdapterRegistry adapterRegistry,
            IOrchestrationConditionEvaluator conditionEvaluator,
            IOrchestrationPayloadContextFactory payloadContextFactory,
            IOrchestrationPropagationMetadataStore propagationMetadataStore = null)
        {
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _payloadContextFactory = payloadContextFactory ?? throw new ArgumentNullException(nameof(payloadContextFactory));
            _propagationMetadataStore = propagationMetadataStore ?? new DefaultOrchestrationPropagationMetadataStore();
        }

        public async Task HandleAsync(CompensateInstanceDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var completedTasks = (await _taskRepository.GetByInstanceId(decision.InstanceId, cancellationToken))
                .Where(x => x.Status == TaskExecutionStatus.Completed)
                .OrderByDescending(x => x.CompletedOnUtc)
                .ToArray();
            var resolvedArtifact = await _artifactResolver.ResolveAsync(decision.ArtifactId, cancellationToken);
            var taskArtifacts = resolvedArtifact.Artifact.StageDefinitions
                .SelectMany(x => x.TaskDefinitions)
                .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

            instance.Status = OrchestrationInstanceStatus.Compensating;
            instance.CompensationStartedOnUtc = now;
            instance.LastUpdatedOnUtc = now;
            await _instanceRepository.Update(instance, cancellationToken);

            foreach (var task in completedTasks)
            {
                if (!taskArtifacts.TryGetValue(task.TaskKey, out var taskArtifact))
                {
                    continue;
                }

                if (taskArtifact.Compensation is null || taskArtifact.Compensation.Configuration is null)
                {
                    continue;
                }

                var stage = await _stageRepository.GetById(task.StageExecutionId, cancellationToken);
                var adapter = _adapterRegistry.TryGet(taskArtifact.Compensation.CompensationTaskKind, out var resolvedAdapter)
                    ? resolvedAdapter
                    : null;
                var condition = await _conditionEvaluator.EvaluateAsync(
                    new OrchestrationConditionEvaluationRequest
                    {
                        Condition = taskArtifact.Compensation.ExecutionCondition,
                        PayloadContext = _payloadContextFactory.Create(
                            instance,
                            stage.StageKey,
                            task.TaskKey,
                            resolvedArtifact.Artifact.MetadataDescriptors),
                        ElementKey = task.TaskKey,
                        Phase = "Compensation"
                    },
                    cancellationToken);
                var compensation = new CompensationExecution
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    SourceTaskExecutionId = task.Id,
                    CompensationTaskKey = ResolveCompensationTaskKey(adapter, taskArtifact.Compensation),
                    Status = "Running",
                    StartedOnUtc = DateTime.UtcNow,
                    RequestPayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : JsonNode.Parse(decision.Payload)
                };
                await _compensationRepository.Create(compensation, cancellationToken);

                if (!condition.Succeeded)
                {
                    await MarkCompensationConditionFailedAsync(
                        instance,
                        task,
                        compensation,
                        condition,
                        cancellationToken);
                    return;
                }

                if (!condition.ShouldExecute)
                {
                    await SkipCompensationAsync(instance, task, compensation, cancellationToken);
                    continue;
                }

                if (adapter is null)
                {
                    await MarkCompensationFailedAsync(
                        instance,
                        task,
                        compensation,
                        new InvalidOperationException($"No runtime task adapter is configured for compensation task kind '{taskArtifact.Compensation.CompensationTaskKind}'."),
                        "CompensationTaskRuntimeAdapterNotConfigured",
                        cancellationToken);
                    return;
                }

                try
                {
                    var descriptor = await adapter.BuildCompensationCommandAsync(
                        new TaskRuntimeCompensationCommandRequest
                        {
                            Instance = instance,
                            SourceTaskExecution = task,
                            CompensationExecution = compensation,
                            Compensation = taskArtifact.Compensation,
                            Payload = decision.Payload
                        },
                        cancellationToken);
                    var propagationMetadata = _propagationMetadataStore.Load(instance);
                    var messageMetadata = new OrchestrationMessageMetadata
                    {
                        SagaId = GetSagaId(instance),
                        OrchestrationInstanceId = instance.Id.ToString(),
                        CurrentStage = stage.StageKey,
                        CurrentTasks = [task.TaskKey],
                        CorrelationId = instance.CorrelationId,
                        TaskExecutionId = task.Id.ToString(),
                        ReplyAddress = descriptor.ReplyAddress
                    };

                    await _dispatcher.DispatchAsync(new RemoteCommand
                    {
                        Payload = decision.Payload,
                        RemoteCommandTransport = descriptor.Transport,
                        SettingsPayload = descriptor.SettingsPayload,
                        OrchestrationInstanceId = instance.Id.ToString(),
                        StageExecutionId = task.StageExecutionId.ToString(),
                        TaskExecutionId = task.Id.ToString(),
                        TaskKey = task.TaskKey,
                        AwaitResponse = false,
                        PropagationMetadata = propagationMetadata,
                        MessageMetadata = messageMetadata
                    }, cancellationToken);
                }
                catch (Exception exception)
                {
                    await MarkCompensationFailedAsync(
                        instance,
                        task,
                        compensation,
                        exception,
                        "CompensationDispatchFailed",
                        cancellationToken);
                    return;
                }

                compensation.Status = "Completed";
                compensation.CompletedOnUtc = DateTime.UtcNow;
                await _compensationRepository.Update(compensation, cancellationToken);
                await _transitionRepository.Create(new ExecutionTransition
                {
                    Id = Id.New(),
                    OrchestrationInstanceId = instance.Id,
                    StageExecutionId = task.StageExecutionId,
                    TaskExecutionId = task.Id,
                    TransitionType = "CompensationCompleted",
                    FromStatus = "Running",
                    ToStatus = "Completed",
                    OccurredOnUtc = compensation.CompletedOnUtc.Value,
                    Message = $"Compensation '{compensation.CompensationTaskKey}' completed for task '{task.TaskKey}'.",
                    ProducedBy = nameof(CompensateInstanceDecisionHandler)
                }, cancellationToken);
            }

            instance.Status = OrchestrationInstanceStatus.Compensated;
            instance.CompensatedOnUtc = DateTime.UtcNow;
            instance.LastUpdatedOnUtc = instance.CompensatedOnUtc.Value;
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                TransitionType = "InstanceCompensated",
                FromStatus = OrchestrationInstanceStatus.Compensating.ToString(),
                ToStatus = OrchestrationInstanceStatus.Compensated.ToString(),
                OccurredOnUtc = instance.CompensatedOnUtc.Value,
                Message = "Compensation executed in reverse order for completed tasks.",
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private static string GetSagaId(OrchestrationInstance instance)
            => string.IsNullOrWhiteSpace(instance.SagaId)
                ? instance.Id.ToString()
                : instance.SagaId;

        private static string ResolveCompensationTaskKey(
            ITaskRuntimeAdapter adapter,
            CompensationArtifact compensation)
        {
            if (adapter is null)
            {
                return compensation.CompensationTaskKind.ToString();
            }

            try
            {
                return adapter.GetCompensationDestination(compensation);
            }
            catch (InvalidOperationException)
            {
                return compensation.CompensationTaskKind.ToString();
            }
        }

        private async Task MarkCompensationConditionFailedAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            CompensationExecution compensation,
            OrchestrationConditionEvaluationResult condition,
            CancellationToken cancellationToken)
        {
            compensation.Status = "Failed";
            compensation.FailedOnUtc = DateTime.UtcNow;
            compensation.ErrorMessage = condition.ErrorMessage;
            compensation.Metadata["ConditionErrorCode"] = JsonValue.Create(condition.ErrorCode);
            compensation.Metadata["ConditionErrorMessage"] = JsonValue.Create(condition.ErrorMessage);
            CopyDiagnostics(compensation.Metadata, condition.Diagnostics);

            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = compensation.FailedOnUtc;
            instance.ErrorSummary = condition.ErrorMessage;
            instance.LastUpdatedOnUtc = compensation.FailedOnUtc.Value;
            instance.Metadata[CompensationTerminalFailureMetadataKey] = JsonValue.Create(true);

            await _compensationRepository.Update(compensation, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TransitionType = "CompensationConditionFailed",
                FromStatus = "Running",
                ToStatus = "Failed",
                OccurredOnUtc = compensation.FailedOnUtc.Value,
                Message = condition.ErrorMessage,
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private async Task SkipCompensationAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            CompensationExecution compensation,
            CancellationToken cancellationToken)
        {
            compensation.Status = "Skipped";
            compensation.CompletedOnUtc = DateTime.UtcNow;
            compensation.Metadata["ConditionResult"] = JsonValue.Create(false);

            await _compensationRepository.Update(compensation, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TransitionType = "CompensationSkipped",
                FromStatus = "Running",
                ToStatus = "Skipped",
                OccurredOnUtc = compensation.CompletedOnUtc.Value,
                Message = $"Compensation '{compensation.CompensationTaskKey}' skipped because its execution condition evaluated to false.",
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private async Task MarkCompensationFailedAsync(
            OrchestrationInstance instance,
            TaskExecution task,
            CompensationExecution compensation,
            Exception exception,
            string errorCode,
            CancellationToken cancellationToken)
        {
            var failedOnUtc = DateTime.UtcNow;
            compensation.Status = "Failed";
            compensation.FailedOnUtc = failedOnUtc;
            compensation.ErrorMessage = exception.Message;
            compensation.Metadata["ExecutionErrorCode"] = JsonValue.Create(errorCode);
            compensation.Metadata["ExecutionErrorMessage"] = JsonValue.Create(exception.Message);

            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = failedOnUtc;
            instance.ErrorSummary = exception.Message;
            instance.LastUpdatedOnUtc = failedOnUtc;
            instance.Metadata[CompensationTerminalFailureMetadataKey] = JsonValue.Create(true);

            await _compensationRepository.Update(compensation, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TransitionType = "CompensationFailed",
                FromStatus = "Running",
                ToStatus = "Failed",
                OccurredOnUtc = failedOnUtc,
                Message = exception.Message,
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private static void CopyDiagnostics(
            IDictionary<string, JsonNode> metadata,
            IReadOnlyDictionary<string, JsonNode> diagnostics)
        {
            if (diagnostics is null)
            {
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                metadata[$"Condition.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
            }
        }
    }
}
