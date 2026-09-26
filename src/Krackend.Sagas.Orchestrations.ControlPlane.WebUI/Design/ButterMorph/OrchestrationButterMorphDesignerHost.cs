namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;

/// <summary>
/// Provides ButterMorph mapping designer integration for orchestration task transformations.
/// </summary>
public sealed class OrchestrationButterMorphDesignerHost : IButterMorphDesignerHost
{
    private readonly IOrchestrationSchemaContextApplicationService _schemaContextService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly IOrchestrationButterMorphDesignerContextParser _contextParser;
    private readonly IOrchestrationButterMorphSchemaImporter _schemaImporter;
    private readonly IOrchestrationButterMorphSourceMetadataFactory _sourceMetadataFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphDesignerHost"/> class.
    /// </summary>
    /// <param name="schemaContextService">Schema context application service.</param>
    /// <param name="taskApplicationService">Task application service.</param>
    /// <param name="contextParser">Designer context parser.</param>
    /// <param name="schemaImporter">Schema importer.</param>
    /// <param name="sourceMetadataFactory">Source metadata factory.</param>
    public OrchestrationButterMorphDesignerHost(
        IOrchestrationSchemaContextApplicationService schemaContextService,
        ITaskApplicationService taskApplicationService,
        IOrchestrationButterMorphDesignerContextParser contextParser,
        IOrchestrationButterMorphSchemaImporter schemaImporter,
        IOrchestrationButterMorphSourceMetadataFactory sourceMetadataFactory)
    {
        _schemaContextService = schemaContextService ?? throw new ArgumentNullException(nameof(schemaContextService));
        _taskApplicationService = taskApplicationService ?? throw new ArgumentNullException(nameof(taskApplicationService));
        _contextParser = contextParser ?? throw new ArgumentNullException(nameof(contextParser));
        _schemaImporter = schemaImporter ?? throw new ArgumentNullException(nameof(schemaImporter));
        _sourceMetadataFactory = sourceMetadataFactory ?? throw new ArgumentNullException(nameof(sourceMetadataFactory));
    }

    /// <inheritdoc />
    public async Task<ButterMorphDesignerLoadResult> Load(ButterMorphDesignerLoadRequest request)
    {
        if (!_contextParser.TryParseTaskTransformation(request.ContextKey, out var designerContext))
        {
            return CreateLoadFailure("Invalid orchestration transformation context.");
        }

        try
        {
            var schemaContext = await _schemaContextService.GetForTask(new GetTaskSchemaContextQuery(
                designerContext.OrchestrationVersionId,
                designerContext.TaskDefinitionId));
            var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(designerContext.TaskDefinitionId));
            var diagnostics = new List<string>();
            AddMissingSnapshotDiagnostics(schemaContext, diagnostics);

            var sources = ImportSources(schemaContext, diagnostics);
            var target = ImportTarget(schemaContext, diagnostics);

            if (target is null)
            {
                diagnostics.Add("The current task does not have a usable request schema target.");
            }

            return new ButterMorphDesignerLoadResult
            {
                SourceSchemas = sources,
                SourceMetadata = _sourceMetadataFactory.Create(schemaContext),
                TargetSchema = target,
                InitialDslContent = (task?.Transformation?.Configuration as DslTransformationConfiguration)?.Dsl ?? string.Empty,
                ShowSchemaActions = false,
                Message = diagnostics.Count == 0 ? string.Empty : string.Join(Environment.NewLine, diagnostics)
            };
        }
        catch (Exception exception)
        {
            return CreateLoadFailure(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ButterMorphDesignerSaveResult> Save(ButterMorphDesignerSaveRequest request)
    {
        if (!_contextParser.TryParseTaskTransformation(request.ContextKey, out var designerContext))
        {
            return new ButterMorphDesignerSaveResult
            {
                Succeeded = false,
                Message = "Invalid orchestration transformation context."
            };
        }

        try
        {
            var schemaContext = await _schemaContextService.GetForTask(new GetTaskSchemaContextQuery(
                designerContext.OrchestrationVersionId,
                designerContext.TaskDefinitionId));
            var targetSchemaHash = schemaContext.Target?.SchemaBinding?.Snapshot?.ContentHash ?? string.Empty;
            var transformation = CreateTransformation(request.DslContent, schemaContext.Signature, targetSchemaHash);
            var updated = await _taskApplicationService.SetTransformation(
                new SetTaskTransformationCommand(designerContext.TaskDefinitionId, transformation));

            return new ButterMorphDesignerSaveResult
            {
                Succeeded = updated,
                Message = updated ? "Transformation saved." : "Transformation could not be saved."
            };
        }
        catch (Exception exception)
        {
            return new ButterMorphDesignerSaveResult
            {
                Succeeded = false,
                Message = exception.Message
            };
        }
    }

    private static void AddMissingSnapshotDiagnostics(OrchestrationSchemaContext schemaContext, ICollection<string> diagnostics)
    {
        if (schemaContext?.Target?.SchemaBinding is null)
        {
            diagnostics.Add("The current task does not define a request schema target.");
        }
        else if (schemaContext.Target.SchemaBinding.Snapshot is null)
        {
            diagnostics.Add(BuildMissingSnapshotMessage("Target", schemaContext.Target.Alias, schemaContext.Target.SchemaBinding));
        }

        if (schemaContext?.Sources is null)
        {
            return;
        }

        foreach (var source in schemaContext.Sources.Where(source => source.SchemaBinding?.Snapshot is null))
        {
            diagnostics.Add(BuildMissingSnapshotMessage("Source", source.Alias, source.SchemaBinding));
        }
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

    private IStructureSchema ImportTarget(OrchestrationSchemaContext schemaContext, ICollection<string> diagnostics)
    {
        if (schemaContext.Target?.SchemaBinding?.Snapshot is null)
        {
            return null;
        }

        if (_schemaImporter.TryImport(schemaContext.Target.SchemaBinding, out var schema, out var message))
        {
            return schema;
        }

        diagnostics.Add($"{schemaContext.Target.Alias}: {message}");
        return null;
    }

    private static string BuildMissingSnapshotMessage(string role, string alias, SchemaBinding binding)
    {
        var contract = binding is null
            ? "unknown contract"
            : $"{binding.ContractKey} v{binding.ContractVersion}";
        return $"{role} '{alias}' does not have a stored schema snapshot for {contract}. Re-select the contract while the schema registry is available so Krackend can capture the snapshot.";
    }

    private static string BuildLoadFailureMessage(IReadOnlyCollection<string> diagnostics)
        => diagnostics is null || diagnostics.Count == 0
            ? "ButterMorph could not load the orchestration schema context."
            : string.Join(Environment.NewLine, diagnostics.Distinct(StringComparer.Ordinal));

    private static TransformationDefinition CreateTransformation(
        string dsl,
        string sourceContextHash,
        string targetSchemaHash)
    {
        if (string.IsNullOrWhiteSpace(dsl))
        {
            return null;
        }

        return new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = dsl,
                SourceContextHash = sourceContextHash ?? string.Empty,
                TargetSchemaHash = targetSchemaHash ?? string.Empty,
                SemanticDiagnosticsJson = "{}"
            }
        };
    }

    private static ButterMorphDesignerLoadResult CreateLoadFailure(string message)
        => new()
        {
            SourceSchemas = new Dictionary<string, IStructureSchema>(StringComparer.Ordinal),
            TargetSchema = null,
            InitialDslContent = string.Empty,
            ShowSchemaActions = false,
            Message = message
        };
}
