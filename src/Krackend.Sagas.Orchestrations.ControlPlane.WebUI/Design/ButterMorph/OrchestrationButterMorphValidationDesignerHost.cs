namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

/// <summary>
/// Provides ButterMorph validation designer integration for orchestration entry validations.
/// </summary>
public sealed class OrchestrationButterMorphValidationDesignerHost : IButterMorphValidationDesignerHost
{
    private readonly IOrchestrationSchemaContextApplicationService _schemaContextService;
    private readonly IStageApplicationService _stageApplicationService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly IOrchestrationButterMorphDesignerContextParser _contextParser;
    private readonly IOrchestrationButterMorphSchemaImporter _schemaImporter;
    private readonly IOrchestrationButterMorphSourceMetadataFactory _sourceMetadataFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphValidationDesignerHost"/> class.
    /// </summary>
    /// <param name="schemaContextService">Schema context application service.</param>
    /// <param name="stageApplicationService">Stage application service.</param>
    /// <param name="taskApplicationService">Task application service.</param>
    /// <param name="contextParser">Designer context parser.</param>
    /// <param name="schemaImporter">Schema importer.</param>
    /// <param name="sourceMetadataFactory">Source metadata factory.</param>
    public OrchestrationButterMorphValidationDesignerHost(
        IOrchestrationSchemaContextApplicationService schemaContextService,
        IStageApplicationService stageApplicationService,
        ITaskApplicationService taskApplicationService,
        IOrchestrationButterMorphDesignerContextParser contextParser,
        IOrchestrationButterMorphSchemaImporter schemaImporter,
        IOrchestrationButterMorphSourceMetadataFactory sourceMetadataFactory)
    {
        _schemaContextService = schemaContextService ?? throw new ArgumentNullException(nameof(schemaContextService));
        _stageApplicationService = stageApplicationService ?? throw new ArgumentNullException(nameof(stageApplicationService));
        _taskApplicationService = taskApplicationService ?? throw new ArgumentNullException(nameof(taskApplicationService));
        _contextParser = contextParser ?? throw new ArgumentNullException(nameof(contextParser));
        _schemaImporter = schemaImporter ?? throw new ArgumentNullException(nameof(schemaImporter));
        _sourceMetadataFactory = sourceMetadataFactory ?? throw new ArgumentNullException(nameof(sourceMetadataFactory));
    }

    /// <inheritdoc />
    public async Task<ButterMorphValidationDesignerLoadResult> Load(ButterMorphValidationDesignerLoadRequest request)
    {
        try
        {
            if (_contextParser.TryParseStageEntryValidation(request.ContextKey, out var stageContext))
            {
                return await LoadStageValidation(stageContext);
            }

            if (_contextParser.TryParseTaskEntryValidation(request.ContextKey, out var taskContext))
            {
                return await LoadTaskValidation(taskContext);
            }

            return CreateLoadFailure("Invalid orchestration validation context.");
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
            if (_contextParser.TryParseStageEntryValidation(request.ContextKey, out var stageContext))
            {
                return await SaveStageValidation(request, stageContext);
            }

            if (_contextParser.TryParseTaskEntryValidation(request.ContextKey, out var taskContext))
            {
                return await SaveTaskValidation(request, taskContext);
            }

            return CreateSaveFailure("Invalid orchestration validation context.");
        }
        catch (Exception exception)
        {
            return CreateSaveFailure(exception.Message);
        }
    }

    private async Task<ButterMorphValidationDesignerLoadResult> LoadStageValidation(
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForStage(new GetStageSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.StageDefinitionId));
        var stage = await _stageApplicationService.GetById(new GetStageDefinitionByIdQuery(designerContext.StageDefinitionId));

        return LoadValidation(
            schemaContext,
            (stage?.EntryValidation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty);
    }

    private async Task<ButterMorphValidationDesignerLoadResult> LoadTaskValidation(
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForTask(new GetTaskSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.TaskDefinitionId));
        var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(designerContext.TaskDefinitionId));

        return LoadValidation(
            schemaContext,
            (task?.EntryValidation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty);
    }

    private ButterMorphValidationDesignerLoadResult LoadValidation(
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

    private async Task<ButterMorphValidationDesignerSaveResult> SaveStageValidation(
        ButterMorphValidationDesignerSaveRequest request,
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForStage(new GetStageSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.StageDefinitionId));
        var updated = await _stageApplicationService.SetEntryValidation(
            new SetStageEntryValidationCommand(
                designerContext.StageDefinitionId,
                CreateValidation(request.DslContent, schemaContext.Signature, "StageEntryValidationFailed")));

        return new ButterMorphValidationDesignerSaveResult
        {
            Succeeded = updated,
            Message = updated ? "Stage entry validation saved." : "Stage entry validation could not be saved."
        };
    }

    private async Task<ButterMorphValidationDesignerSaveResult> SaveTaskValidation(
        ButterMorphValidationDesignerSaveRequest request,
        OrchestrationButterMorphDesignerContext designerContext)
    {
        var schemaContext = await _schemaContextService.GetForTask(new GetTaskSchemaContextQuery(
            designerContext.OrchestrationVersionId,
            designerContext.TaskDefinitionId));
        var updated = await _taskApplicationService.SetEntryValidation(
            new SetTaskEntryValidationCommand(
                designerContext.TaskDefinitionId,
                CreateValidation(request.DslContent, schemaContext.Signature, "TaskEntryValidationFailed")));

        return new ButterMorphValidationDesignerSaveResult
        {
            Succeeded = updated,
            Message = updated ? "Task entry validation saved." : "Task entry validation could not be saved."
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

    private static ValidationDefinition CreateValidation(
        string dsl,
        string sourceContextHash,
        string fallbackErrorCode)
    {
        if (string.IsNullOrWhiteSpace(dsl))
        {
            return null;
        }

        return new ValidationDefinition
        {
            Engine = EngineType.DSL,
            ErrorCode = fallbackErrorCode,
            Configuration = new DslValidationConfiguration
            {
                Dsl = dsl,
                SourceContextHash = sourceContextHash ?? string.Empty,
                SemanticDiagnosticsJson = "{}"
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
