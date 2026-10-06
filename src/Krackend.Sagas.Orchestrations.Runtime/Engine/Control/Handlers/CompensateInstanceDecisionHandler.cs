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
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
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
        private readonly IOrchestrationTransformationExecutor _transformationExecutor;
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
            IOrchestrationTransformationExecutor transformationExecutor,
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
            _transformationExecutor = transformationExecutor ?? throw new ArgumentNullException(nameof(transformationExecutor));
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
                var dispatched = await ExecuteCompensationAsync(
                    instance,
                    resolvedArtifact,
                    stage.StageKey,
                    task.StageExecutionId,
                    task.TaskKey,
                    task.Id,
                    task,
                    taskArtifact with
                    {
                        Kind = taskArtifact.Compensation.CompensationTaskKind,
                        Transformation = taskArtifact.Compensation.Transformation,
                        Configuration = taskArtifact.Compensation.Configuration,
                        DispatchType = taskArtifact.Compensation.DispatchType,
                        OnErrorPolicy = ResolveCompensationOnErrorPolicy(taskArtifact.Compensation.OnErrorPolicy)
                    },
                    taskArtifact.Compensation,
                    "Task",
                    decision.Payload,
                    cancellationToken);
                if (!dispatched)
                {
                    return;
                }
            }

            var trigger = resolvedArtifact.Artifact.TriggerBindings?
                .FirstOrDefault(x =>
                    x.IsEnabled &&
                    x.TriggerChannel is EventTriggerChannelArtifact &&
                    x.Compensation?.Configuration is not null);
            if (trigger is not null)
            {
                var triggerKey = $"trigger:{trigger.Id}";
                var dispatched = await ExecuteCompensationAsync(
                    instance,
                    resolvedArtifact,
                    "trigger",
                    null,
                    triggerKey,
                    instance.Id,
                    null,
                    CreateTriggerCompensationTask(trigger, triggerKey),
                    trigger.Compensation,
                    "Trigger",
                    decision.Payload,
                    cancellationToken);
                if (!dispatched)
                {
                    return;
                }
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

        private async Task<bool> ExecuteCompensationAsync(
            OrchestrationInstance instance,
            ResolvedOrchestrationArtifact resolvedArtifact,
            string stageKey,
            Id? stageExecutionId,
            string sourceElementKey,
            Id sourceTaskExecutionId,
            TaskExecution sourceTaskExecution,
            TaskArtifact transformationTask,
            CompensationArtifact compensationArtifact,
            string sourceElementType,
            string fallbackPayload,
            CancellationToken cancellationToken)
        {
            var adapter = _adapterRegistry.TryGet(compensationArtifact.CompensationTaskKind, out var resolvedAdapter)
                ? resolvedAdapter
                : null;
            var payloadContext = _payloadContextFactory.Create(
                instance,
                stageKey,
                sourceElementKey,
                resolvedArtifact.Artifact.MetadataDescriptors);
            var condition = await _conditionEvaluator.EvaluateAsync(
                new OrchestrationConditionEvaluationRequest
                {
                    Condition = compensationArtifact.ExecutionCondition,
                    PayloadContext = payloadContext,
                    ElementKey = sourceElementKey,
                    Phase = sourceElementType == "Trigger" ? "TriggerCompensation" : "Compensation"
                },
                cancellationToken);
            var compensation = new CompensationExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                SourceTaskExecutionId = sourceTaskExecutionId,
                CompensationTaskKey = ResolveCompensationTaskKey(adapter, compensationArtifact),
                Status = "Running",
                StartedOnUtc = DateTime.UtcNow,
                RequestPayload = ParsePayload(fallbackPayload)
            };
            compensation.Metadata["SourceElementType"] = JsonValue.Create(sourceElementType);
            compensation.Metadata["SourceElementKey"] = JsonValue.Create(sourceElementKey);
            await _compensationRepository.Create(compensation, cancellationToken);

            if (!condition.Succeeded)
            {
                await MarkCompensationConditionFailedAsync(
                    instance,
                    stageExecutionId,
                    sourceTaskExecutionId,
                    compensation,
                    condition,
                    IsTerminalCompensationFailure(compensationArtifact),
                    cancellationToken);
                return ShouldContinueAfterCompensationFailure(compensationArtifact);
            }

            if (!condition.ShouldExecute)
            {
                await SkipCompensationAsync(
                    instance,
                    stageExecutionId,
                    sourceTaskExecutionId,
                    sourceElementKey,
                    compensation,
                    cancellationToken);
                return true;
            }

            if (adapter is null)
            {
                await MarkCompensationFailedAsync(
                    instance,
                    stageExecutionId,
                    sourceTaskExecutionId,
                    compensation,
                    new InvalidOperationException($"No runtime task adapter is configured for compensation task kind '{compensationArtifact.CompensationTaskKind}'."),
                    "CompensationTaskRuntimeAdapterNotConfigured",
                    IsTerminalCompensationFailure(compensationArtifact),
                    cancellationToken);
                return ShouldContinueAfterCompensationFailure(compensationArtifact);
            }

            var payloadPreparation = await PrepareCompensationPayloadAsync(
                transformationTask,
                payloadContext,
                compensationArtifact,
                fallbackPayload,
                cancellationToken);
            if (!payloadPreparation.Succeeded)
            {
                await MarkCompensationTransformationFailedAsync(
                    instance,
                    stageExecutionId,
                    sourceTaskExecutionId,
                    compensation,
                    payloadPreparation,
                    IsTerminalCompensationFailure(compensationArtifact),
                    cancellationToken);
                return ShouldContinueAfterCompensationFailure(compensationArtifact);
            }

            compensation.RequestPayload = payloadPreparation.Payload?.DeepClone();
            await _compensationRepository.Update(compensation, cancellationToken);

            try
            {
                var descriptor = await adapter.BuildCompensationCommandAsync(
                    new TaskRuntimeCompensationCommandRequest
                    {
                        Instance = instance,
                        SourceTaskExecution = sourceTaskExecution,
                        CompensationExecution = compensation,
                        Compensation = compensationArtifact,
                        Payload = payloadPreparation.PayloadText
                    },
                    cancellationToken);
                var propagationMetadata = _propagationMetadataStore.Load(instance);
                var messageMetadata = new OrchestrationMessageMetadata
                {
                    SagaId = GetSagaId(instance),
                    OrchestrationInstanceId = instance.Id.ToString(),
                    CurrentStage = stageKey,
                    CurrentTasks = [sourceElementKey],
                    CorrelationId = instance.CorrelationId,
                    TaskExecutionId = sourceTaskExecutionId.ToString(),
                    ReplyAddress = descriptor.ReplyAddress
                };

                await _dispatcher.DispatchAsync(new RemoteCommand
                {
                    Payload = payloadPreparation.PayloadText,
                    RemoteCommandTransport = descriptor.Transport,
                    SettingsPayload = descriptor.SettingsPayload,
                    OrchestrationInstanceId = instance.Id.ToString(),
                    StageExecutionId = stageExecutionId?.ToString(),
                    TaskExecutionId = sourceTaskExecutionId.ToString(),
                    TaskKey = sourceElementKey,
                    AwaitResponse = false,
                    PropagationMetadata = propagationMetadata,
                    MessageMetadata = messageMetadata
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                await MarkCompensationFailedAsync(
                    instance,
                    stageExecutionId,
                    sourceTaskExecutionId,
                    compensation,
                    exception,
                    "CompensationDispatchFailed",
                    IsTerminalCompensationFailure(compensationArtifact),
                    cancellationToken);
                return ShouldContinueAfterCompensationFailure(compensationArtifact);
            }

            compensation.Status = "Completed";
            compensation.CompletedOnUtc = DateTime.UtcNow;
            await _compensationRepository.Update(compensation, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecutionId,
                TaskExecutionId = sourceTaskExecutionId,
                TransitionType = sourceElementType == "Trigger" ? "TriggerCompensationCompleted" : "CompensationCompleted",
                FromStatus = "Running",
                ToStatus = "Completed",
                OccurredOnUtc = compensation.CompletedOnUtc.Value,
                Message = $"Compensation '{compensation.CompensationTaskKey}' completed for {sourceElementType.ToLowerInvariant()} '{sourceElementKey}'.",
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);

            return true;
        }

        private async Task<CompensationPayloadPreparationResult> PrepareCompensationPayloadAsync(
            TaskArtifact transformationTask,
            OrchestrationPayloadContext payloadContext,
            CompensationArtifact compensation,
            string fallbackPayload,
            CancellationToken cancellationToken)
        {
            if (compensation.Transformation?.IsEnabled != true)
            {
                var parsedPayload = ParsePayload(fallbackPayload);
                return CompensationPayloadPreparationResult.Success(parsedPayload);
            }

            var transformResult = await _transformationExecutor.TransformAsync(
                new OrchestrationTransformationRequest
                {
                    Task = transformationTask,
                    PayloadContext = payloadContext
                },
                cancellationToken);
            return transformResult.Succeeded
                ? CompensationPayloadPreparationResult.Success(transformResult.Payload?.DeepClone())
                : CompensationPayloadPreparationResult.Failure(
                    string.IsNullOrWhiteSpace(transformResult.ErrorCode) ? "CompensationTransformationFailed" : transformResult.ErrorCode,
                    string.IsNullOrWhiteSpace(transformResult.ErrorMessage) ? "Compensation transformation failed." : transformResult.ErrorMessage,
                    transformResult.Diagnostics);
        }

        private static TaskArtifact CreateTriggerCompensationTask(
            TriggerBindingArtifact trigger,
            string triggerKey)
            => new(
                trigger.Id,
                triggerKey,
                trigger.Description ?? triggerKey,
                0,
                string.Empty,
                trigger.Compensation.CompensationTaskKind,
                TaskExecutionMode.Sequential,
                null,
                null,
                trigger.Compensation.Transformation,
                trigger.Compensation.Configuration,
                trigger.Compensation.RetryPolicy,
                trigger.Compensation.TimeoutPolicy,
                ResolveCompensationOnErrorPolicy(trigger.Compensation.OnErrorPolicy),
                trigger.Compensation,
                trigger.Compensation.DispatchType,
                true);

        private static OnErrorPolicy ResolveCompensationOnErrorPolicy(OnErrorPolicy requested)
            => requested == OnErrorPolicy.Continue ? OnErrorPolicy.Continue : OnErrorPolicy.Stop;

        private static bool IsTerminalCompensationFailure(CompensationArtifact compensation)
            => ResolveCompensationOnErrorPolicy(compensation.OnErrorPolicy) == OnErrorPolicy.Stop;

        private static bool ShouldContinueAfterCompensationFailure(CompensationArtifact compensation)
            => !IsTerminalCompensationFailure(compensation);

        private static JsonNode ParsePayload(string payload)
            => string.IsNullOrWhiteSpace(payload) ? null : JsonNode.Parse(payload);

        private async Task MarkCompensationConditionFailedAsync(
            OrchestrationInstance instance,
            Id? stageExecutionId,
            Id? sourceTaskExecutionId,
            CompensationExecution compensation,
            OrchestrationConditionEvaluationResult condition,
            bool terminalFailure,
            CancellationToken cancellationToken)
        {
            compensation.Status = "Failed";
            compensation.FailedOnUtc = DateTime.UtcNow;
            compensation.ErrorMessage = condition.ErrorMessage;
            compensation.Metadata["ConditionErrorCode"] = JsonValue.Create(condition.ErrorCode);
            compensation.Metadata["ConditionErrorMessage"] = JsonValue.Create(condition.ErrorMessage);
            CopyDiagnostics(compensation.Metadata, condition.Diagnostics);

            if (terminalFailure)
            {
                instance.Status = OrchestrationInstanceStatus.Failed;
                instance.FailedOnUtc = compensation.FailedOnUtc;
                instance.ErrorSummary = condition.ErrorMessage;
                instance.LastUpdatedOnUtc = compensation.FailedOnUtc.Value;
                instance.Metadata[CompensationTerminalFailureMetadataKey] = JsonValue.Create(true);
            }

            await _compensationRepository.Update(compensation, cancellationToken);
            if (terminalFailure)
            {
                await _instanceRepository.Update(instance, cancellationToken);
            }
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecutionId,
                TaskExecutionId = sourceTaskExecutionId,
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
            Id? stageExecutionId,
            Id? sourceTaskExecutionId,
            string sourceElementKey,
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
                StageExecutionId = stageExecutionId,
                TaskExecutionId = sourceTaskExecutionId,
                TransitionType = "CompensationSkipped",
                FromStatus = "Running",
                ToStatus = "Skipped",
                OccurredOnUtc = compensation.CompletedOnUtc.Value,
                Message = $"Compensation '{compensation.CompensationTaskKey}' for '{sourceElementKey}' skipped because its execution condition evaluated to false.",
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private async Task MarkCompensationFailedAsync(
            OrchestrationInstance instance,
            Id? stageExecutionId,
            Id? sourceTaskExecutionId,
            CompensationExecution compensation,
            Exception exception,
            string errorCode,
            bool terminalFailure,
            CancellationToken cancellationToken)
        {
            var failedOnUtc = DateTime.UtcNow;
            compensation.Status = "Failed";
            compensation.FailedOnUtc = failedOnUtc;
            compensation.ErrorMessage = exception.Message;
            compensation.Metadata["ExecutionErrorCode"] = JsonValue.Create(errorCode);
            compensation.Metadata["ExecutionErrorMessage"] = JsonValue.Create(exception.Message);

            if (terminalFailure)
            {
                instance.Status = OrchestrationInstanceStatus.Failed;
                instance.FailedOnUtc = failedOnUtc;
                instance.ErrorSummary = exception.Message;
                instance.LastUpdatedOnUtc = failedOnUtc;
                instance.Metadata[CompensationTerminalFailureMetadataKey] = JsonValue.Create(true);
            }

            await _compensationRepository.Update(compensation, cancellationToken);
            if (terminalFailure)
            {
                await _instanceRepository.Update(instance, cancellationToken);
            }
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecutionId,
                TaskExecutionId = sourceTaskExecutionId,
                TransitionType = "CompensationFailed",
                FromStatus = "Running",
                ToStatus = "Failed",
                OccurredOnUtc = failedOnUtc,
                Message = exception.Message,
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private async Task MarkCompensationTransformationFailedAsync(
            OrchestrationInstance instance,
            Id? stageExecutionId,
            Id? sourceTaskExecutionId,
            CompensationExecution compensation,
            CompensationPayloadPreparationResult preparation,
            bool terminalFailure,
            CancellationToken cancellationToken)
        {
            var failedOnUtc = DateTime.UtcNow;
            compensation.Status = "Failed";
            compensation.FailedOnUtc = failedOnUtc;
            compensation.ErrorMessage = preparation.ErrorMessage;
            compensation.Metadata["TransformationErrorCode"] = JsonValue.Create(preparation.ErrorCode);
            compensation.Metadata["TransformationErrorMessage"] = JsonValue.Create(preparation.ErrorMessage);
            CopyDiagnostics(compensation.Metadata, preparation.Diagnostics, "Transformation");

            if (terminalFailure)
            {
                instance.Status = OrchestrationInstanceStatus.Failed;
                instance.FailedOnUtc = failedOnUtc;
                instance.ErrorSummary = preparation.ErrorMessage;
                instance.LastUpdatedOnUtc = failedOnUtc;
                instance.Metadata[CompensationTerminalFailureMetadataKey] = JsonValue.Create(true);
            }

            await _compensationRepository.Update(compensation, cancellationToken);
            if (terminalFailure)
            {
                await _instanceRepository.Update(instance, cancellationToken);
            }
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = stageExecutionId,
                TaskExecutionId = sourceTaskExecutionId,
                TransitionType = "CompensationTransformationFailed",
                FromStatus = "Running",
                ToStatus = "Failed",
                OccurredOnUtc = failedOnUtc,
                Message = preparation.ErrorMessage,
                ProducedBy = nameof(CompensateInstanceDecisionHandler)
            }, cancellationToken);
        }

        private static void CopyDiagnostics(
            IDictionary<string, JsonNode> metadata,
            IReadOnlyDictionary<string, JsonNode> diagnostics,
            string prefix = "Condition")
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

        private sealed record CompensationPayloadPreparationResult
        {
            public bool Succeeded { get; init; }

            public JsonNode Payload { get; init; }

            public string PayloadText { get; init; }

            public string ErrorCode { get; init; }

            public string ErrorMessage { get; init; }

            public IReadOnlyDictionary<string, JsonNode> Diagnostics { get; init; } = new Dictionary<string, JsonNode>();

            public static CompensationPayloadPreparationResult Success(JsonNode payload)
                => new()
                {
                    Succeeded = true,
                    Payload = payload,
                    PayloadText = payload?.ToJsonString()
                };

            public static CompensationPayloadPreparationResult Failure(
                string errorCode,
                string errorMessage,
                IReadOnlyDictionary<string, JsonNode> diagnostics)
                => new()
                {
                    Succeeded = false,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    Diagnostics = diagnostics ?? new Dictionary<string, JsonNode>()
                };
        }
    }
}
