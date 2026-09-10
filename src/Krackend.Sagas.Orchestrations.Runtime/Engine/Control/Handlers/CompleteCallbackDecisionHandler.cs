using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class CompleteCallbackDecisionHandler : IDecisionHandler<CompleteCallbackDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskExecutionAttemptRepository _attemptRepository;
        private readonly ITaskDispatchRepository _dispatchRepository;
        private readonly IExecutionTransitionRepository _transitionRepository;
        private readonly IOrchestrationPayloadState _payloadState;
        private readonly IRuntimeArtifactResolver _artifactResolver;
        private readonly IOrchestrationValidationExecutor _validationExecutor;

        public CompleteCallbackDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            ITaskExecutionRepository taskRepository,
            ITaskExecutionAttemptRepository attemptRepository,
            ITaskDispatchRepository dispatchRepository,
            IExecutionTransitionRepository transitionRepository,
            IOrchestrationPayloadState payloadState,
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationValidationExecutor validationExecutor)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
            _transitionRepository = transitionRepository ?? throw new ArgumentNullException(nameof(transitionRepository));
            _payloadState = payloadState ?? throw new ArgumentNullException(nameof(payloadState));
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _validationExecutor = validationExecutor ?? throw new ArgumentNullException(nameof(validationExecutor));
        }

        public async Task HandleAsync(CompleteCallbackDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var task = await _taskRepository.GetById(decision.TaskExecutionId, cancellationToken);
            var stage = await _stageRepository.GetById(task.StageExecutionId, cancellationToken);
            var dispatch = await _dispatchRepository.GetById(decision.DispatchId, cancellationToken);
            var attempt = await _attemptRepository.GetByDispatchId(decision.DispatchId, cancellationToken);
            var result = decision.ExecutionResultMetadata ?? BuildMissingExecutionResultMetadata();
            var responsePayload = string.IsNullOrWhiteSpace(decision.Payload) ? null : JsonNode.Parse(decision.Payload);
            result = await ApplyResponseValidationAsync(instance, stage, task, result, responsePayload, cancellationToken);
            var succeeded = result.Succeeded;
            var errorMessage = result.ErrorMessage;

            dispatch.DispatchStatus = succeeded ? "Acknowledged" : "Failed";
            dispatch.AcknowledgedOnUtc = succeeded ? now : null;
            dispatch.FailedOnUtc = succeeded ? null : now;
            dispatch.FailureReason = succeeded ? null : errorMessage;
            task.Status = succeeded ? TaskExecutionStatus.Completed : TaskExecutionStatus.Failed;
            task.CompletedOnUtc = succeeded ? now : null;
            task.FailedOnUtc = succeeded ? null : now;
            task.WaitingSinceUtc = null;
            attempt.Status = succeeded ? TaskExecutionStatus.Completed : TaskExecutionStatus.Failed;
            attempt.CompletedOnUtc = succeeded ? now : null;
            attempt.FailedOnUtc = succeeded ? null : now;
            attempt.WaitingSinceUtc = null;
            attempt.ResponsePayload = responsePayload?.DeepClone();
            attempt.ErrorCode = succeeded ? null : result.ErrorCode;
            attempt.ErrorMessage = succeeded ? null : errorMessage;
            CopyExecutionResultMetadata(attempt, result);

            instance.Status = succeeded || task.OnErrorPolicy == OnErrorPolicy.Continue
                ? OrchestrationInstanceStatus.Running
                : OrchestrationInstanceStatus.Failed;
            instance.FailedOnUtc = succeeded || task.OnErrorPolicy == OnErrorPolicy.Continue ? null : now;
            instance.ErrorSummary = succeeded ? null : errorMessage;
            instance.WaitingSinceUtc = null;
            instance.LastUpdatedOnUtc = now;
            if (succeeded)
            {
                instance.SnapshotPayload = _payloadState.ApplyCallbackPayload(
                    instance,
                    stage.StageKey,
                    task.TaskKey,
                    responsePayload);
            }

            await _dispatchRepository.Update(dispatch, cancellationToken);
            await _attemptRepository.Update(attempt, cancellationToken);
            await _taskRepository.Update(task, cancellationToken);
            await _instanceRepository.Update(instance, cancellationToken);
            await _transitionRepository.Create(new ExecutionTransition
            {
                Id = Id.New(),
                OrchestrationInstanceId = instance.Id,
                StageExecutionId = task.StageExecutionId,
                TaskExecutionId = task.Id,
                TaskExecutionAttemptId = attempt.Id,
                TransitionType = succeeded ? "TaskCallbackCompleted" : "TaskCallbackFailed",
                FromStatus = TaskExecutionStatus.WaitingResponse.ToString(),
                ToStatus = task.Status.ToString(),
                OccurredOnUtc = now,
                Message = succeeded
                    ? $"Callback completed task '{task.TaskKey}'."
                    : $"Callback failed task '{task.TaskKey}': {errorMessage}",
                Payload = responsePayload?.DeepClone(),
                ProducedBy = nameof(CompleteCallbackDecisionHandler)
            }, cancellationToken);
        }

        private static OrchestrationExecutionResultMetadata BuildMissingExecutionResultMetadata()
            => new()
            {
                Succeeded = false,
                Status = "Failed",
                ErrorCode = "MissingExecutionResultMetadata",
                ErrorMessage = "Backchannel callback did not include orchestration execution result metadata.",
                CompletedOnUtc = DateTime.UtcNow
            };

        private async Task<OrchestrationExecutionResultMetadata> ApplyResponseValidationAsync(
            OrchestrationInstance instance,
            StageExecution stage,
            TaskExecution task,
            OrchestrationExecutionResultMetadata result,
            JsonNode responsePayload,
            CancellationToken cancellationToken)
        {
            if (!result.Succeeded)
            {
                return result;
            }

            var resolvedArtifact = await _artifactResolver.ResolveAsync(
                instance.RuntimeOrchestrationArtifactId.ToString(),
                cancellationToken);
            var taskArtifact = resolvedArtifact.Artifact.StageDefinitions
                .FirstOrDefault(x => x.Key == stage.StageKey)?
                .TaskDefinitions
                .FirstOrDefault(x => x.Key == task.TaskKey);

            if (taskArtifact?.Configuration is not MessagingTaskConfigurationArtifact messagingConfiguration)
            {
                return result;
            }

            var validationBinding = messagingConfiguration.ResponseSchemaBinding ?? messagingConfiguration.SchemaBinding;
            if (validationBinding?.IsValidationEnabled != true)
            {
                return result;
            }

            var validationResult = await _validationExecutor.ValidateAsync(
                new OrchestrationValidationRequest
                {
                    Task = taskArtifact,
                    SchemaBinding = validationBinding,
                    Payload = responsePayload?.DeepClone(),
                    Phase = "Response"
                },
                cancellationToken);

            if (validationResult.Succeeded)
            {
                return result;
            }

            result.Succeeded = false;
            result.Status = "Failed";
            result.ErrorCode = string.IsNullOrWhiteSpace(validationResult.ErrorCode)
                ? "ResponseValidationFailed"
                : validationResult.ErrorCode;
            result.ErrorMessage = string.IsNullOrWhiteSpace(validationResult.ErrorMessage)
                ? "Task response validation failed."
                : validationResult.ErrorMessage;
            result.ErrorType = "Validation";

            foreach (var diagnostic in validationResult.Diagnostics)
            {
                result.Metadata[$"Validation.{diagnostic.Key}"] = diagnostic.Value?.DeepClone();
            }

            return result;
        }

        private static void CopyExecutionResultMetadata(
            TaskExecutionAttempt attempt,
            OrchestrationExecutionResultMetadata result)
        {
            attempt.Metadata["ExecutionSucceeded"] = JsonValue.Create(result.Succeeded);
            attempt.Metadata["ExecutionStatus"] = JsonValue.Create(result.Status ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                attempt.Metadata["ExecutionErrorCode"] = JsonValue.Create(result.ErrorCode);
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                attempt.Metadata["ExecutionErrorMessage"] = JsonValue.Create(result.ErrorMessage);
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorType))
            {
                attempt.Metadata["ExecutionErrorType"] = JsonValue.Create(result.ErrorType);
            }

            if (result.StartedOnUtc.HasValue)
            {
                attempt.Metadata["ExecutionStartedOnUtc"] = JsonValue.Create(result.StartedOnUtc.Value);
            }

            if (result.CompletedOnUtc.HasValue)
            {
                attempt.Metadata["ExecutionCompletedOnUtc"] = JsonValue.Create(result.CompletedOnUtc.Value);
            }

            if (result.ExecutionTimeMs.HasValue)
            {
                attempt.Metadata["ExecutionTimeMs"] = JsonValue.Create(result.ExecutionTimeMs.Value);
            }

            if (!string.IsNullOrWhiteSpace(result.RequestType))
            {
                attempt.Metadata["ExecutionRequestType"] = JsonValue.Create(result.RequestType);
            }

            if (!string.IsNullOrWhiteSpace(result.ResponseType))
            {
                attempt.Metadata["ExecutionResponseType"] = JsonValue.Create(result.ResponseType);
            }

            foreach (var metadata in result.Metadata)
            {
                attempt.Metadata[$"Execution.{metadata.Key}"] = metadata.Value?.DeepClone();
            }
        }
    }
}
