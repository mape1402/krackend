using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Responses;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    internal class DecisionControl : IDecisionControl
    {
        private readonly IRuntimeArtifactResolver _artifactResolver;
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly IStageExecutionRepository _stageRepository;
        private readonly ITaskExecutionRepository _taskRepository;

        public DecisionControl(
            IRuntimeArtifactResolver artifactResolver,
            IOrchestrationInstanceRepository instanceRepository,
            IStageExecutionRepository stageRepository,
            ITaskExecutionRepository taskRepository)
        {
            _artifactResolver = artifactResolver ?? throw new ArgumentNullException(nameof(artifactResolver));
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        }

        public async Task<IReadOnlyCollection<IDecision>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
        {
            var callbackDecision = await TryBuildCallbackDecision(request, cancellationToken);
            if (callbackDecision is not null)
            {
                return [callbackDecision];
            }

            if (string.IsNullOrWhiteSpace(request.Metadata?.OrchestrationInstanceId))
            {
                return [];
            }

            var resolvedArtifact = await _artifactResolver.ResolveAsync(request.ArtifactId, cancellationToken);
            var instanceId = RuntimeIdParser.Parse(request.Metadata.OrchestrationInstanceId);
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

            var failedTask = taskExecutions.FirstOrDefault(x => x.Status == TaskExecutionStatus.Failed);
            if (failedTask is not null)
            {
                var failedArtifact = tasks.FirstOrDefault(x => x.Key == failedTask.TaskKey);
                if (failedArtifact?.OnErrorPolicy == OnErrorPolicy.StopAndCompensate)
                {
                    return [new CompensateInstanceDecision(instance.Id, request.ArtifactId, request.Payload?.ToJsonString())];
                }

                if (failedArtifact?.OnErrorPolicy == OnErrorPolicy.Continue)
                {
                    return BuildNextTaskDecisions(instance, currentStage, currentStageExecution, tasks, taskExecutions, request);
                }

                return [];
            }

            if (taskExecutions.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse))
            {
                return [];
            }

            return BuildNextTaskDecisions(instance, currentStage, currentStageExecution, tasks, taskExecutions, request);
        }

        private static IReadOnlyCollection<IDecision> BuildNextTaskDecisions(
            OrchestrationInstance instance,
            StageArtifact currentStage,
            StageExecution currentStageExecution,
            IReadOnlyCollection<TaskArtifact> tasks,
            IReadOnlyCollection<TaskExecution> taskExecutions,
            DecisionRequest request)
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
                    request.Payload?.ToJsonString())];
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
                    request.Payload?.ToJsonString()))
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

        private async Task<IDecision> TryBuildCallbackDecision(DecisionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Metadata?.OrchestrationInstanceId) ||
                string.IsNullOrWhiteSpace(request.Metadata.TaskExecutionId) ||
                string.IsNullOrWhiteSpace(request.Metadata.DispatchId))
            {
                return null;
            }

            var task = await _taskRepository.GetById(RuntimeIdParser.Parse(request.Metadata.TaskExecutionId), cancellationToken);
            if (task.Status != TaskExecutionStatus.WaitingResponse)
            {
                return null;
            }

            var callback = ParseCallbackPayload(request.Payload?.ToJsonString());

            return new CompleteCallbackDecision(
                RuntimeIdParser.Parse(request.Metadata.OrchestrationInstanceId),
                RuntimeIdParser.Parse(request.Metadata.TaskExecutionId),
                RuntimeIdParser.Parse(request.Metadata.DispatchId),
                callback.Payload,
                callback.Succeeded,
                callback.ErrorCode,
                callback.ErrorMessage,
                callback.EnvelopePayload);
        }

        private static CallbackPayload ParseCallbackPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new CallbackPayload(null, true, null, null, null);
            }

            try
            {
                var envelope = System.Text.Json.JsonSerializer.Deserialize<RuntimeTaskResponseEnvelope>(payload);
                if (envelope is not null && !string.IsNullOrWhiteSpace(envelope.Status))
                {
                    return new CallbackPayload(
                        envelope.Payload?.ToJsonString(),
                        envelope.Succeeded,
                        envelope.Error?.Code,
                        envelope.Error?.Message,
                        payload);
                }
            }
            catch
            {
                return new CallbackPayload(payload, true, null, null, null);
            }

            return new CallbackPayload(payload, true, null, null, null);
        }

        private sealed record CallbackPayload(
            string Payload,
            bool Succeeded,
            string ErrorCode,
            string ErrorMessage,
            string EnvelopePayload);
    }
}
