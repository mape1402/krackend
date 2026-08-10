using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles create orchestration version command requests.
/// </summary>
public sealed class CreateOrchestrationVersionCommandHandler : IRequestHandler<CreateOrchestrationVersionCommand, string>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IStageRepository _stageRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IParallelGroupRepository _parallelGroupRepository;
    private readonly IBranchRuleRepository _branchRuleRepository;
    private readonly ITriggerBindingRepository _triggerBindingRepository;
    private readonly IVariableDefinitionRepository _variableDefinitionRepository;
    private readonly IOrchestrationVersionArtifactSnapshotBuilder _snapshotBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrchestrationVersionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public CreateOrchestrationVersionCommandHandler(
        IOrchestrationVersionRepository repository,
        IStageRepository stageRepository,
        ITaskRepository taskRepository,
        IParallelGroupRepository parallelGroupRepository,
        IBranchRuleRepository branchRuleRepository,
        ITriggerBindingRepository triggerBindingRepository,
        IVariableDefinitionRepository variableDefinitionRepository,
        IOrchestrationVersionArtifactSnapshotBuilder snapshotBuilder)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _parallelGroupRepository = parallelGroupRepository ?? throw new ArgumentNullException(nameof(parallelGroupRepository));
        _branchRuleRepository = branchRuleRepository ?? throw new ArgumentNullException(nameof(branchRuleRepository));
        _triggerBindingRepository = triggerBindingRepository ?? throw new ArgumentNullException(nameof(triggerBindingRepository));
        _variableDefinitionRepository = variableDefinitionRepository ?? throw new ArgumentNullException(nameof(variableDefinitionRepository));
        _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    public async Task<string> Handle(CreateOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        Id id = Id.New();

        OrchestrationVersion model = new()
        {
            Id = id,
            OrchestrationDefinitionId = PrimitiveParser.ParseId(request.OrchestrationDefinitionId),
            Version = PrimitiveParser.ParseSemanticVersion(request.Version),
            Status = request.Status,
            VersionLabel = request.VersionLabel,
            Description = request.Description,
            Checksum = new Checksum(request.Checksum),
            Notes = request.Notes,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = request.CreatedBy,
            ApprovedOnUtc = default,
            ApprovedBy = string.Empty,
            UpdatedOnUtc = default,
            UpdatedBy = string.Empty,
        };

        await _repository.Create(model, cancellationToken);
        await ClonePreviousVersionRoadmap(model, cancellationToken);
        return id.ToString();
    }

    private async Task ClonePreviousVersionRoadmap(OrchestrationVersion targetVersion, CancellationToken cancellationToken)
    {
        var source = await GetSourceVersionSnapshot(targetVersion, cancellationToken);
        if (source is null)
        {
            return;
        }

        await CloneTriggers(targetVersion.Id, source, cancellationToken);
        await CloneVariables(targetVersion.Id, source, cancellationToken);
        var idMaps = await CloneStagesGroupsAndTasks(targetVersion.Id, source, cancellationToken);
        await CloneBranchRules(source, idMaps, cancellationToken);
    }

    private async Task<OrchestrationVersion> GetSourceVersionSnapshot(OrchestrationVersion targetVersion, CancellationToken cancellationToken)
    {
        var previousVersion = await _repository.GetLatestByOrchestrationDefinitionId(
            targetVersion.OrchestrationDefinitionId,
            targetVersion.Id,
            cancellationToken);

        return previousVersion is null
            ? null
            : await _snapshotBuilder.Build(previousVersion, cancellationToken);
    }

    private async Task CloneTriggers(Id targetVersionId, OrchestrationVersion source, CancellationToken cancellationToken)
    {
        foreach (var trigger in source.TriggerBindings ?? Enumerable.Empty<TriggerBinding>())
        {
            await _triggerBindingRepository.Create(new TriggerBinding
            {
                Id = Id.New(),
                OrchestrationVersionId = targetVersionId,
                Key = trigger.Key,
                TriggerType = trigger.TriggerType,
                TriggerChannel = trigger.TriggerChannel,
                IsEnabled = trigger.IsEnabled,
                Description = trigger.Description,
            }, cancellationToken);
        }
    }

    private async Task CloneVariables(Id targetVersionId, OrchestrationVersion source, CancellationToken cancellationToken)
    {
        foreach (var variable in source.VariableDefinitions ?? Enumerable.Empty<VariableDefinition>())
        {
            await _variableDefinitionRepository.Create(new VariableDefinition
            {
                Id = Id.New(),
                OrchestrationVersionId = targetVersionId,
                Key = variable.Key,
                DisplayName = variable.DisplayName,
                Description = variable.Description,
                Scope = variable.Scope,
                ValueType = variable.ValueType,
                DefaultValue = variable.DefaultValue,
                IsRequired = variable.IsRequired,
                IsSensitive = variable.IsSensitive,
            }, cancellationToken);
        }
    }

    private async Task<IdMaps> CloneStagesGroupsAndTasks(Id targetVersionId, OrchestrationVersion source, CancellationToken cancellationToken)
    {
        var stageIdMap = new Dictionary<Id, Id>();
        var taskIdMap = new Dictionary<Id, Id>();
        var groupIdMap = new Dictionary<Id, Id>();

        foreach (var stage in source.StageDefinitions.OrderBy(x => x.Order))
        {
            var newStageId = Id.New();
            stageIdMap[stage.Id] = newStageId;

            await _stageRepository.Create(new StageDefinition
            {
                Id = newStageId,
                OrchestrationVersionId = targetVersionId,
                Key = stage.Key,
                Name = stage.Name,
                Description = stage.Description,
                Order = stage.Order,
                ExecutionCondition = stage.ExecutionCondition,
            }, cancellationToken);

            await CloneParallelGroups(stage, newStageId, groupIdMap, cancellationToken);
            await CloneTasks(stage, newStageId, taskIdMap, groupIdMap, cancellationToken);
        }

        return new IdMaps(stageIdMap, taskIdMap);
    }

    private async Task CloneParallelGroups(
        StageDefinition sourceStage,
        Id targetStageId,
        IDictionary<Id, Id> groupIdMap,
        CancellationToken cancellationToken)
    {
        foreach (var group in sourceStage.ParallelGroups ?? Enumerable.Empty<ParallelGroupDefinition>())
        {
            var newGroupId = Id.New();
            groupIdMap[group.Id] = newGroupId;

            await _parallelGroupRepository.Create(new ParallelGroupDefinition
            {
                Id = newGroupId,
                StageDefinitionId = targetStageId,
                Name = group.Name,
                JoinPolicy = group.JoinPolicy,
                MaxParallelAgents = group.MaxParallelAgents,
            }, cancellationToken);
        }
    }

    private async Task CloneTasks(
        StageDefinition sourceStage,
        Id targetStageId,
        IDictionary<Id, Id> taskIdMap,
        IReadOnlyDictionary<Id, Id> groupIdMap,
        CancellationToken cancellationToken)
    {
        foreach (var task in (sourceStage.TaskDefinitions ?? new List<TaskDefinition>()).OrderBy(x => x.Order))
        {
            var newTaskId = Id.New();
            taskIdMap[task.Id] = newTaskId;

            await _taskRepository.Create(new TaskDefinition
            {
                Id = newTaskId,
                StageDefinitionId = targetStageId,
                Key = task.Key,
                Name = task.Name,
                Order = task.Order,
                Notes = task.Notes,
                Kind = task.Kind,
                ExecutionMode = task.ExecutionMode,
                ParallelGroupId = task.ParallelGroupId.HasValue && groupIdMap.TryGetValue(task.ParallelGroupId.Value, out var mappedGroupId)
                    ? mappedGroupId
                    : null,
                ExecutionCondition = task.ExecutionCondition,
                Transformation = task.Transformation,
                Configuration = task.Configuration,
                RetryPolicy = task.RetryPolicy,
                TimeoutPolicy = task.TimeoutPolicy,
                OnErrorPolicy = task.OnErrorPolicy,
                CompensationDefinition = task.CompensationDefinition,
                DispatchType = task.DispatchType,
                IsEnabled = task.IsEnabled,
            }, cancellationToken);
        }
    }

    private async Task CloneBranchRules(OrchestrationVersion source, IdMaps idMaps, CancellationToken cancellationToken)
    {
        foreach (var stage in source.StageDefinitions.OrderBy(x => x.Order))
        {
            foreach (var rule in stage.BranchRules ?? Enumerable.Empty<BranchRuleDefinition>())
            {
                await _branchRuleRepository.Create(new BranchRuleDefinition
                {
                    Id = Id.New(),
                    FromType = rule.FromType,
                    FromId = RemapElementId(rule.FromType, rule.FromId, idMaps.StageIds, idMaps.TaskIds),
                    Condition = rule.Condition,
                    NavigateToType = rule.NavigateToType,
                    NavigateToId = RemapElementId(rule.NavigateToType, rule.NavigateToId, idMaps.StageIds, idMaps.TaskIds),
                }, cancellationToken);
            }
        }
    }

    private static Id RemapElementId(
        ElementType elementType,
        Id sourceId,
        IReadOnlyDictionary<Id, Id> stageIdMap,
        IReadOnlyDictionary<Id, Id> taskIdMap)
    {
        return elementType switch
        {
            ElementType.Stage when stageIdMap.TryGetValue(sourceId, out var newStageId) => newStageId,
            ElementType.Task when taskIdMap.TryGetValue(sourceId, out var newTaskId) => newTaskId,
            _ => sourceId,
        };
    }

    private sealed record IdMaps(
        IReadOnlyDictionary<Id, Id> StageIds,
        IReadOnlyDictionary<Id, Id> TaskIds);
}


