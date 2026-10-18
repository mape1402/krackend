namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;

/// <summary>
/// Provides read-only ButterMorph mapping views for persisted orchestration transformations.
/// </summary>
public sealed class OrchestrationButterMorphMappingViewerHost : IButterMorphMappingViewerHost
{
    private readonly IOrchestrationSchemaContextApplicationService _schemaContextService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly ITriggerBindingApplicationService _triggerBindingApplicationService;
    private readonly IOrchestrationButterMorphDesignerContextParser _contextParser;
    private readonly IOrchestrationButterMorphSchemaImporter _schemaImporter;
    private readonly IDslParser _dslParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphMappingViewerHost"/> class.
    /// </summary>
    public OrchestrationButterMorphMappingViewerHost(
        IOrchestrationSchemaContextApplicationService schemaContextService,
        ITaskApplicationService taskApplicationService,
        ITriggerBindingApplicationService triggerBindingApplicationService,
        IOrchestrationButterMorphDesignerContextParser contextParser,
        IOrchestrationButterMorphSchemaImporter schemaImporter,
        IDslParser dslParser)
    {
        _schemaContextService = schemaContextService ?? throw new ArgumentNullException(nameof(schemaContextService));
        _taskApplicationService = taskApplicationService ?? throw new ArgumentNullException(nameof(taskApplicationService));
        _triggerBindingApplicationService = triggerBindingApplicationService ?? throw new ArgumentNullException(nameof(triggerBindingApplicationService));
        _contextParser = contextParser ?? throw new ArgumentNullException(nameof(contextParser));
        _schemaImporter = schemaImporter ?? throw new ArgumentNullException(nameof(schemaImporter));
        _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
    }

    /// <inheritdoc />
    public async Task<ButterMorphMappingViewerLoadResult> Load(ButterMorphMappingViewerLoadRequest request)
    {
        try
        {
            if (_contextParser.TryParseTaskTransformation(request.ContextKey, out var taskContext))
            {
                var schemaContext = await _schemaContextService.GetForTask(new GetTaskSchemaContextQuery(
                    taskContext.OrchestrationVersionId,
                    taskContext.TaskDefinitionId));
                var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(taskContext.TaskDefinitionId));
                return CreateResult(request.ContextKey, "Task transformation", schemaContext, GetDsl(task?.Transformation));
            }

            if (_contextParser.TryParseTaskCompensationTransformation(request.ContextKey, out var compensationContext))
            {
                var schemaContext = await _schemaContextService.GetForTaskCompensation(new GetTaskCompensationSchemaContextQuery(
                    compensationContext.OrchestrationVersionId,
                    compensationContext.TaskDefinitionId));
                var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(compensationContext.TaskDefinitionId));
                return CreateResult(request.ContextKey, "Task compensation transformation", schemaContext, GetDsl(task?.CompensationDefinition?.Transformation));
            }

            if (_contextParser.TryParseTriggerCompensationTransformation(request.ContextKey, out var triggerContext))
            {
                var schemaContext = await _schemaContextService.GetForTriggerCompensation(new GetTriggerCompensationSchemaContextQuery(
                    triggerContext.OrchestrationVersionId,
                    triggerContext.TriggerBindingId));
                var trigger = await _triggerBindingApplicationService.GetById(new GetTriggerBindingByIdQuery(triggerContext.TriggerBindingId));
                return CreateResult(request.ContextKey, "Trigger compensation transformation", schemaContext, GetDsl(trigger?.CompensationDefinition?.Transformation));
            }

            return CreateFailure(request.ContextKey, "Invalid orchestration transformation context.");
        }
        catch (Exception exception)
        {
            return CreateFailure(request?.ContextKey, exception.Message);
        }
    }

    private ButterMorphMappingViewerLoadResult CreateResult(
        string contextKey,
        string name,
        OrchestrationSchemaContext schemaContext,
        string dsl)
    {
        var diagnostics = new List<string>();
        IStructureSchema target = null;

        if (schemaContext?.Target?.SchemaBinding?.Snapshot is null)
        {
            diagnostics.Add("The mapping target does not have a stored schema snapshot.");
        }
        else if (!_schemaImporter.TryImport(schemaContext.Target.SchemaBinding, out target, out var targetMessage))
        {
            diagnostics.Add(targetMessage);
        }

        ITransformationDocument document = null;
        if (!string.IsNullOrWhiteSpace(dsl))
        {
            try
            {
                document = _dslParser.Parse(new DslDefinition { Content = dsl }) as ITransformationDocument;
                if (document is null)
                {
                    diagnostics.Add("The stored DSL is not a ButterMorph transformation document.");
                }
            }
            catch (Exception exception)
            {
                diagnostics.Add($"The stored transformation DSL could not be parsed: {exception.Message}");
            }
        }

        return new ButterMorphMappingViewerLoadResult
        {
            Key = contextKey ?? string.Empty,
            Name = name,
            Description = "Read-only orchestration mapping.",
            Version = schemaContext?.OrchestrationVersion ?? string.Empty,
            Document = document,
            TargetSchema = target,
            DslContent = dsl ?? string.Empty,
            Message = string.Join(Environment.NewLine, diagnostics)
        };
    }

    private static string GetDsl(TransformationDefinition transformation)
        => (transformation?.Configuration as DslTransformationConfiguration)?.Dsl ?? string.Empty;

    private static ButterMorphMappingViewerLoadResult CreateFailure(string contextKey, string message)
        => new()
        {
            Key = contextKey ?? string.Empty,
            Name = "Orchestration transformation",
            DslContent = string.Empty,
            Message = message
        };
}
