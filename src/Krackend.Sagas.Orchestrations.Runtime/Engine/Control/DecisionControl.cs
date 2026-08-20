using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
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
            if (instance.Status == OrchestrationInstanceStatus.Completed)
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

            if (taskExecutions.Any(x => x.Status is TaskExecutionStatus.Running or TaskExecutionStatus.WaitingResponse))
            {
                return [];
            }

            var nextTask = tasks.FirstOrDefault(task =>
                taskExecutions.All(execution => execution.TaskKey != task.Key || execution.Status != TaskExecutionStatus.Completed));
            if (nextTask is null)
            {
                return [new CompleteStageDecision(instance.Id, currentStageExecution.Id)];
            }

            return [new DispatchTaskDecision(
                instance.Id,
                currentStageExecution.Id,
                currentStage.Key,
                nextTask,
                request.Payload?.ToJsonString())];
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

            return new CompleteCallbackDecision(
                RuntimeIdParser.Parse(request.Metadata.OrchestrationInstanceId),
                RuntimeIdParser.Parse(request.Metadata.TaskExecutionId),
                RuntimeIdParser.Parse(request.Metadata.DispatchId),
                request.Payload?.ToJsonString());
        }
    }
}
