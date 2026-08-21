using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Builds a complete orchestration version snapshot ready to be serialized as an artifact.
/// </summary>
public sealed class OrchestrationVersionArtifactSnapshotBuilder : IOrchestrationVersionArtifactSnapshotBuilder
{
    private readonly ITriggerBindingRepository _triggerBindingRepository;
    private readonly IVariableDefinitionRepository _variableDefinitionRepository;
    private readonly IStageRepository _stageRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IParallelGroupRepository _parallelGroupRepository;
    private readonly IBranchRuleRepository _branchRuleRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationVersionArtifactSnapshotBuilder"/> class.
    /// </summary>
    public OrchestrationVersionArtifactSnapshotBuilder(
        ITriggerBindingRepository triggerBindingRepository,
        IVariableDefinitionRepository variableDefinitionRepository,
        IStageRepository stageRepository,
        ITaskRepository taskRepository,
        IParallelGroupRepository parallelGroupRepository,
        IBranchRuleRepository branchRuleRepository)
    {
        _triggerBindingRepository = triggerBindingRepository ?? throw new ArgumentNullException(nameof(triggerBindingRepository));
        _variableDefinitionRepository = variableDefinitionRepository ?? throw new ArgumentNullException(nameof(variableDefinitionRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _parallelGroupRepository = parallelGroupRepository ?? throw new ArgumentNullException(nameof(parallelGroupRepository));
        _branchRuleRepository = branchRuleRepository ?? throw new ArgumentNullException(nameof(branchRuleRepository));
    }

    /// <summary>
    /// Builds a complete snapshot of the specified orchestration version.
    /// </summary>
    /// <param name="version">Version to snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete orchestration version snapshot.</returns>
    public async Task<OrchestrationVersion> Build(
        OrchestrationVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        version.TriggerBindings = (await _triggerBindingRepository.GetAll(version.Id, cancellationToken))
            .OrderBy(x => x.Key)
            .ToList();
        version.VariableDefinitions = (await _variableDefinitionRepository.GetAll(version.Id, cancellationToken))
            .OrderBy(x => x.Key)
            .ToList();
        version.StageDefinitions = (await _stageRepository.GetAll(version.Id, cancellationToken))
            .OrderBy(x => x.Order)
            .ToList();

        foreach (var stage in version.StageDefinitions)
        {
            stage.TaskDefinitions = (await _taskRepository.GetAll(stage.Id, cancellationToken))
                .OrderBy(x => x.Order)
                .ToList();
            stage.ParallelGroups = (await _parallelGroupRepository.GetAll(stage.Id, cancellationToken))
                .OrderBy(x => x.Name)
                .ToList();
            stage.BranchRules = (await _branchRuleRepository.GetAll(stage.Id, cancellationToken))
                .ToList();
        }

        ValidateSnapshot(version);
        return version;
    }

    private void ValidateSnapshot(OrchestrationVersion version)
    {
        if (!version.TriggerBindings.Any(x => x.IsEnabled))
        {
            throw new InvalidOperationException($"Orchestration version '{version.Id}' does not have an enabled trigger.");
        }

        if (version.StageDefinitions.Count == 0)
        {
            throw new InvalidOperationException($"Orchestration version '{version.Id}' does not have stages.");
        }

        foreach (var stage in version.StageDefinitions)
        {
            if (!stage.TaskDefinitions.Any(x => x.IsEnabled))
            {
                throw new InvalidOperationException($"Stage '{stage.Key}' does not have enabled tasks.");
            }

            foreach (var task in stage.TaskDefinitions.Where(x => x.IsEnabled))
            {
                ValidateTask(task);
            }
        }
    }

    private void ValidateTask(TaskDefinition task)
    {
        if (task.Configuration is null)
        {
            throw new InvalidOperationException($"Task '{task.Key}' does not have a configuration.");
        }

        if (task.RetryPolicy is null)
        {
            throw new InvalidOperationException($"Task '{task.Key}' does not have a retry policy.");
        }

        if (task.TimeoutPolicy is null)
        {
            throw new InvalidOperationException($"Task '{task.Key}' does not have a timeout policy.");
        }
    }
}
