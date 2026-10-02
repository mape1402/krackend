using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Infrastructure;
using Krackend.Sagas.Orchestrations.SchemaRegistry;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationVersions;

/// <summary>
/// Represents roadmap management for one orchestration version.
/// </summary>
public sealed class DetailsModel : PageModel
{
    private readonly IOrchestrationApplicationService _orchestrationService;
    private readonly IOrchestrationVersionApplicationService _versionService;
    private readonly IStageApplicationService _stageService;
    private readonly ITaskApplicationService _taskService;
    private readonly ITriggerBindingApplicationService _triggerBindingService;
    private readonly ISchemaContractCatalog _schemaContractCatalog;
    private readonly string _defaultSchemaRegistryProviderKey;

    public DetailsModel(
        IOrchestrationApplicationService orchestrationService,
        IOrchestrationVersionApplicationService versionService,
        IStageApplicationService stageService,
        ITaskApplicationService taskService,
        ITriggerBindingApplicationService triggerBindingService,
        ISchemaContractCatalog schemaContractCatalog,
        IOptions<OrchestratorDesignWebUIOptions> uiOptions)
    {
        _orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _stageService = stageService ?? throw new ArgumentNullException(nameof(stageService));
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _triggerBindingService = triggerBindingService ?? throw new ArgumentNullException(nameof(triggerBindingService));
        _schemaContractCatalog = schemaContractCatalog ?? throw new ArgumentNullException(nameof(schemaContractCatalog));
        _defaultSchemaRegistryProviderKey = NormalizeProviderKey(uiOptions?.Value?.DefaultSchemaRegistryProviderKey);
    }

    [BindProperty(SupportsGet = true)]
    public string OrchestrationId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string VersionId { get; set; } = string.Empty;

    public OrchestrationDefinitionModel Orchestration { get; private set; }

    public OrchestrationVersionModel SelectedVersion { get; private set; }

    public bool CanEdit => SelectedVersion?.Status == OrchestrationVersionStatus.Draft;

    public IReadOnlyCollection<StageDefinitionModel> Stages { get; private set; } = Array.Empty<StageDefinitionModel>();

    public IReadOnlyCollection<TriggerBindingModel> TriggerBindings { get; private set; } = Array.Empty<TriggerBindingModel>();

    public IReadOnlyDictionary<string, int> StageTaskCounts { get; private set; } = new Dictionary<string, int>(StringComparer.Ordinal);

    [BindProperty]
    public CreateStageInput NewStage { get; set; } = new();

    [BindProperty]
    public UpsertTriggerInput TriggerInput { get; set; } = new();

    public IEnumerable<SelectListItem> TriggerTypes =>
        new[] { TriggerType.Event }.Select(x => new SelectListItem(x.ToString(), x.ToString()));

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
        ModelState.Clear();

        try
        {
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
        catch (Exception ex)
        {
            OrchestrationId = orchestrationId;
            VersionId = versionId;
            ErrorMessage = ToFriendlyErrorMessage(ex);
            await LoadDataAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpsertStageAsync(string orchestrationId, string versionId, CancellationToken cancellationToken = default)
    {
        ModelState.Clear();
        ValidateInputModel(NewStage, nameof(NewStage));

        if (!ModelState.IsValid)
        {
            OrchestrationId = orchestrationId;
            VersionId = versionId;
            await LoadDataAsync(cancellationToken);
            return Page();
        }

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

    /// <summary>
    /// Searches schema contracts available to trigger configuration.
    /// </summary>
    public async Task<IActionResult> OnGetSchemaContractsAsync(
        string contractKind = "",
        string term = "",
        int take = 25,
        CancellationToken cancellationToken = default)
    {
        var items = await _schemaContractCatalog.SearchAsync(
            new SchemaContractCatalogSearchRequest
            {
                ProviderKey = _defaultSchemaRegistryProviderKey,
                ContractKind = ParseEnum(contractKind, SchemaContractKind.Event),
                SearchText = term ?? string.Empty,
                Take = take
            },
            cancellationToken);

        return new JsonResult(items.Select(item => new
        {
            providerKey = item.ProviderKey,
            contractId = item.ContractId,
            contractKey = item.ContractKey,
            contractVersion = item.ContractVersion,
            contractKind = item.ContractKind.ToString(),
            contentHash = item.ContentHash,
            displayName = string.IsNullOrWhiteSpace(item.DisplayName)
                ? $"{item.ContractKey} v{item.ContractVersion}"
                : item.DisplayName
        }));
    }

    public async Task<IActionResult> OnPostDeleteStageAsync(string orchestrationId, string versionId, string stageId, CancellationToken cancellationToken = default)
    {
        await _stageService.Delete(new DeleteStageDefinitionCommand(stageId), cancellationToken);
        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    /// <summary>
    /// Sets enabled state of one stage from roadmap card toggle.
    /// </summary>
    public async Task<IActionResult> OnPostSetStageEnabledAsync([FromBody] SetStageEnabledRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.StageId))
        {
            return BadRequest();
        }

        var updated = request.IsEnabled
            ? await _stageService.Enable(new EnableStageDefinitionCommand(request.StageId), cancellationToken)
            : await _stageService.Disable(new DisableStageDefinitionCommand(request.StageId), cancellationToken);

        return new JsonResult(new { success = updated });
    }

    public async Task<IActionResult> OnPostUpsertTriggerAsync(string orchestrationId, string versionId, CancellationToken cancellationToken = default)
    {
        ModelState.Clear();

        TriggerBindingModel existingTrigger = null;
        if (!string.IsNullOrWhiteSpace(TriggerInput.TriggerId))
        {
            existingTrigger = await _triggerBindingService.GetById(new GetTriggerBindingByIdQuery(TriggerInput.TriggerId), cancellationToken);
            if (existingTrigger is null)
            {
                return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
            }
        }

        ValidateInputModel(TriggerInput, nameof(TriggerInput));

        ValidateTriggerInput();
        if (!ModelState.IsValid)
        {
            OrchestrationId = orchestrationId;
            VersionId = versionId;
            await LoadDataAsync(cancellationToken);
            return Page();
        }

        var key = TriggerInput.Key.Trim();
        var triggerType = ResolveTriggerType(ParseEnum(TriggerInput.TriggerType, TriggerType.Event));
        var triggerChannel = BuildTriggerChannel(triggerType, TriggerInput, orchestrationId, _defaultSchemaRegistryProviderKey);
        var description = TriggerInput.Description ?? string.Empty;
        var compensation = BuildTriggerCompensationDefinition(TriggerInput, orchestrationId, _defaultSchemaRegistryProviderKey);

        if (existingTrigger is not null)
        {
            await _triggerBindingService.Update(
                new UpdateTriggerBindingCommand(
                    existingTrigger.Id,
                    key,
                    triggerType,
                    triggerChannel,
                    existingTrigger.IsEnabled,
                    description,
                    compensation),
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
                description,
                compensation),
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
            OrchestrationVersionStatus.Draft => new[] { "SetInReview", "Archive" },
            OrchestrationVersionStatus.InReview => new[] { "Approve", "ReturnToDraft", "Archive" },
            OrchestrationVersionStatus.Approved => new[] { "Deploy", "ReopenReview", "Archive" },
            OrchestrationVersionStatus.Deployed => new[] { "Deprecate", "Archive" },
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
            ErrorMessage = ToFriendlyErrorMessage(ex);
        }
    }

    private static string ToFriendlyErrorMessage(Exception exception)
        => exception switch
        {
            OrchestrationArtifactDslValidationException dslException =>
                $"The version cannot be published because the DSL configuration at '{dslException.Path}' is not valid. Review that transformation, validation, or condition and try again.",
            InvalidOperationException invalidOperation when !string.IsNullOrWhiteSpace(invalidOperation.Message) =>
                invalidOperation.Message,
            _ => "The operation could not be completed. Review the captured data and try again."
        };

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

    private void ValidateTriggerInput()
    {
        var triggerType = ResolveTriggerType(ParseEnum(TriggerInput.TriggerType, TriggerType.Event));
        if (triggerType == TriggerType.Event && string.IsNullOrWhiteSpace(TriggerInput.EventTopic))
        {
            ModelState.AddModelError(nameof(TriggerInput.EventTopic), "Capture the event topic.");
        }

        if (TriggerInput.HasEventValidation && string.IsNullOrWhiteSpace(TriggerInput.EventValidationDsl))
        {
            ModelState.AddModelError(nameof(TriggerInput.EventValidationDsl), "Capture the event validation DSL.");
        }

        if (TriggerInput.HasCompensation && string.IsNullOrWhiteSpace(TriggerInput.CompensationMessagingTopic))
        {
            ModelState.AddModelError(nameof(TriggerInput.CompensationMessagingTopic), "Capture the compensation topic.");
        }

        if (!string.IsNullOrWhiteSpace(TriggerInput.EventSchemaRegistryProviderId) &&
            !Ulid.TryParse(TriggerInput.EventSchemaRegistryProviderId, out _))
        {
            ModelState.AddModelError(nameof(TriggerInput.EventSchemaRegistryProviderId), "Capture a valid schema registry provider id.");
        }
    }

    private void ValidateInputModel<TInput>(TInput input, string modelName)
        where TInput : class
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(input);
        Validator.TryValidateObject(input, validationContext, validationResults, validateAllProperties: true);

        foreach (var validationResult in validationResults)
        {
            var memberNames = validationResult.MemberNames?.Where(memberName => !string.IsNullOrWhiteSpace(memberName)).ToArray();
            if (memberNames is null || memberNames.Length == 0)
            {
                ModelState.AddModelError(modelName, validationResult.ErrorMessage ?? "The value is invalid.");
                continue;
            }

            foreach (var memberName in memberNames)
            {
                ModelState.AddModelError($"{modelName}.{memberName}", validationResult.ErrorMessage ?? "The value is invalid.");
            }
        }
    }

    private static object BuildTriggerEditPayload(TriggerBindingModel trigger)
    {
        var eventChannel = trigger.TriggerChannel as EventTriggerChannel;
        var schema = eventChannel?.SchemaBinding;
        var compensationMessaging = trigger.CompensationDefinition?.Configuration as MessagingTaskConfiguration;
        var compensationSchema = compensationMessaging?.SchemaBinding;

        return new
        {
            TriggerId = trigger.Id,
            Key = trigger.Key ?? string.Empty,
            TriggerType = trigger.TriggerType.ToString(),
            Description = trigger.Description ?? string.Empty,
            EventTopic = eventChannel?.Topic ?? string.Empty,
            EventVersion = eventChannel?.Version.ToString() ?? "1.0.0",
            HasEventSchemaValidation = eventChannel?.HasSchemaValidation ?? false,
            EventSchemaContractKey = schema?.ContractKey ?? string.Empty,
            EventSchemaContractVersion = schema?.ContractVersion.ToString() ?? "1.0.0",
            EventSchemaRegistryProviderId = schema is null || schema.RegistryProviderId == default ? string.Empty : schema.RegistryProviderId.ToString(),
            EventSchemaStrictMode = schema?.StrictMode ?? false,
            HasEventValidation = eventChannel?.HasValidation ?? false,
            EventValidationDsl = (eventChannel?.Validation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty,
            EventValidationErrorCode = eventChannel?.Validation?.ErrorCode ?? "TriggerValidationFailed",
            HasCompensation = trigger.CompensationDefinition is not null,
            CompensationMessagingTopic = compensationMessaging?.Topic ?? string.Empty,
            CompensationMessagingVersion = compensationMessaging?.Version.ToString() ?? "1.0.0",
            HasCompensationSchemaValidation = compensationMessaging?.HasSchemaValidation ?? false,
            CompensationSchemaContractKey = compensationSchema?.ContractKey ?? string.Empty,
            CompensationSchemaContractVersion = compensationSchema?.ContractVersion.ToString() ?? "1.0.0",
            CompensationSchemaRegistryProviderId = compensationSchema is null || compensationSchema.RegistryProviderId == default ? string.Empty : compensationSchema.RegistryProviderId.ToString(),
            CompensationSchemaStrictMode = compensationSchema?.StrictMode ?? false,
            HasCompensationExecutionCondition = trigger.CompensationDefinition?.HasExecutionCondition ?? false,
            CompensationConditionDslExpression = (trigger.CompensationDefinition?.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? "true",
            HasCompensationTransformation = trigger.CompensationDefinition?.HasTransformation ?? false,
            CompensationTransformationDsl = (trigger.CompensationDefinition?.Transformation?.Configuration as DslTransformationConfiguration)?.Dsl ?? string.Empty,
        };
    }

    private static ITriggerChannel BuildTriggerChannel(
        TriggerType triggerType,
        UpsertTriggerInput input,
        string orchestrationId,
        string defaultSchemaRegistryProviderKey)
    {
        var hasSchemaBinding = !string.IsNullOrWhiteSpace(input.EventSchemaContractKey);

        return triggerType switch
        {
            TriggerType.Event => new EventTriggerChannel
            {
                HasSchemaValidation = input.HasEventSchemaValidation,
                HasValidation = input.HasEventValidation,
                Validation = input.HasEventValidation
                    ? BuildValidation(input.EventValidationDsl, input.EventValidationErrorCode)
                    : null,
                Topic = input.EventTopic.Trim(),
                Version = ParseSemanticVersion(input.EventVersion, new SemanticVersion(1, 0, 0)),
                SchemaBinding = hasSchemaBinding ? CreateSchemaBinding(
                    input.EventSchemaContractKey,
                    input.EventSchemaContractVersion,
                    input.EventSchemaRegistryProviderId,
                    input.EventSchemaStrictMode,
                    input.HasEventSchemaValidation,
                    orchestrationId,
                    defaultSchemaRegistryProviderKey) : null,
            },
            _ => new EventTriggerChannel
            {
                HasSchemaValidation = false,
                HasValidation = false,
                Topic = input.EventTopic.Trim(),
                Version = new SemanticVersion(1, 0, 0),
            },
        };
    }

    private static ValidationDefinition BuildValidation(string dsl, string errorCode)
        => new()
        {
            Engine = EngineType.DSL,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "TriggerValidationFailed" : errorCode.Trim(),
            Configuration = new DslValidationConfiguration
            {
                Dsl = dsl ?? string.Empty
            }
        };

    private static CompensationDefinition BuildTriggerCompensationDefinition(
        UpsertTriggerInput input,
        string orchestrationId,
        string defaultSchemaRegistryProviderKey)
    {
        if (!input.HasCompensation)
        {
            return null;
        }

        var hasSchemaBinding = !string.IsNullOrWhiteSpace(input.CompensationSchemaContractKey);
        return new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            DispatchType = TaskDispatchType.FireAndForget,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = input.CompensationMessagingTopic.Trim(),
                Version = ParseSemanticVersion(input.CompensationMessagingVersion, new SemanticVersion(1, 0, 0)),
                HasSchemaValidation = input.HasCompensationSchemaValidation,
                SchemaBinding = hasSchemaBinding
                    ? CreateSchemaBinding(
                        input.CompensationSchemaContractKey,
                        input.CompensationSchemaContractVersion,
                        input.CompensationSchemaRegistryProviderId,
                        input.CompensationSchemaStrictMode,
                        input.HasCompensationSchemaValidation,
                        orchestrationId,
                        defaultSchemaRegistryProviderKey,
                        SchemaContractKind.Command)
                    : null,
            },
            HasExecutionCondition = input.HasCompensationExecutionCondition,
            ExecutionCondition = input.HasCompensationExecutionCondition
                ? BuildExecutionCondition(input.CompensationConditionDslExpression)
                : null,
            HasTransformation = input.HasCompensationTransformation,
            Transformation = input.HasCompensationTransformation
                ? BuildTransformation(input.CompensationTransformationDsl)
                : null,
        };
    }

    private static ExecutionCondition BuildExecutionCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression(string.IsNullOrWhiteSpace(expression) ? "true" : expression)
            }
        };

    private static TransformationDefinition BuildTransformation(string dsl)
        => string.IsNullOrWhiteSpace(dsl)
            ? null
            : new TransformationDefinition
            {
                Engine = EngineType.DSL,
                Configuration = new DslTransformationConfiguration
                {
                    Dsl = dsl,
                    SourceContextHash = string.Empty,
                    TargetSchemaHash = string.Empty,
                    SemanticDiagnosticsJson = "{}"
                }
            };

    private static SchemaBinding CreateSchemaBinding(
        string contractKey,
        string contractVersion,
        string registryProviderId,
        bool strictMode,
        bool isValidationEnabled,
        string orchestrationId,
        string defaultSchemaRegistryProviderKey,
        SchemaContractKind contractKind = SchemaContractKind.Event)
    {
        return new SchemaBinding
        {
            Id = Id.New(),
            ElementType = ElementType.Orchestration,
            ElementId = ParseId(orchestrationId),
            ContractId = Id.New(),
            ContractKey = contractKey.Trim(),
            ContractVersion = ParseSemanticVersion(contractVersion, new SemanticVersion(1, 0, 0)),
            RegistryProviderId = ParseId(registryProviderId),
            RegistryProviderKey = defaultSchemaRegistryProviderKey,
            ContractKind = contractKind,
            StrictMode = strictMode,
            IsValidationEnabled = isValidationEnabled,
        };
    }

    private static string NormalizeProviderKey(string providerKey)
        => string.IsNullOrWhiteSpace(providerKey) ? "knowl" : providerKey.Trim();

    private static TriggerType ResolveTriggerType(TriggerType requested)
        => requested == TriggerType.Event ? requested : TriggerType.Event;

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
        [RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(?:[._][A-Za-z0-9]+)*$", ErrorMessage = "Use letters or numbers separated by dot or underscore, starting with a letter.")]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }

    public sealed class UpsertTriggerInput
    {
        public string TriggerId { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(?:[._][A-Za-z0-9]+)*$", ErrorMessage = "Use letters or numbers separated by dot or underscore, starting with a letter.")]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string TriggerType { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.TriggerType.Event.ToString();

        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string EventTopic { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string EventVersion { get; set; } = "1.0.0";

        public bool HasEventSchemaValidation { get; set; }

        public string EventSchemaContractKey { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string EventSchemaContractVersion { get; set; } = "1.0.0";

        public string EventSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool EventSchemaStrictMode { get; set; }

        public bool HasEventValidation { get; set; }

        public string EventValidationDsl { get; set; } = string.Empty;

        public string EventValidationErrorCode { get; set; } = "TriggerValidationFailed";

        public bool HasCompensation { get; set; }

        [MaxLength(512)]
        public string CompensationMessagingTopic { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string CompensationMessagingVersion { get; set; } = "1.0.0";

        public bool HasCompensationSchemaValidation { get; set; }

        public string CompensationSchemaContractKey { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string CompensationSchemaContractVersion { get; set; } = "1.0.0";

        public string CompensationSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool CompensationSchemaStrictMode { get; set; }

        public bool HasCompensationExecutionCondition { get; set; }

        public string CompensationConditionDslExpression { get; set; } = "true";

        public bool HasCompensationTransformation { get; set; }

        public string CompensationTransformationDsl { get; set; } = string.Empty;
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
    /// Represents toggle payload for stage enabled state.
    /// </summary>
    public sealed class SetStageEnabledRequest
    {
        /// <summary>
        /// Gets or sets stage identifier.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets enabled state.
        /// </summary>
        public bool IsEnabled { get; set; }
    }

}
