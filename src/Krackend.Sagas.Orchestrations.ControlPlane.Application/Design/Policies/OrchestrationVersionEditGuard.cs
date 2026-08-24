using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Default guard that allows composition edits only while a version is Draft.
/// </summary>
public sealed class OrchestrationVersionEditGuard : IOrchestrationVersionEditGuard
{
    private readonly IOrchestrationVersionRepository _versionRepository;
    private readonly IStageRepository _stageRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ITriggerBindingRepository _triggerBindingRepository;
    private readonly IVariableDefinitionRepository _variableDefinitionRepository;
    private readonly IParallelGroupRepository _parallelGroupRepository;
    private readonly IBranchRuleRepository _branchRuleRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationVersionEditGuard"/> class.
    /// </summary>
    public OrchestrationVersionEditGuard(
        IOrchestrationVersionRepository versionRepository,
        IStageRepository stageRepository,
        ITaskRepository taskRepository,
        ITriggerBindingRepository triggerBindingRepository,
        IVariableDefinitionRepository variableDefinitionRepository,
        IParallelGroupRepository parallelGroupRepository,
        IBranchRuleRepository branchRuleRepository)
    {
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _triggerBindingRepository = triggerBindingRepository ?? throw new ArgumentNullException(nameof(triggerBindingRepository));
        _variableDefinitionRepository = variableDefinitionRepository ?? throw new ArgumentNullException(nameof(variableDefinitionRepository));
        _parallelGroupRepository = parallelGroupRepository ?? throw new ArgumentNullException(nameof(parallelGroupRepository));
        _branchRuleRepository = branchRuleRepository ?? throw new ArgumentNullException(nameof(branchRuleRepository));
    }

    /// <inheritdoc />
    public async Task EnsureVersionIsDraft(string orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        var version = await _versionRepository.GetById(ParseId(orchestrationVersionId), cancellationToken);
        EnsureDraft(version.Status, version.Id.ToString());
    }

    /// <inheritdoc />
    public async Task EnsureStageVersionIsDraft(string stageDefinitionId, CancellationToken cancellationToken = default)
    {
        var stage = await _stageRepository.GetById(ParseId(stageDefinitionId), cancellationToken);
        await EnsureVersionIsDraft(stage.OrchestrationVersionId.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureTaskVersionIsDraft(string taskDefinitionId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetById(ParseId(taskDefinitionId), cancellationToken);
        await EnsureStageVersionIsDraft(task.StageDefinitionId.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureTriggerVersionIsDraft(string triggerBindingId, CancellationToken cancellationToken = default)
    {
        var trigger = await _triggerBindingRepository.GetById(ParseId(triggerBindingId), cancellationToken);
        await EnsureVersionIsDraft(trigger.OrchestrationVersionId.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureVariableVersionIsDraft(string variableDefinitionId, CancellationToken cancellationToken = default)
    {
        var variable = await _variableDefinitionRepository.GetById(ParseId(variableDefinitionId), cancellationToken);
        await EnsureVersionIsDraft(variable.OrchestrationVersionId.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureParallelGroupVersionIsDraft(string parallelGroupDefinitionId, CancellationToken cancellationToken = default)
    {
        var parallelGroup = await _parallelGroupRepository.GetById(ParseId(parallelGroupDefinitionId), cancellationToken);
        await EnsureStageVersionIsDraft(parallelGroup.StageDefinitionId.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureBranchRuleVersionIsDraft(string branchRuleDefinitionId, CancellationToken cancellationToken = default)
    {
        var branchRule = await _branchRuleRepository.GetById(ParseId(branchRuleDefinitionId), cancellationToken);
        await EnsureElementVersionIsDraft(branchRule.FromType, branchRule.FromId.ToString(), cancellationToken);
    }

    /// <summary>
    /// Ensures a branch source element belongs to an editable orchestration version.
    /// </summary>
    public async Task EnsureElementVersionIsDraft(
        ElementType elementType,
        string elementId,
        CancellationToken cancellationToken = default)
    {
        if (elementType == ElementType.Task)
        {
            await EnsureTaskVersionIsDraft(elementId, cancellationToken);
            return;
        }

        await EnsureStageVersionIsDraft(elementId, cancellationToken);
    }

    private static void EnsureDraft(OrchestrationVersionStatus status, string versionId)
    {
        if (status != OrchestrationVersionStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Orchestration version '{versionId}' is '{status}' and can only be edited while it is Draft.");
        }
    }

    private static Id ParseId(string value) => new(Ulid.Parse(value));
}
