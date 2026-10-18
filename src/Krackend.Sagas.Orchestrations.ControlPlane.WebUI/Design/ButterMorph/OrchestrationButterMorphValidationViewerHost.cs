namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;

/// <summary>
/// Provides read-only ButterMorph validation views for persisted execution conditions.
/// </summary>
public sealed class OrchestrationButterMorphValidationViewerHost : IButterMorphValidationViewerHost
{
    private readonly IStageApplicationService _stageApplicationService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly ITriggerBindingApplicationService _triggerBindingApplicationService;
    private readonly IOrchestrationButterMorphDesignerContextParser _contextParser;
    private readonly IDslParser _dslParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationButterMorphValidationViewerHost"/> class.
    /// </summary>
    public OrchestrationButterMorphValidationViewerHost(
        IStageApplicationService stageApplicationService,
        ITaskApplicationService taskApplicationService,
        ITriggerBindingApplicationService triggerBindingApplicationService,
        IOrchestrationButterMorphDesignerContextParser contextParser,
        IDslParser dslParser)
    {
        _stageApplicationService = stageApplicationService ?? throw new ArgumentNullException(nameof(stageApplicationService));
        _taskApplicationService = taskApplicationService ?? throw new ArgumentNullException(nameof(taskApplicationService));
        _triggerBindingApplicationService = triggerBindingApplicationService ?? throw new ArgumentNullException(nameof(triggerBindingApplicationService));
        _contextParser = contextParser ?? throw new ArgumentNullException(nameof(contextParser));
        _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
    }

    /// <inheritdoc />
    public async Task<ButterMorphValidationViewerLoadResult> Load(ButterMorphValidationViewerLoadRequest request)
    {
        try
        {
            if (_contextParser.TryParseStageExecutionCondition(request.ContextKey, out var stageContext))
            {
                var stage = await _stageApplicationService.GetById(new GetStageDefinitionByIdQuery(stageContext.StageDefinitionId));
                return CreateResult(request.ContextKey, "Stage execution condition", GetDsl(stage?.ExecutionCondition));
            }

            if (_contextParser.TryParseTaskExecutionCondition(request.ContextKey, out var taskContext))
            {
                var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(taskContext.TaskDefinitionId));
                return CreateResult(request.ContextKey, "Task execution condition", GetDsl(task?.ExecutionCondition));
            }

            if (_contextParser.TryParseTaskCompensationExecutionCondition(request.ContextKey, out var compensationContext))
            {
                var task = await _taskApplicationService.GetById(new GetTaskDefinitionByIdQuery(compensationContext.TaskDefinitionId));
                return CreateResult(request.ContextKey, "Task compensation execution condition", GetDsl(task?.CompensationDefinition?.ExecutionCondition));
            }

            if (_contextParser.TryParseTriggerCompensationExecutionCondition(request.ContextKey, out var triggerContext))
            {
                var trigger = await _triggerBindingApplicationService.GetById(new GetTriggerBindingByIdQuery(triggerContext.TriggerBindingId));
                return CreateResult(request.ContextKey, "Trigger compensation execution condition", GetDsl(trigger?.CompensationDefinition?.ExecutionCondition));
            }

            return CreateFailure(request.ContextKey, "Invalid orchestration execution condition context.");
        }
        catch (Exception exception)
        {
            return CreateFailure(request?.ContextKey, exception.Message);
        }
    }

    private ButterMorphValidationViewerLoadResult CreateResult(string contextKey, string name, string dsl)
    {
        var diagnostics = new List<string>();
        IValidationDocument document = null;

        if (!string.IsNullOrWhiteSpace(dsl))
        {
            try
            {
                document = _dslParser.Parse(new DslDefinition { Content = dsl }) as IValidationDocument;
                if (document is null)
                {
                    diagnostics.Add("The stored execution condition is an expression and does not have a visual validation document.");
                }
            }
            catch (Exception exception)
            {
                diagnostics.Add($"The stored execution condition DSL could not be parsed: {exception.Message}");
            }
        }

        return new ButterMorphValidationViewerLoadResult
        {
            Key = contextKey ?? string.Empty,
            Name = name,
            Description = "Read-only orchestration execution condition.",
            Document = document,
            DslContent = dsl ?? string.Empty,
            Message = string.Join(Environment.NewLine, diagnostics)
        };
    }

    private static string GetDsl(ExecutionCondition condition)
        => (condition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? string.Empty;

    private static ButterMorphValidationViewerLoadResult CreateFailure(string contextKey, string message)
        => new()
        {
            Key = contextKey ?? string.Empty,
            Name = "Orchestration execution condition",
            DslContent = string.Empty,
            Message = message
        };
}
