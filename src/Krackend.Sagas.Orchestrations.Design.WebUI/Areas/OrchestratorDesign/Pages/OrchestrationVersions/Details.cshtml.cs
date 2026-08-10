using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.Design.Interaction;
using Krackend.Sagas.Orchestrations.Design.WebUI.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Design.WebUI.Areas.OrchestratorDesign.Pages.OrchestrationVersions;

/// <summary>
/// Represents roadmap management for one orchestration version.
/// </summary>
public sealed class DetailsModel : PageModel
{
    private readonly IOrchestrationInteractionService _orchestrationService;
    private readonly IOrchestrationVersionInteractionService _versionService;
    private readonly IStageInteractionService _stageService;
    private readonly ITaskInteractionService _taskService;
    private readonly ITriggerBindingInteractionService _triggerBindingService;

    public DetailsModel(
        IOrchestrationInteractionService orchestrationService,
        IOrchestrationVersionInteractionService versionService,
        IStageInteractionService stageService,
        ITaskInteractionService taskService,
        ITriggerBindingInteractionService triggerBindingService)
    {
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _stageService = stageService ?? throw new ArgumentNullException(nameof(stageService));
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _triggerBindingService = triggerBindingService ?? throw new ArgumentNullException(nameof(triggerBindingService));
    }

    [BindProperty(SupportsGet = true)]
    public string OrchestrationId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string VersionId { get; set; } = string.Empty;

    public OrchestrationDefinitionModel Orchestration { get; private set; }

    public OrchestrationVersionModel SelectedVersion { get; private set; }

    public IReadOnlyCollection<StageDefinitionModel> Stages { get; private set; } = Array.Empty<StageDefinitionModel>();

    public IReadOnlyCollection<TriggerBindingModel> TriggerBindings { get; private set; } = Array.Empty<TriggerBindingModel>();

    public IReadOnlyDictionary<string, int> StageTaskCounts { get; private set; } = new Dictionary<string, int>(StringComparer.Ordinal);

    [BindProperty]
    public CreateStageInput NewStage { get; set; } = new();

    [BindProperty]
    public UpsertTriggerInput TriggerInput { get; set; } = new();

    public IEnumerable<SelectListItem> TriggerTypes =>
        Enum.GetValues<TriggerType>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> EngineTypes =>
        Enum.GetValues<EngineType>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string orchestrationId, string versionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId) || string.IsNullOrWhiteSpace(versionId))
        {
            return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign" });
        }

        OrchestrationId = orchestrationId;
        VersionId = versionId;

        await LoadDataAsync(cancellationToken);
        if (Orchestration is null || SelectedVersion is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTransitionVersionAsync(string orchestrationId, string versionId, string action, CancellationToken cancellationToken = default)
    {
        const string actor = "web-ui";

        switch (action)
        {
            case "SetInReview":
                await _versionService.SetInReview(new SetOrchestrationVersionInReviewCommand(versionId), cancellationToken);
                break;
            case "ReturnToDraft":
                await _versionService.ReturnToDraft(new ReturnOrchestrationVersionToDraftCommand(versionId), cancellationToken);
                break;
            case "ReopenReview":
                await _versionService.ReopenReview(new ReopenOrchestrationVersionReviewCommand(versionId, actor), cancellationToken);
                break;
            case "Approve":
                await _versionService.Approve(new ApproveOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
            case "Deploy":
                await _versionService.Deploy(new DeployOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
            case "Deprecate":
                await _versionService.Deprecate(new DeprecateOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
            case "Archive":
                await _versionService.Archive(new ArchiveOrchestrationVersionCommand(versionId, actor), cancellationToken);
                break;
        }

        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    public async Task<IActionResult> OnPostUpsertStageAsync(string orchestrationId, string versionId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(NewStage.StageId))
        {
            var current = await _stageService.GetById(new GetStageDefinitionByIdQuery(NewStage.StageId), cancellationToken);
            if (current is null)
            {
                return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
            }

            await _stageService.Update(
                new UpdateStageDefinitionCommand(
                    current.Id,
                    string.IsNullOrWhiteSpace(NewStage.Key) ? current.Key : NewStage.Key.Trim(),
                    string.IsNullOrWhiteSpace(NewStage.Name) ? current.Name : NewStage.Name.Trim(),
                    NewStage.Description ?? string.Empty,
                    current.Order,
                    current.ExecutionCondition),
                cancellationToken);

            return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
        }

        var currentStages = (await _stageService.GetAll(new GetStageDefinitionsQuery(versionId), cancellationToken)).ToArray();

        await _stageService.Create(
            new CreateStageDefinitionCommand(
                versionId,
                NewStage.Key,
                NewStage.Name,
                NewStage.Description ?? string.Empty,
                currentStages.Length,
                null),
            cancellationToken);

        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    public async Task<IActionResult> OnGetStageForEditAsync(string stageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return BadRequest();
        }

        var stage = await _stageService.GetById(new GetStageDefinitionByIdQuery(stageId), cancellationToken);
        if (stage is null)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            stageId = stage.Id,
            key = stage.Key,
            name = stage.Name,
            description = stage.Description ?? string.Empty,
        });
    }

    public async Task<IActionResult> OnPostDeleteStageAsync(string orchestrationId, string versionId, string stageId, CancellationToken cancellationToken = default)
    {
        await _stageService.Delete(new DeleteStageDefinitionCommand(stageId), cancellationToken);
        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    /// <summary>
    /// Loads execution condition payload for one stage.
    /// </summary>
    public async Task<IActionResult> OnGetStageExecutionConditionForEditAsync(string stageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return BadRequest();
        }

        var stage = await _stageService.GetById(new GetStageDefinitionByIdQuery(stageId), cancellationToken);
        if (stage is null)
        {
            return NotFound();
        }

        return new JsonResult(BuildStageExecutionConditionEditPayload(stage));
    }

    /// <summary>
    /// Updates execution condition of one stage.
    /// </summary>
    public async Task<IActionResult> OnPostSetStageExecutionConditionAsync([FromBody] SetStageExecutionConditionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.StageId))
        {
            return BadRequest();
        }

        var executionCondition = request.HasExecutionCondition
            ? BuildExecutionCondition(request.ConditionEngine, request.ConditionDslExpression)
            : null;

        var updated = await _stageService.SetExecutionCondition(
            new SetStageExecutionConditionCommand(request.StageId, executionCondition),
            cancellationToken);

        var stage = await _stageService.GetById(new GetStageDefinitionByIdQuery(request.StageId), cancellationToken);
        if (stage is null)
        {
            return new JsonResult(new { success = false });
        }

        var payload = BuildStageExecutionConditionEditPayload(stage);
        return new JsonResult(new
        {
            success = updated,
            stageId = payload.StageId,
            hasExecutionCondition = payload.HasExecutionCondition,
            conditionEngine = payload.ConditionEngine,
            conditionDslExpression = payload.ConditionDslExpression,
        });
    }

    public async Task<IActionResult> OnPostUpsertTriggerAsync(string orchestrationId, string versionId, CancellationToken cancellationToken = default)
    {
        var key = string.IsNullOrWhiteSpace(TriggerInput.Key) ? "trigger.key" : TriggerInput.Key.Trim();
        var triggerType = ParseEnum(TriggerInput.TriggerType, TriggerType.Event);
        var triggerChannel = BuildTriggerChannel(triggerType, TriggerInput, orchestrationId);
        var description = TriggerInput.Description ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(TriggerInput.TriggerId))
        {
            var existing = await _triggerBindingService.GetById(new GetTriggerBindingByIdQuery(TriggerInput.TriggerId), cancellationToken);
            if (existing is null)
            {
                return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
            }

            await _triggerBindingService.Update(
                new UpdateTriggerBindingCommand(
                    existing.Id,
                    key,
                    triggerType,
                    triggerChannel,
                    existing.IsEnabled,
                    description),
                cancellationToken);

            return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
        }

        await _triggerBindingService.Create(
            new CreateTriggerBindingCommand(
                versionId,
                key,
                triggerType,
                triggerChannel,
                true,
                description),
            cancellationToken);

        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    public async Task<IActionResult> OnPostDeleteTriggerAsync(string orchestrationId, string versionId, string triggerId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(triggerId))
        {
            await _triggerBindingService.Delete(new DeleteTriggerBindingCommand(triggerId), cancellationToken);
        }

        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    /// <summary>
    /// Sets enabled state of one trigger from board card toggle.
    /// </summary>
    public async Task<IActionResult> OnPostSetTriggerEnabledAsync([FromBody] SetTriggerEnabledRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TriggerId))
        {
            return BadRequest();
        }

        var updated = request.IsEnabled
            ? await _triggerBindingService.Enable(new EnableTriggerBindingCommand(request.TriggerId), cancellationToken)
            : await _triggerBindingService.Disable(new DisableTriggerBindingCommand(request.TriggerId), cancellationToken);

        return new JsonResult(new { success = updated });
    }

    public async Task<IActionResult> OnGetTriggerForEditAsync(string triggerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(triggerId))
        {
            return BadRequest();
        }

        var trigger = await _triggerBindingService.GetById(new GetTriggerBindingByIdQuery(triggerId), cancellationToken);
        if (trigger is null)
        {
            return NotFound();
        }

        return new JsonResult(BuildTriggerEditPayload(trigger));
    }

    /// <summary>
    /// Reorders stages using drag and drop sequence.
    /// </summary>
    public async Task<IActionResult> OnPostReorderStagesAsync([FromBody] ReorderStagesRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.VersionId) || request.StageIds is null || request.StageIds.Count == 0)
        {
            return BadRequest();
        }

        var stages = (await _stageService.GetAll(new GetStageDefinitionsQuery(request.VersionId), cancellationToken))
            .ToDictionary(x => x.Id, StringComparer.Ordinal);

        var ordered = request.StageIds.Where(stages.ContainsKey).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var stage = stages[ordered[index]];

            await _stageService.Update(
                new UpdateStageDefinitionCommand(
                    stage.Id,
                    stage.Key,
                    stage.Name,
                    stage.Description,
                    index,
                    stage.ExecutionCondition),
                cancellationToken);
        }

        return new JsonResult(new { success = true });
    }

    public IEnumerable<string> GetAllowedActions(OrchestrationVersionStatus status)
    {
        return status switch
        {
            OrchestrationVersionStatus.Draft => new[] { "SetInReview" },
            OrchestrationVersionStatus.InReview => new[] { "Approve", "ReturnToDraft" },
            OrchestrationVersionStatus.Approved => new[] { "Deploy", "ReopenReview" },
            OrchestrationVersionStatus.Deployed => new[] { "Deprecate" },
            OrchestrationVersionStatus.Deprecated => new[] { "Archive" },
            _ => Array.Empty<string>()
        };
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            Orchestration = await _orchestrationService.GetById(new GetOrchestrationDefinitionByIdQuery(OrchestrationId), cancellationToken);
            if (Orchestration is null)
            {
                return;
            }

            SelectedVersion = await _versionService.GetById(new GetOrchestrationVersionByIdQuery(VersionId), cancellationToken);
            if (SelectedVersion is null)
            {
                return;
            }

            Stages = (await _stageService.GetAll(new GetStageDefinitionsQuery(SelectedVersion.Id), cancellationToken))
                .OrderBy(x => x.Order)
                .ToArray();

            var stageTaskCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var stage in Stages)
            {
                var tasks = await _taskService.GetAll(new GetTaskDefinitionsQuery(stage.Id), cancellationToken);
                stageTaskCounts[stage.Id] = tasks.Count();
            }

            StageTaskCounts = stageTaskCounts;

            TriggerBindings = (await _triggerBindingService.GetAll(new GetTriggerBindingsQuery(SelectedVersion.Id), cancellationToken))
                .ToArray();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task NormalizeStageOrderAsync(string versionId, CancellationToken cancellationToken)
    {
        var stages = (await _stageService.GetAll(new GetStageDefinitionsQuery(versionId), cancellationToken))
            .OrderBy(x => x.Order)
            .ToArray();

        for (var index = 0; index < stages.Length; index++)
        {
            var stage = stages[index];
            if (stage.Order == index)
            {
                continue;
            }

            await _stageService.Update(
                new UpdateStageDefinitionCommand(
                    stage.Id,
                    stage.Key,
                    stage.Name,
                    stage.Description,
                    index,
                    stage.ExecutionCondition),
                cancellationToken);
        }
    }

    private static object BuildTriggerEditPayload(TriggerBindingModel trigger)
    {
        var eventChannel = trigger.TriggerChannel as EventTriggerChannel;
        var schema = eventChannel?.SchemaBinding;

        return new
        {
            TriggerId = trigger.Id,
            Key = trigger.Key ?? string.Empty,
            TriggerType = trigger.TriggerType.ToString(),
            Description = trigger.Description ?? string.Empty,
            EventTopic = eventChannel?.Topic ?? string.Empty,
            EventVersion = eventChannel?.Version.ToString() ?? "1.0.0",
            EventSchemaContractKey = schema?.ContractKey ?? "contract.placeholder",
            EventSchemaContractVersion = schema?.ContractVersion.ToString() ?? "1.0.0",
            EventSchemaRegistryProviderId = schema is null || schema.RegistryProviderId == default ? string.Empty : schema.RegistryProviderId.ToString(),
            EventSchemaStrictMode = schema?.StrictMode ?? false,
        };
    }

    private static StageExecutionConditionPayload BuildStageExecutionConditionEditPayload(StageDefinitionModel stage)
    {
        var dsl = stage.ExecutionCondition?.Configuration as DslConditionConfiguration;
        var expressionText = dsl?.Expression.ToString() ?? "true";
        var hasExecutionCondition = stage.ExecutionCondition is not null;

        return new StageExecutionConditionPayload
        {
            StageId = stage.Id,
            HasExecutionCondition = hasExecutionCondition,
            ConditionEngine = stage.ExecutionCondition?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ConditionDslExpression = expressionText,
        };
    }

    private sealed class StageExecutionConditionPayload
    {
        public string StageId { get; set; } = string.Empty;

        public bool HasExecutionCondition { get; set; }

        public string ConditionEngine { get; set; } = EngineType.DSL.ToString();

        public string ConditionDslExpression { get; set; } = "true";
    }

    private static ExecutionCondition BuildExecutionCondition(string engineText, string dslExpression)
    {
        var engine = ParseEnum(engineText, EngineType.DSL);

        if (engine == EngineType.DSL)
        {
            return new ExecutionCondition
            {
                Engine = engine,
                Configuration = new DslConditionConfiguration
                {
                    Expression = new Expression(string.IsNullOrWhiteSpace(dslExpression) ? "true" : dslExpression)
                }
            };
        }

        return new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression("true")
            }
        };
    }

    private static ITriggerChannel BuildTriggerChannel(TriggerType triggerType, UpsertTriggerInput input, string orchestrationId)
    {
        return triggerType switch
        {
            TriggerType.Event => new EventTriggerChannel
            {
                Topic = string.IsNullOrWhiteSpace(input.EventTopic) ? "orchestrator.trigger.topic" : input.EventTopic,
                Version = ParseSemanticVersion(input.EventVersion, new SemanticVersion(1, 0, 0)),
                SchemaBinding = CreateSchemaBinding(
                    input.EventSchemaContractKey,
                    input.EventSchemaContractVersion,
                    input.EventSchemaRegistryProviderId,
                    input.EventSchemaStrictMode,
                    orchestrationId),
            },
            _ => new EventTriggerChannel
            {
                Topic = "orchestrator.trigger.topic",
                Version = new SemanticVersion(1, 0, 0),
                SchemaBinding = CreateSchemaBinding(
                    "contract.placeholder",
                    "1.0.0",
                    string.Empty,
                    false,
                    orchestrationId),
            },
        };
    }

    private static SchemaBinding CreateSchemaBinding(
        string contractKey,
        string contractVersion,
        string registryProviderId,
        bool strictMode,
        string orchestrationId)
    {
        return new SchemaBinding
        {
            Id = Id.New(),
            ElementType = ElementType.Orchestration,
            ElementId = ParseId(orchestrationId),
            ContractId = Id.New(),
            ContractKey = string.IsNullOrWhiteSpace(contractKey) ? "contract.placeholder" : contractKey,
            ContractVersion = ParseSemanticVersion(contractVersion, new SemanticVersion(1, 0, 0)),
            RegistryProviderId = ParseId(registryProviderId),
            StrictMode = strictMode,
        };
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    }

    private static SemanticVersion ParseSemanticVersion(string value, SemanticVersion fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return fallback;
        }

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) || !int.TryParse(parts[2], out var patch))
        {
            return fallback;
        }

        return new SemanticVersion(Math.Max(0, major), Math.Max(0, minor), Math.Max(0, patch));
    }

    private static Id ParseId(string value)
    {
        return Ulid.TryParse(value, out var parsed)
            ? new Id(parsed)
            : Id.New();
    }

    public sealed class CreateStageInput
    {
        public string StageId { get; set; } = string.Empty;

        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }

    public sealed class UpsertTriggerInput
    {
        public string TriggerId { get; set; } = string.Empty;

        [Required]
        public string Key { get; set; } = "trigger.key";

        [Required]
        public string TriggerType { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.TriggerType.Event.ToString();

        public string Description { get; set; } = string.Empty;

        public string EventTopic { get; set; } = "orchestrator.trigger.topic";

        public string EventVersion { get; set; } = "1.0.0";

        public string EventSchemaContractKey { get; set; } = "contract.placeholder";

        public string EventSchemaContractVersion { get; set; } = "1.0.0";

        public string EventSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool EventSchemaStrictMode { get; set; }
    }

    /// <summary>
    /// Represents reorder request payload for stage drag and drop.
    /// </summary>
    public sealed class ReorderStagesRequest
    {
        /// <summary>
        /// Gets or sets version identifier.
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets ordered stage identifiers.
        /// </summary>
        public List<string> StageIds { get; set; } = new();
    }

    /// <summary>
    /// Represents toggle payload for trigger enabled state.
    /// </summary>
    public sealed class SetTriggerEnabledRequest
    {
        /// <summary>
        /// Gets or sets trigger identifier.
        /// </summary>
        public string TriggerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets enabled state.
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Represents payload to set stage execution condition.
    /// </summary>
    public sealed class SetStageExecutionConditionRequest
    {
        /// <summary>
        /// Gets or sets stage identifier.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether execution condition is active.
        /// </summary>
        public bool HasExecutionCondition { get; set; }

        /// <summary>
        /// Gets or sets execution engine.
        /// </summary>
        public string ConditionEngine { get; set; } = EngineType.DSL.ToString();

        /// <summary>
        /// Gets or sets DSL expression.
        /// </summary>
        public string ConditionDslExpression { get; set; } = "true";
    }
}
