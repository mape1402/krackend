namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;

/// <summary>
/// Provides ButterMorph validation designer integration for orchestration execution conditions.
/// </summary>
public sealed class OrchestrationButterMorphValidationDesignerHost : IButterMorphValidationDesignerHost
{
    private readonly IOrchestrationSchemaContextApplicationService _schemaContextService;
    private readonly IStageApplicationService _stageApplicationService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly ITriggerBindingApplicationService _triggerBindingApplicationService;
    private readonly IOrchestrationButterMorphDesignerContextParser _contextParser;
    private readonly IOrchestrationButterMorphSchemaImporter _schemaImporter;
    private readonly IOrchestrationButterMorphSourceMetadataFactory _sourceMetadataFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphValidationDesignerHost"/> class.
    /// </summary>
    /// <param name="schemaContextService">Schema context application service.</param>
    /// <param name="stageApplicationService">Stage application service.</param>
    /// <param name="taskApplicationService">Task application service.</param>
    /// <param name="triggerBindingApplicationService">Trigger binding application service.</param>
    /// <param name="contextParser">Designer context parser.</param>
    /// <param name="schemaImporter">Schema importer.</param>
    /// <param name="sourceMetadataFactory">Source metadata factory.</param>
    public OrchestrationButterMorphValidationDesignerHost(
        IOrchestrationSchemaContextApplicationService schemaContextService,
        IStageApplicationService stageApplicationService,
        ITaskApplicationService taskApplicationService,
        ITriggerBindingApplicationService triggerBindingApplicationService,
        IOrchestrationButterMorphDesignerContextParser contextParser,
        IOrchestrationButterMorphSchemaImporter schemaImporter,
        IOrchestrationButterMorphSourceMetadataFactory sourceMetadataFactory)
    {
        _schemaContextService = schemaContextService ?? throw new ArgumentNullException(nameof(schemaContextService));
        _stageApplicationService = stageApplicationService ?? throw new ArgumentNullException(nameof(stageApplicationService));
        _taskApplicationService = taskApplicationService ?? throw new ArgumentNullException(nameof(taskApplicationService));
        _triggerBindingApplicationService = triggerBindingApplicationService ?? throw new ArgumentNullException(nameof(triggerBindingApplicationService));
        _contextParser = contextParser ?? throw new ArgumentNullException(nameof(contextParser));
        _schemaImporter = schemaImporter ?? throw new ArgumentNullException(nameof(schemaImporter));
        _sourceMetadataFactory = sourceMetadataFactory ?? throw new ArgumentNullException(nameof(sourceMetadataFactory));
    }

    /// <inheritdoc />
    public async Task<ButterMorphValidationDesignerLoadResult> Load(ButterMorphValidationDesignerLoadRequest request)
    {
        try
        {
            if (_contextParser.TryParseStageExecutionCondition(request.ContextKey, out var stageContext))
            {
                return await LoadStageCondition(stageContext);
            }

            if (_contextParser.TryParseTaskExecutionCondition(request.ContextKey, out var taskContext))
            {
                return await LoadTaskCondition(taskContext);
            }

            if (_contextParser.TryParseTaskCompensationExecutionCondition(request.ContextKey, out var taskCompensationContext))
            {
                return await LoadTaskCompensationCondition(taskCompensationContext);
            }

            if (_contextParser.TryParseTriggerCompensationExecutionCondition(request.ContextKey, out var triggerCompensationContext))
            {
                return await LoadTriggerCompensationCondition(triggerCompensationContext);
            }

            return CreateLoadFailure("Invalid orchestration execution condition context.");
        }
        catch (Exception exception)
        {
            return CreateLoadFailure(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ButterMorphValidationDesignerSaveResult> Save(ButterMorphValidationDesignerSaveRequest request)
    {
        try
        {
            if (_contextParser.TryParseStageExecutionCondition(request.ContextKey, out var stageContext))
            {
                return await SaveStageCondition(request, stageContext);
            }

            if (_contextParser.TryParseTaskExecutionCondition(request.ContextKey, out var taskContext))
            {
                return await SaveTaskCondition(request, taskContext);
            }

            if (_contextParser.TryParseTaskCompensationExecutionCondition(request.ContextKey, out var taskCompensationContext))
            {
                return await SaveTaskCompensationCondition(request, taskCompensationContext);
            }

            if (_contextParser.TryParseTriggerCompensationExecutionCondition(request.ContextKey, out var triggerCompensationContext))
            {
                return await SaveTriggerCompensationCondition(request, triggerCompensationContext);
            }

            return CreateSaveFailure("Invalid orchestration execution condition context.");
        }
        catch (Exception exception)
        {
            return CreateSaveFailure(exception.Message);
        }
    }

    private async Task<ButterMorphValidationDesignerLoadResult> LoadStageCondition(
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForStage(new GetStageSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.StageDefinitionId));
        var stage = await _stageApplicationService.GetById(new GetStageDefinitionByIdQuery(designerContext.StageDefinitionId));

        return LoadCondition(
            schemaContext,
            stage?.HasExecutionCondition == true
                ? (stage.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? string.Empty
                : string.Empty);
    }

    private async Task<ButterMorphValidationDesignerLoadResult> LoadTaskCondition(
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForTask(new GetTaskSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.TaskDefinitionId));
        var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(designerContext.TaskDefinitionId));

        return LoadCondition(
            schemaContext,
            task?.HasExecutionCondition == true
                ? (task.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? string.Empty
                : string.Empty);
    }

    private async Task<ButterMorphValidationDesignerLoadResult> LoadTaskCompensationCondition(
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForTaskCompensation(new GetTaskCompensationSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.TaskDefinitionId));
        var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(designerContext.TaskDefinitionId));

        return LoadCondition(
            schemaContext,
            task?.CompensationDefinition?.HasExecutionCondition == true
                ? (task.CompensationDefinition.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? string.Empty
                : string.Empty);
    }

    private async Task<ButterMorphValidationDesignerLoadResult> LoadTriggerCompensationCondition(
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForTriggerCompensation(new GetTriggerCompensationSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.TriggerBindingId));
        var trigger = await _triggerBindingApplicationService.GetById(new GetTriggerBindingByIdQuery(designerContext.TriggerBindingId));

        return LoadCondition(
            schemaContext,
            trigger?.CompensationDefinition?.HasExecutionCondition == true
                ? (trigger.CompensationDefinition.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? string.Empty
                : string.Empty);
    }

    private ButterMorphValidationDesignerLoadResult LoadCondition(
        OrchestrationSchemaContext schemaContext,
        string initialDsl)
    {
        var diagnostics = new List<string>();
        AddMissingSnapshotDiagnostics(schemaContext, diagnostics);
        var sources = ImportSources(schemaContext, diagnostics);

        return new ButterMorphValidationDesignerLoadResult
        {
            SourceSchemas = sources,
            SourceMetadata = _sourceMetadataFactory.Create(schemaContext),
            InitialDslContent = initialDsl ?? string.Empty,
            Message = diagnostics.Count == 0 ? string.Empty : string.Join(Environment.NewLine, diagnostics)
        };
    }

    private async Task<ButterMorphValidationDesignerSaveResult> SaveStageCondition(
        ButterMorphValidationDesignerSaveRequest request,
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var updated = await _stageApplicationService.SetExecutionCondition(
            new SetStageExecutionConditionCommand(
                designerContext.StageDefinitionId,
                CreateCondition(request.DslContent)));

        return new ButterMorphValidationDesignerSaveResult
        {
            Succeeded = updated,
            Message = updated ? "Stage execution condition saved." : "Stage execution condition could not be saved."
        };
    }

    private async Task<ButterMorphValidationDesignerSaveResult> SaveTaskCondition(
        ButterMorphValidationDesignerSaveRequest request,
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var updated = await _taskApplicationService.SetExecutionCondition(
            new SetTaskExecutionConditionCommand(
                designerContext.TaskDefinitionId,
                CreateCondition(request.DslContent)));

        return new ButterMorphValidationDesignerSaveResult
        {
            Succeeded = updated,
            Message = updated ? "Task execution condition saved." : "Task execution condition could not be saved."
        };
    }

    private async Task<ButterMorphValidationDesignerSaveResult> SaveTaskCompensationCondition(
        ButterMorphValidationDesignerSaveRequest request,
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var updated = await _taskApplicationService.SetCompensationExecutionCondition(
            new SetTaskCompensationExecutionConditionCommand(
                designerContext.TaskDefinitionId,
                CreateCondition(request.DslContent)));

        return new ButterMorphValidationDesignerSaveResult
        {
            Succeeded = updated,
            Message = updated ? "Task compensation condition saved." : "Task compensation condition could not be saved."
        };
    }

    private async Task<ButterMorphValidationDesignerSaveResult> SaveTriggerCompensationCondition(
        ButterMorphValidationDesignerSaveRequest request,
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var updated = await _triggerBindingApplicationService.SetCompensationExecutionCondition(
            new SetTriggerCompensationExecutionConditionCommand(
                designerContext.TriggerBindingId,
                CreateCondition(request.DslContent)));

        return new ButterMorphValidationDesignerSaveResult
        {
            Succeeded = updated,
            Message = updated ? "Trigger compensation condition saved." : "Trigger compensation condition could not be saved."
        };
    }

    private IReadOnlyDictionary<string, IStructureSchema> ImportSources(
        OrchestrationSchemaContext schemaContext,
        ICollection<string> diagnostics)
    {
        var sources = new Dictionary<string, IStructureSchema>(StringComparer.Ordinal);

        foreach (var source in schemaContext.Sources)
        {
            if (source.SchemaBinding?.Snapshot is null)
            {
                continue;
            }

            if (_schemaImporter.TryImport(source.SchemaBinding, out var schema, out var message))
            {
                sources[source.Alias] = schema;
                continue;
            }

            diagnostics.Add($"{source.Alias}: {message}");
        }

        return sources;
    }

    private static void AddMissingSnapshotDiagnostics(OrchestrationSchemaContext schemaContext, ICollection<string> diagnostics)
    {
        if (schemaContext?.Sources is null)
        {
            return;
        }

        foreach (var source in schemaContext.Sources.Where(source => source.SchemaBinding?.Snapshot is null))
        {
            diagnostics.Add(BuildMissingSnapshotMessage(source.Alias, source.SchemaBinding));
        }
    }

    private static ExecutionCondition CreateCondition(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
        {
            return null;
        }

        return new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression(dsl)
            }
        };
    }

    private static string BuildMissingSnapshotMessage(string alias, SchemaBinding binding)
    {
        var contract = binding is null
            ? "unknown contract"
            : $"{binding.ContractKey} v{binding.ContractVersion}";
        return $"Source '{alias}' does not have a stored schema snapshot for {contract}. Re-select the contract while the schema registry is available so Krackend can capture the snapshot.";
    }

    private static ButterMorphValidationDesignerLoadResult CreateLoadFailure(string message)
        => new()
        {
            SourceSchemas = new Dictionary<string, IStructureSchema>(StringComparer.Ordinal),
            SourceMetadata = new Dictionary<string, ButterMorphDesignerSourceMetadata>(StringComparer.Ordinal),
            InitialDslContent = string.Empty,
            Message = message
        };

    private static ButterMorphValidationDesignerSaveResult CreateSaveFailure(string message)
        => new()
        {
            Succeeded = false,
            Message = message
        };
}
