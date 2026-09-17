using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Infrastructure;
using Krackend.Sagas.Orchestrations.SchemaRegistry;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Areas.OrchestratorDesign.Pages.OrchestrationStages;

/// <summary>
/// Represents stage-level task management.
/// </summary>
public sealed class DetailsModel : PageModel
{
    private readonly IStageApplicationService _stageService;
    private readonly ITaskApplicationService _taskService;
    private readonly IParallelGroupApplicationService _parallelGroupService;
    private readonly IOrchestrationVersionApplicationService _versionService;
    private readonly IOrchestrationSchemaContextApplicationService _schemaContextService;
    private readonly string _defaultSchemaRegistryProviderKey;

    public DetailsModel(
        IStageApplicationService stageService,
        ITaskApplicationService taskService,
        IParallelGroupApplicationService parallelGroupService,
        IOrchestrationVersionApplicationService versionService,
        IOrchestrationSchemaContextApplicationService schemaContextService,
        IOptions<OrchestratorDesignWebUIOptions> uiOptions)
    {
        _stageService = stageService ?? throw new ArgumentNullException(nameof(stageService));
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _parallelGroupService = parallelGroupService ?? throw new ArgumentNullException(nameof(parallelGroupService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _schemaContextService = schemaContextService ?? throw new ArgumentNullException(nameof(schemaContextService));
        _defaultSchemaRegistryProviderKey = NormalizeProviderKey(uiOptions?.Value?.DefaultSchemaRegistryProviderKey);
    }

    [BindProperty(SupportsGet = true)]
    public string OrchestrationId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string VersionId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string StageId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string EditTaskId { get; set; } = string.Empty;

    public StageDefinitionModel Stage { get; private set; }

    public OrchestrationVersionModel SelectedVersion { get; private set; }

    public bool CanEdit => SelectedVersion?.Status == OrchestrationVersionStatus.Draft;

    public IReadOnlyCollection<TaskDefinitionModel> Tasks { get; private set; } = Array.Empty<TaskDefinitionModel>();

    public IReadOnlyCollection<ParallelGroupDefinitionModel> ParallelGroups { get; private set; } = Array.Empty<ParallelGroupDefinitionModel>();

    [BindProperty]
    public CreateTaskInput NewTask { get; set; } = new();

    public IEnumerable<SelectListItem> TaskKinds => new[] { TaskKind.Messaging }.Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> ExecutionModes => Enum.GetValues<TaskExecutionMode>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> DispatchTypes =>
        new[] { TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback }
            .Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> CompensationDispatchTypes =>
        new[] { TaskDispatchType.FireAndForget }
            .Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> OnErrorPolicies => Enum.GetValues<OnErrorPolicy>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> EngineTypes => new[] { EngineType.DSL }.Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> RetryStrategyTypes => new[] { RetryStrategyType.Fixed }.Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> TimeoutBehaviors => Enum.GetValues<TimeoutBehavior>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> TimeoutActions => Enum.GetValues<OrchestrationActionOnTimeout>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public string ErrorMessage { get; private set; } = string.Empty;

    public bool ShouldOpenEditModal { get; private set; }

    public string EditTaskPayloadJson { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string orchestrationId, string versionId, string stageId, string editTaskId = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId) || string.IsNullOrWhiteSpace(versionId) || string.IsNullOrWhiteSpace(stageId))
        {
            return RedirectToPage("/Orchestrations/Index", new { area = "OrchestratorDesign" });
        }

        OrchestrationId = orchestrationId;
        VersionId = versionId;
        StageId = stageId;
        EditTaskId = editTaskId ?? string.Empty;
        NewTask.StageId = stageId;

        await LoadDataAsync(cancellationToken);
        if (CanEdit && Stage is not null && !string.IsNullOrWhiteSpace(EditTaskId))
        {
            var task = await _taskService.GetById(new GetTaskDefinitionByIdQuery(EditTaskId), cancellationToken);
            if (task is not null)
            {
                EditTaskPayloadJson = JsonSerializer.Serialize(BuildEditPayload(task));
                ShouldOpenEditModal = true;
            }
        }

        return Stage is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostCreateTaskAsync(string orchestrationId, string versionId, string taskId = "", CancellationToken cancellationToken = default)
    {
        ValidateTaskInput();
        if (!ModelState.IsValid)
        {
            OrchestrationId = orchestrationId;
            VersionId = versionId;
            StageId = NewTask.StageId;
            await LoadDataAsync(cancellationToken);
            return Page();
        }

        var effectiveTaskId = string.IsNullOrWhiteSpace(taskId) ? NewTask.TaskId : taskId;
        if (!string.IsNullOrWhiteSpace(effectiveTaskId))
        {
            var current = await _taskService.GetById(new GetTaskDefinitionByIdQuery(effectiveTaskId), cancellationToken);
            if (current is null)
            {
                return RedirectToPage("/OrchestrationStages/Details", new { area = "OrchestratorDesign", orchestrationId, versionId, stageId = NewTask.StageId });
            }

            var editKind = ResolveTaskKind(ParseEnum(NewTask.Kind, current.Kind));
            var editExecutionMode = ParseEnum(NewTask.ExecutionMode, current.ExecutionMode);
            var editDispatchType = ResolveDispatchType(editKind, ParseEnum(NewTask.DispatchType, current.DispatchType));
            var editOnErrorPolicy = ParseEnum(NewTask.OnErrorPolicy, current.OnErrorPolicy);
            var editParallelGroupId = NormalizeUlid(NewTask.ParallelGroupId);

            await _taskService.Update(
                new UpdateTaskDefinitionCommand(
                    current.Id,
                    string.IsNullOrWhiteSpace(NewTask.Key) ? current.Key : NewTask.Key,
                    string.IsNullOrWhiteSpace(NewTask.Name) ? current.Name : NewTask.Name,
                    current.Order,
                    NewTask.Notes ?? string.Empty,
                    editKind,
                    editExecutionMode,
                    editParallelGroupId,
                    NewTask.HasExecutionCondition
                        ? BuildExecutionCondition(NewTask.ConditionEngine, NewTask.ConditionDslExpression)
                        : null,
                    NewTask.HasTransformation
                        ? BuildTransformation(NewTask.TransformationEngine, NewTask.TransformationDsl)
                        : null,
                    BuildTaskConfiguration(editKind, NewTask, _defaultSchemaRegistryProviderKey),
                    NewTask.HasRetryPolicy
                        ? BuildRetryPolicy(
                            NewTask.RetryStrategyType,
                            NewTask.RetryMaxRetries,
                            NewTask.RetryDelaySeconds,
                            NewTask.RetryableErrorCodes,
                            NewTask.RetryStopOnNonRetryableError)
                        : null,
                    NewTask.HasTimeoutPolicy
                        ? BuildTimeoutPolicy(
                            NewTask.TimeoutSeconds,
                            NewTask.TimeoutBehavior,
                            NewTask.TimeoutFailErrorCode,
                            NewTask.TimeoutWaitSeconds,
                            NewTask.TimeoutAction,
                            NewTask.TimeoutReconcileRetryStrategyType,
                            NewTask.TimeoutReconcileRetries,
                            NewTask.TimeoutReconcileDelaySeconds,
                            NewTask.TimeoutReconcileRetryableErrorCodes,
                            NewTask.TimeoutReconcileStopOnNonRetryableError)
                        : null,
                    editOnErrorPolicy,
                    NewTask.HasCompensation ? BuildCompensationDefinition(NewTask, _defaultSchemaRegistryProviderKey) : null,
                    editDispatchType,
                    current.IsEnabled),
                cancellationToken);

            return RedirectToPage("/OrchestrationStages/Details", new { area = "OrchestratorDesign", orchestrationId, versionId, stageId = NewTask.StageId });
        }

        var kind = ResolveTaskKind(ParseEnum(NewTask.Kind, TaskKind.Messaging));
        var executionMode = ParseEnum(NewTask.ExecutionMode, TaskExecutionMode.Sequential);
        var dispatchType = ResolveDispatchType(kind, ParseEnum(NewTask.DispatchType, TaskDispatchType.FireAndWaitCallback));
        var onErrorPolicy = ParseEnum(NewTask.OnErrorPolicy, OnErrorPolicy.Stop);
        var parallelGroupId = NormalizeUlid(NewTask.ParallelGroupId);

        var currentTasks = (await _taskService.GetAll(new GetTaskDefinitionsQuery(NewTask.StageId), cancellationToken)).ToArray();

        await _taskService.Create(
            new CreateTaskDefinitionCommand(
                NewTask.StageId,
                NewTask.Key,
                NewTask.Name,
                currentTasks.Length,
                NewTask.Notes,
                kind,
                executionMode,
                parallelGroupId,
                NewTask.HasExecutionCondition
                    ? BuildExecutionCondition(NewTask.ConditionEngine, NewTask.ConditionDslExpression)
                    : null,
                NewTask.HasTransformation
                    ? BuildTransformation(NewTask.TransformationEngine, NewTask.TransformationDsl)
                    : null,
                BuildTaskConfiguration(kind, NewTask, _defaultSchemaRegistryProviderKey),
                NewTask.HasRetryPolicy
                    ? BuildRetryPolicy(
                        NewTask.RetryStrategyType,
                        NewTask.RetryMaxRetries,
                        NewTask.RetryDelaySeconds,
                        NewTask.RetryableErrorCodes,
                        NewTask.RetryStopOnNonRetryableError)
                    : null,
                NewTask.HasTimeoutPolicy
                    ? BuildTimeoutPolicy(
                        NewTask.TimeoutSeconds,
                        NewTask.TimeoutBehavior,
                        NewTask.TimeoutFailErrorCode,
                        NewTask.TimeoutWaitSeconds,
                        NewTask.TimeoutAction,
                        NewTask.TimeoutReconcileRetryStrategyType,
                        NewTask.TimeoutReconcileRetries,
                        NewTask.TimeoutReconcileDelaySeconds,
                        NewTask.TimeoutReconcileRetryableErrorCodes,
                        NewTask.TimeoutReconcileStopOnNonRetryableError)
                    : null,
                onErrorPolicy,
                NewTask.HasCompensation ? BuildCompensationDefinition(NewTask, _defaultSchemaRegistryProviderKey) : null,
                dispatchType,
                true),
            cancellationToken);

        return RedirectToPage("/OrchestrationStages/Details", new { area = "OrchestratorDesign", orchestrationId, versionId, stageId = NewTask.StageId });
    }

    public async Task<IActionResult> OnPostDeleteTaskAsync(string orchestrationId, string versionId, string stageId, string taskId, CancellationToken cancellationToken = default)
    {
        await _taskService.Delete(new DeleteTaskDefinitionCommand(taskId), cancellationToken);
        await NormalizeTaskOrderAsync(stageId, cancellationToken);
        return RedirectToPage("/OrchestrationStages/Details", new { area = "OrchestratorDesign", orchestrationId, versionId, stageId });
    }

    /// <summary>
    /// Sets enabled state of one task from board card toggle.
    /// </summary>
    public async Task<IActionResult> OnPostSetTaskEnabledAsync([FromBody] SetTaskEnabledRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TaskId))
        {
            return BadRequest();
        }

        var updated = request.IsEnabled
            ? await _taskService.Enable(new EnableTaskDefinitionCommand(request.TaskId), cancellationToken)
            : await _taskService.Disable(new DisableTaskDefinitionCommand(request.TaskId), cancellationToken);

        return new JsonResult(new { success = updated });
    }

    /// <summary>
    /// Loads execution condition payload for one task.
    /// </summary>
    public async Task<IActionResult> OnGetTaskExecutionConditionForEditAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return BadRequest();
        }

        var task = await _taskService.GetById(new GetTaskDefinitionByIdQuery(taskId), cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        return new JsonResult(BuildTaskExecutionConditionEditPayload(task));
    }

    /// <summary>
    /// Updates execution condition of one task.
    /// </summary>
    public async Task<IActionResult> OnPostSetTaskExecutionConditionAsync([FromBody] SetTaskExecutionConditionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TaskId))
        {
            return BadRequest();
        }

        var executionCondition = request.HasExecutionCondition
            ? BuildExecutionCondition(request.ConditionEngine, request.ConditionDslExpression)
            : null;

        var updated = await _taskService.SetExecutionCondition(
            new SetTaskExecutionConditionCommand(request.TaskId, executionCondition),
            cancellationToken);

        return new JsonResult(new { success = updated });
    }

    /// <summary>
    /// Loads transformation payload for one task.
    /// </summary>
    public async Task<IActionResult> OnGetTaskTransformationForEditAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return BadRequest();
        }

        var task = await _taskService.GetById(new GetTaskDefinitionByIdQuery(taskId), cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        return new JsonResult(BuildTaskTransformationEditPayload(task));
    }

    /// <summary>
    /// Loads schema context available to one task transformation.
    /// </summary>
    public async Task<IActionResult> OnGetTaskSchemaContextAsync(
        string versionId,
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionId) || string.IsNullOrWhiteSpace(taskId))
        {
            return BadRequest();
        }

        var context = await _schemaContextService.GetForTask(
            new GetTaskSchemaContextQuery(versionId, taskId),
            cancellationToken);

        return new JsonResult(BuildTaskSchemaContextPayload(context));
    }

    /// <summary>
    /// Updates transformation of one task.
    /// </summary>
    public async Task<IActionResult> OnPostSetTaskTransformationAsync([FromBody] SetTaskTransformationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TaskId))
        {
            return BadRequest();
        }

        var transformation = request.HasTransformation
            ? BuildTransformation(
                request.TransformationEngine,
                request.TransformationDsl,
                request.SourceContextHash,
                request.TargetSchemaHash)
            : null;

        var updated = await _taskService.SetTransformation(
            new SetTaskTransformationCommand(request.TaskId, transformation),
            cancellationToken);

        return new JsonResult(new { success = updated });
    }

    /// <summary>
    /// Reorders tasks using drag and drop sequence.
    /// </summary>
    public async Task<IActionResult> OnPostReorderTasksAsync([FromBody] ReorderTasksRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.StageId) || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest();
        }

        var tasks = (await _taskService.GetAll(new GetTaskDefinitionsQuery(request.StageId), cancellationToken))
            .ToDictionary(x => x.Id, StringComparer.Ordinal);

        var ordered = request.Items
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.TaskId) && tasks.ContainsKey(x.TaskId))
            .GroupBy(x => x.TaskId, StringComparer.Ordinal)
            .Select(x => x.First())
            .ToArray();

        if (ordered.Length == 0)
        {
            return BadRequest();
        }

        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            var task = tasks[item.TaskId];
            var parallelGroupId = NormalizeUlid(item.ParallelGroupId);
            var executionMode = string.IsNullOrWhiteSpace(parallelGroupId)
                ? TaskExecutionMode.Sequential
                : TaskExecutionMode.Parallel;

            await _taskService.Update(
                new UpdateTaskDefinitionCommand(
                    task.Id,
                    task.Key,
                    task.Name,
                    index,
                    task.Notes,
                    task.Kind,
                    executionMode,
                    parallelGroupId,
                    task.ExecutionCondition,
                    task.Transformation,
                    task.Configuration,
                    task.RetryPolicy,
                    task.TimeoutPolicy,
                    task.OnErrorPolicy,
                    task.CompensationDefinition,
                    task.DispatchType,
                    task.IsEnabled),
                cancellationToken);
        }

        return new JsonResult(new { success = true });
    }

    /// <summary>
    /// Creates a parallel group with default policy values.
    /// </summary>
    public async Task<IActionResult> OnPostCreateParallelGroupAsync([FromBody] CreateParallelGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.StageId))
        {
            return BadRequest();
        }

        var existingGroups = (await _parallelGroupService.GetAll(new GetParallelGroupDefinitionsQuery(request.StageId), cancellationToken)).ToArray();
        var defaultName = GenerateDefaultGroupName(existingGroups);
        var normalizedName = NormalizeGroupName(request.Name, defaultName);

        var createdId = await _parallelGroupService.Create(
            new CreateParallelGroupDefinitionCommand(
                string.Empty,
                request.StageId,
                normalizedName,
                ParallelJoinPolicy.WaitAll,
                null),
            cancellationToken);

        var created = await _parallelGroupService.GetById(new GetParallelGroupDefinitionByIdQuery(createdId), cancellationToken);
        if (created is null)
        {
            return BadRequest();
        }

        return new JsonResult(new
        {
            success = true,
            group = new
            {
                id = created.Id,
                name = created.Name,
                joinPolicy = created.JoinPolicy.ToString(),
                maxParallelAgents = created.MaxParallelAgents
            }
        });
    }

    /// <summary>
    /// Loads one parallel group payload for edit modal.
    /// </summary>
    public async Task<IActionResult> OnGetParallelGroupForEditAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return BadRequest();
        }

        var group = await _parallelGroupService.GetById(new GetParallelGroupDefinitionByIdQuery(groupId), cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            id = group.Id,
            name = group.Name,
            joinPolicy = group.JoinPolicy.ToString(),
            maxParallelAgents = group.MaxParallelAgents
        });
    }

    /// <summary>
    /// Updates one parallel group.
    /// </summary>
    public async Task<IActionResult> OnPostUpdateParallelGroupAsync([FromBody] UpdateParallelGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.GroupId))
        {
            return BadRequest();
        }

        var existing = await _parallelGroupService.GetById(new GetParallelGroupDefinitionByIdQuery(request.GroupId), cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var updated = await _parallelGroupService.Update(
            new UpdateParallelGroupDefinitionCommand(
                existing.Id,
                NormalizeGroupName(request.Name, existing.Name),
                ParseEnum(request.JoinPolicy, existing.JoinPolicy),
                NormalizeMaxParallelAgents(request.MaxParallelAgents)),
            cancellationToken);

        return new JsonResult(new { success = updated });
    }

    /// <summary>
    /// Deletes one parallel group and unassigns tasks from it.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteParallelGroupAsync([FromBody] DeleteParallelGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.StageId) || string.IsNullOrWhiteSpace(request.GroupId))
        {
            return BadRequest();
        }

        var groupId = NormalizeUlid(request.GroupId);
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return BadRequest();
        }

        var tasksInGroup = (await _taskService.GetAll(new GetTaskDefinitionsQuery(request.StageId), cancellationToken))
            .Where(x => string.Equals(x.ParallelGroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        foreach (var task in tasksInGroup)
        {
            await _taskService.Update(
                new UpdateTaskDefinitionCommand(
                    task.Id,
                    task.Key,
                    task.Name,
                    task.Order,
                    task.Notes,
                    task.Kind,
                    TaskExecutionMode.Sequential,
                    string.Empty,
                    task.ExecutionCondition,
                    task.Transformation,
                    task.Configuration,
                    task.RetryPolicy,
                    task.TimeoutPolicy,
                    task.OnErrorPolicy,
                    task.CompensationDefinition,
                    task.DispatchType,
                    task.IsEnabled),
                cancellationToken);
        }

        var deleted = await _parallelGroupService.Delete(new DeleteParallelGroupDefinitionCommand(groupId), cancellationToken);
        return new JsonResult(new { success = deleted });
    }

    public async Task<IActionResult> OnPostDeleteStageAsync(string orchestrationId, string versionId, string stageId, CancellationToken cancellationToken = default)
    {
        await _stageService.Delete(new DeleteStageDefinitionCommand(stageId), cancellationToken);
        return RedirectToPage("/OrchestrationVersions/Details", new { area = "OrchestratorDesign", orchestrationId, versionId });
    }

    /// <summary>
    /// Retrieves a task payload to prefill the edit modal.
    /// </summary>
    public async Task<IActionResult> OnGetTaskForEditAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return BadRequest();
        }

        var task = await _taskService.GetById(new GetTaskDefinitionByIdQuery(taskId), cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        return new JsonResult(BuildEditPayload(task));
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            Stage = await _stageService.GetById(new GetStageDefinitionByIdQuery(StageId), cancellationToken);
            if (Stage is null)
            {
                return;
            }

            SelectedVersion = await _versionService.GetById(new GetOrchestrationVersionByIdQuery(VersionId), cancellationToken);
            Tasks = (await _taskService.GetAll(new GetTaskDefinitionsQuery(StageId), cancellationToken))
                .OrderBy(x => x.Order)
                .ToArray();
            ParallelGroups = (await _parallelGroupService.GetAll(new GetParallelGroupDefinitionsQuery(StageId), cancellationToken)).ToArray();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ValidateTaskInput()
    {
        var kind = ResolveTaskKind(ParseEnum(NewTask.Kind, TaskKind.Messaging));
        ValidateTaskConfiguration(kind, string.Empty);

        if (NewTask.HasRetryPolicy)
        {
            ValidateRetryPolicy(nameof(NewTask.RetryMaxRetries), NewTask.RetryMaxRetries, nameof(NewTask.RetryDelaySeconds), NewTask.RetryDelaySeconds);
        }

        if (NewTask.HasTimeoutPolicy)
        {
            ValidateTimeoutPolicy(
                nameof(NewTask.TimeoutSeconds),
                NewTask.TimeoutSeconds,
                nameof(NewTask.TimeoutWaitSeconds),
                NewTask.TimeoutWaitSeconds,
                nameof(NewTask.TimeoutReconcileRetries),
                NewTask.TimeoutReconcileRetries,
                nameof(NewTask.TimeoutReconcileDelaySeconds),
                NewTask.TimeoutReconcileDelaySeconds);
        }

        if (!NewTask.HasCompensation)
        {
            return;
        }

        var compensationKind = ResolveTaskKind(ParseEnum(NewTask.CompensationKind, TaskKind.Messaging));
        ValidateTaskConfiguration(compensationKind, "Compensation");

        if (NewTask.HasCompensationRetryPolicy)
        {
            ValidateRetryPolicy(
                nameof(NewTask.CompensationRetryMaxRetries),
                NewTask.CompensationRetryMaxRetries,
                nameof(NewTask.CompensationRetryDelaySeconds),
                NewTask.CompensationRetryDelaySeconds);
        }

        if (NewTask.HasCompensationTimeoutPolicy)
        {
            ValidateTimeoutPolicy(
                nameof(NewTask.CompensationTimeoutSeconds),
                NewTask.CompensationTimeoutSeconds,
                nameof(NewTask.CompensationTimeoutWaitSeconds),
                NewTask.CompensationTimeoutWaitSeconds,
                nameof(NewTask.CompensationTimeoutReconcileRetries),
                NewTask.CompensationTimeoutReconcileRetries,
                nameof(NewTask.CompensationTimeoutReconcileDelaySeconds),
                NewTask.CompensationTimeoutReconcileDelaySeconds);
        }
    }

    private void ValidateTaskConfiguration(TaskKind kind, string prefix)
    {
        switch (kind)
        {
            case TaskKind.Http:
                ValidateHttpConfiguration(prefix);
                break;
            case TaskKind.Messaging:
                ValidateMessagingConfiguration(prefix);
                break;
            case TaskKind.Plugin:
                ValidatePluginConfiguration(prefix);
                break;
        }
    }

    private void ValidateHttpConfiguration(string prefix)
    {
        var isCompensation = string.Equals(prefix, "Compensation", StringComparison.Ordinal);
        var baseUrlVariable = isCompensation ? NewTask.CompensationHttpBaseUrlVariableRef : NewTask.HttpBaseUrlVariableRef;
        var relativePath = isCompensation ? NewTask.CompensationHttpRelativePath : NewTask.HttpRelativePath;
        var statusCodes = isCompensation ? NewTask.CompensationHttpExpectedStatusCodes : NewTask.HttpExpectedStatusCodes;
        var hasSchemaValidation = isCompensation ? NewTask.HasCompensationHttpSchemaValidation : NewTask.HasHttpSchemaValidation;
        var contractKey = isCompensation ? NewTask.CompensationHttpSchemaContractKey : NewTask.HttpSchemaContractKey;
        var registryProviderId = isCompensation ? NewTask.CompensationHttpSchemaRegistryProviderId : NewTask.HttpSchemaRegistryProviderId;

        AddRequired($"{prefix}HttpBaseUrlVariableRef", baseUrlVariable, "Capture the base URL variable.");
        AddRequired($"{prefix}HttpRelativePath", relativePath, "Capture the relative path.");

        if (!ParseIntList(statusCodes, Array.Empty<int>()).Any())
        {
            ModelState.AddModelError($"{prefix}HttpExpectedStatusCodes", "Capture at least one valid HTTP status code.");
        }

        if (hasSchemaValidation)
        {
            AddRequired($"{prefix}HttpSchemaContractKey", contractKey, "Capture the HTTP schema contract key.");
        }

        ValidateOptionalUlid($"{prefix}HttpSchemaRegistryProviderId", registryProviderId);
    }

    private void ValidateMessagingConfiguration(string prefix)
    {
        var isCompensation = string.Equals(prefix, "Compensation", StringComparison.Ordinal);
        var topic = isCompensation ? NewTask.CompensationMessagingTopic : NewTask.MessagingTopic;
        var hasSchemaValidation = isCompensation ? NewTask.HasCompensationMessagingSchemaValidation : NewTask.HasMessagingSchemaValidation;
        var contractKey = isCompensation ? NewTask.CompensationMessagingSchemaContractKey : NewTask.MessagingSchemaContractKey;
        var registryProviderId = isCompensation ? NewTask.CompensationMessagingSchemaRegistryProviderId : NewTask.MessagingSchemaRegistryProviderId;
        var hasRequestValidation = isCompensation ? NewTask.HasCompensationMessagingRequestValidation : NewTask.HasMessagingRequestValidation;
        var requestValidationDsl = isCompensation ? NewTask.CompensationMessagingRequestValidationDsl : NewTask.MessagingRequestValidationDsl;
        var hasResponseValidation = isCompensation ? NewTask.HasCompensationMessagingResponseValidation : NewTask.HasMessagingResponseValidation;
        var responseValidationDsl = isCompensation ? NewTask.CompensationMessagingResponseValidationDsl : NewTask.MessagingResponseValidationDsl;

        AddRequired($"{prefix}MessagingTopic", topic, "Capture the messaging topic.");
        if (hasSchemaValidation)
        {
            AddRequired($"{prefix}MessagingSchemaContractKey", contractKey, "Capture the messaging schema contract key.");
        }

        if (hasRequestValidation || hasSchemaValidation)
        {
            AddRequired($"{prefix}MessagingRequestValidationDsl", requestValidationDsl, "Capture the request validation DSL.");
        }

        if (hasResponseValidation)
        {
            AddRequired($"{prefix}MessagingResponseValidationDsl", responseValidationDsl, "Capture the response validation DSL.");
        }

        ValidateOptionalUlid($"{prefix}MessagingSchemaRegistryProviderId", registryProviderId);
    }

    private void ValidatePluginConfiguration(string prefix)
    {
        var pluginId = string.Equals(prefix, "Compensation", StringComparison.Ordinal)
            ? NewTask.CompensationPluginId
            : NewTask.PluginId;

        AddRequired($"{prefix}PluginId", pluginId, "Capture the plugin id.");
        ValidateOptionalUlid($"{prefix}PluginId", pluginId);
    }

    private void ValidateRetryPolicy(string retriesField, int retries, string delayField, double delaySeconds)
    {
        if (retries < 0)
        {
            ModelState.AddModelError(retriesField, "Retries cannot be negative.");
        }

        if (delaySeconds < 0)
        {
            ModelState.AddModelError(delayField, "Delay cannot be negative.");
        }
    }

    private void ValidateTimeoutPolicy(
        string timeoutField,
        double timeoutSeconds,
        string waitField,
        double waitSeconds,
        string reconcileRetriesField,
        int reconcileRetries,
        string reconcileDelayField,
        double reconcileDelaySeconds)
    {
        if (timeoutSeconds <= 0)
        {
            ModelState.AddModelError(timeoutField, "Timeout must be greater than zero.");
        }

        if (waitSeconds <= 0)
        {
            ModelState.AddModelError(waitField, "Wait time must be greater than zero.");
        }

        if (reconcileRetries < 0)
        {
            ModelState.AddModelError(reconcileRetriesField, "Reconcile retries cannot be negative.");
        }

        if (reconcileDelaySeconds < 0)
        {
            ModelState.AddModelError(reconcileDelayField, "Reconcile delay cannot be negative.");
        }
    }

    private void AddRequired(string field, string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ModelState.AddModelError(field, message);
        }
    }

    private void ValidateOptionalUlid(string field, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !Ulid.TryParse(value, out _))
        {
            ModelState.AddModelError(field, "Capture a valid ULID.");
        }
    }

    private static object BuildEditPayload(TaskDefinitionModel task)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["TaskId"] = task.Id,
            ["StageId"] = task.StageDefinitionId,
            ["Key"] = task.Key,
            ["Name"] = task.Name,
            ["Kind"] = task.Kind.ToString(),
            ["ExecutionMode"] = task.ExecutionMode.ToString(),
            ["DispatchType"] = task.DispatchType.ToString(),
            ["OnErrorPolicy"] = task.OnErrorPolicy.ToString(),
            ["ParallelGroupId"] = task.ParallelGroupId ?? string.Empty,
            ["IsEnabled"] = task.IsEnabled,
            ["Notes"] = task.Notes ?? string.Empty,
            ["HasExecutionCondition"] = task.HasExecutionCondition,
            ["ConditionEngine"] = task.ExecutionCondition?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["ConditionDslExpression"] = (task.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? "true",
            ["HasTransformation"] = task.HasTransformation,
            ["TransformationEngine"] = task.Transformation?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["TransformationDsl"] = (task.Transformation?.Configuration as DslTransformationConfiguration)?.Dsl ?? string.Empty,
            ["HasRetryPolicy"] = task.RetryPolicy is not null,
            ["HasTimeoutPolicy"] = task.TimeoutPolicy is not null,
            ["HasCompensation"] = task.CompensationDefinition is not null,
            ["CompensationKind"] = task.CompensationDefinition?.CompensationTaskKind.ToString() ?? TaskKind.Messaging.ToString(),
            ["CompensationDispatchType"] = task.CompensationDefinition?.DispatchType.ToString() ?? TaskDispatchType.FireAndForget.ToString(),
            ["HasCompensationExecutionCondition"] = task.CompensationDefinition?.HasExecutionCondition ?? false,
            ["CompensationConditionEngine"] = task.CompensationDefinition?.ExecutionCondition?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["CompensationConditionDslExpression"] = (task.CompensationDefinition?.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? "true",
            ["HasCompensationTransformation"] = task.CompensationDefinition?.HasTransformation ?? false,
            ["CompensationTransformationEngine"] = task.CompensationDefinition?.Transformation?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["CompensationTransformationDsl"] = (task.CompensationDefinition?.Transformation?.Configuration as DslTransformationConfiguration)?.Dsl ?? string.Empty,
            ["HasCompensationRetryPolicy"] = task.CompensationDefinition?.RetryPolicy is not null,
            ["HasCompensationTimeoutPolicy"] = task.CompensationDefinition?.TimeoutPolicy is not null,
        };

        if (task.Configuration is HttpTaskConfiguration http)
        {
            payload["HttpBaseUrlVariableRef"] = http.BaseUrlVariableRef ?? string.Empty;
            payload["HttpRelativePath"] = http.RelativePath ?? string.Empty;
            payload["HttpMethod"] = http.Method ?? string.Empty;
            payload["HttpExpectedStatusCodes"] = string.Join(',', http.ExpectedStatusCodes ?? new List<int> { 200 });
            payload["HttpAllowSyncResponse"] = http.AllowSyncResponse;
            payload["HasHttpSchemaValidation"] = http.HasSchemaValidation;
            payload["HttpSchemaContractKey"] = http.SchemaBinding?.ContractKey ?? string.Empty;
            payload["HttpSchemaContractVersion"] = http.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
            payload["HttpSchemaRegistryProviderId"] = http.SchemaBinding is null ? string.Empty : http.SchemaBinding.RegistryProviderId.ToString();
            payload["HttpSchemaStrictMode"] = http.SchemaBinding?.StrictMode ?? false;
        }
        else if (task.Configuration is MessagingTaskConfiguration messaging)
        {
            payload["MessagingTopic"] = messaging.Topic ?? string.Empty;
            payload["MessagingVersion"] = messaging.Version.ToString();
            payload["HasMessagingSchemaValidation"] = messaging.HasSchemaValidation;
            payload["MessagingSchemaContractKey"] = messaging.SchemaBinding?.ContractKey ?? string.Empty;
            payload["MessagingSchemaContractVersion"] = messaging.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
            payload["MessagingSchemaRegistryProviderId"] = messaging.SchemaBinding is null ? string.Empty : messaging.SchemaBinding.RegistryProviderId.ToString();
            payload["MessagingSchemaStrictMode"] = messaging.SchemaBinding?.StrictMode ?? false;
            payload["HasMessagingRequestValidation"] = messaging.HasRequestValidation;
            payload["MessagingRequestValidationDsl"] = (messaging.RequestValidation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty;
            payload["MessagingRequestValidationErrorCode"] = messaging.RequestValidation?.ErrorCode ?? "RequestValidationFailed";
            payload["HasMessagingResponseValidation"] = messaging.HasResponseValidation;
            payload["MessagingResponseValidationDsl"] = (messaging.ResponseValidation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty;
            payload["MessagingResponseValidationErrorCode"] = messaging.ResponseValidation?.ErrorCode ?? "ResponseValidationFailed";
        }
        else if (task.Configuration is PluginTaskConfiguration plugin)
        {
            payload["PluginId"] = plugin.PluginId.ToString();
        }

        if (task.RetryPolicy is not null)
        {
            payload["RetryStrategyType"] = task.RetryPolicy.StrategyType.ToString();
            payload["RetryMaxRetries"] = task.RetryPolicy.MaxRetries;
            payload["RetryableErrorCodes"] = string.Join(',', task.RetryPolicy.RetryableErrorCodes ?? new List<string>());
            payload["RetryStopOnNonRetryableError"] = task.RetryPolicy.StopOnNonRetryableError;
            payload["RetryDelaySeconds"] = (task.RetryPolicy.Strategy as FixedRetryStrategy)?.Delay.Value.TotalSeconds ?? 1d;
        }

        if (task.TimeoutPolicy is not null)
        {
            payload["TimeoutSeconds"] = task.TimeoutPolicy.Timeout.Value.TotalSeconds;
            payload["TimeoutBehavior"] = task.TimeoutPolicy.TimeoutBehavior.ToString();

            if (task.TimeoutPolicy.TimeoutBehaviorPolicy is FailTimeoutBehaviorPolicy fail)
            {
                payload["TimeoutFailErrorCode"] = fail.ErrorCode ?? "TIMEOUT";
            }
            else if (task.TimeoutPolicy.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicy wait)
            {
                payload["TimeoutAction"] = wait.OrchestrationAction.ToString();
                payload["TimeoutWaitSeconds"] = wait.WaitingTime.Value.TotalSeconds;
            }
            else if (task.TimeoutPolicy.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicy reconcile)
            {
                payload["TimeoutAction"] = reconcile.OrchestrationAction.ToString();
                payload["TimeoutReconcileRetryStrategyType"] = reconcile.RetryPolicy?.StrategyType.ToString() ?? RetryStrategyType.Fixed.ToString();
                payload["TimeoutReconcileRetries"] = reconcile.RetryPolicy?.MaxRetries ?? 0;
                payload["TimeoutReconcileDelaySeconds"] = (reconcile.RetryPolicy?.Strategy as FixedRetryStrategy)?.Delay.Value.TotalSeconds ?? 5d;
                payload["TimeoutReconcileRetryableErrorCodes"] = string.Join(',', reconcile.RetryPolicy?.RetryableErrorCodes ?? new List<string>());
                payload["TimeoutReconcileStopOnNonRetryableError"] = reconcile.RetryPolicy?.StopOnNonRetryableError ?? false;
            }
        }

        if (task.CompensationDefinition is not null)
        {
            if (task.CompensationDefinition.Configuration is HttpTaskConfiguration compHttp)
            {
                payload["CompensationHttpBaseUrlVariableRef"] = compHttp.BaseUrlVariableRef ?? string.Empty;
                payload["CompensationHttpRelativePath"] = compHttp.RelativePath ?? string.Empty;
                payload["CompensationHttpMethod"] = compHttp.Method ?? string.Empty;
                payload["CompensationHttpExpectedStatusCodes"] = string.Join(',', compHttp.ExpectedStatusCodes ?? new List<int> { 200 });
                payload["CompensationHttpAllowSyncResponse"] = compHttp.AllowSyncResponse;
                payload["HasCompensationHttpSchemaValidation"] = compHttp.HasSchemaValidation;
                payload["CompensationHttpSchemaContractKey"] = compHttp.SchemaBinding?.ContractKey ?? string.Empty;
                payload["CompensationHttpSchemaContractVersion"] = compHttp.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
                payload["CompensationHttpSchemaRegistryProviderId"] = compHttp.SchemaBinding is null ? string.Empty : compHttp.SchemaBinding.RegistryProviderId.ToString();
                payload["CompensationHttpSchemaStrictMode"] = compHttp.SchemaBinding?.StrictMode ?? false;
            }
            else if (task.CompensationDefinition.Configuration is MessagingTaskConfiguration compMsg)
            {
                payload["CompensationMessagingTopic"] = compMsg.Topic ?? string.Empty;
                payload["CompensationMessagingVersion"] = compMsg.Version.ToString();
                payload["HasCompensationMessagingSchemaValidation"] = compMsg.HasSchemaValidation;
                payload["CompensationMessagingSchemaContractKey"] = compMsg.SchemaBinding?.ContractKey ?? string.Empty;
                payload["CompensationMessagingSchemaContractVersion"] = compMsg.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
                payload["CompensationMessagingSchemaRegistryProviderId"] = compMsg.SchemaBinding is null ? string.Empty : compMsg.SchemaBinding.RegistryProviderId.ToString();
                payload["CompensationMessagingSchemaStrictMode"] = compMsg.SchemaBinding?.StrictMode ?? false;
                payload["HasCompensationMessagingRequestValidation"] = compMsg.HasRequestValidation;
                payload["CompensationMessagingRequestValidationDsl"] = (compMsg.RequestValidation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty;
                payload["CompensationMessagingRequestValidationErrorCode"] = compMsg.RequestValidation?.ErrorCode ?? "RequestValidationFailed";
                payload["HasCompensationMessagingResponseValidation"] = compMsg.HasResponseValidation;
                payload["CompensationMessagingResponseValidationDsl"] = (compMsg.ResponseValidation?.Configuration as DslValidationConfiguration)?.Dsl ?? string.Empty;
                payload["CompensationMessagingResponseValidationErrorCode"] = compMsg.ResponseValidation?.ErrorCode ?? "ResponseValidationFailed";
            }
            else if (task.CompensationDefinition.Configuration is PluginTaskConfiguration compPlugin)
            {
                payload["CompensationPluginId"] = compPlugin.PluginId.ToString();
            }

            if (task.CompensationDefinition.RetryPolicy is not null)
            {
                payload["CompensationRetryStrategyType"] = task.CompensationDefinition.RetryPolicy.StrategyType.ToString();
                payload["CompensationRetryMaxRetries"] = task.CompensationDefinition.RetryPolicy.MaxRetries;
                payload["CompensationRetryableErrorCodes"] = string.Join(',', task.CompensationDefinition.RetryPolicy.RetryableErrorCodes ?? new List<string>());
                payload["CompensationRetryStopOnNonRetryableError"] = task.CompensationDefinition.RetryPolicy.StopOnNonRetryableError;
                payload["CompensationRetryDelaySeconds"] = (task.CompensationDefinition.RetryPolicy.Strategy as FixedRetryStrategy)?.Delay.Value.TotalSeconds ?? 1d;
            }

            if (task.CompensationDefinition.TimeoutPolicy is not null)
            {
                payload["CompensationTimeoutSeconds"] = task.CompensationDefinition.TimeoutPolicy.Timeout.Value.TotalSeconds;
                payload["CompensationTimeoutBehavior"] = task.CompensationDefinition.TimeoutPolicy.TimeoutBehavior.ToString();

                if (task.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy is FailTimeoutBehaviorPolicy compFail)
                {
                    payload["CompensationTimeoutFailErrorCode"] = compFail.ErrorCode ?? "TIMEOUT";
                }
                else if (task.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy is WaitTimeoutBehaviorPolicy compWait)
                {
                    payload["CompensationTimeoutAction"] = compWait.OrchestrationAction.ToString();
                    payload["CompensationTimeoutWaitSeconds"] = compWait.WaitingTime.Value.TotalSeconds;
                }
                else if (task.CompensationDefinition.TimeoutPolicy.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicy compReconcile)
                {
                    payload["CompensationTimeoutAction"] = compReconcile.OrchestrationAction.ToString();
                    payload["CompensationTimeoutReconcileRetryStrategyType"] = compReconcile.RetryPolicy?.StrategyType.ToString() ?? RetryStrategyType.Fixed.ToString();
                    payload["CompensationTimeoutReconcileRetries"] = compReconcile.RetryPolicy?.MaxRetries ?? 0;
                    payload["CompensationTimeoutReconcileDelaySeconds"] = (compReconcile.RetryPolicy?.Strategy as FixedRetryStrategy)?.Delay.Value.TotalSeconds ?? 5d;
                    payload["CompensationTimeoutReconcileRetryableErrorCodes"] = string.Join(',', compReconcile.RetryPolicy?.RetryableErrorCodes ?? new List<string>());
                    payload["CompensationTimeoutReconcileStopOnNonRetryableError"] = compReconcile.RetryPolicy?.StopOnNonRetryableError ?? false;
                }
            }
        }

        return payload;
    }

    private static object BuildTaskExecutionConditionEditPayload(TaskDefinitionModel task)
    {
        var dsl = task.ExecutionCondition?.Configuration as DslConditionConfiguration;
        var expressionText = dsl?.Expression.ToString() ?? "true";
        var hasExecutionCondition = task.HasExecutionCondition;

        return new
        {
            taskId = task.Id,
            hasExecutionCondition,
            conditionEngine = task.ExecutionCondition?.Engine.ToString() ?? EngineType.DSL.ToString(),
            conditionDslExpression = expressionText,
        };
    }

    private static object BuildTaskTransformationEditPayload(TaskDefinitionModel task)
    {
        return new
        {
            taskId = task.Id,
            hasTransformation = task.HasTransformation,
            transformationEngine = task.Transformation?.Engine.ToString() ?? EngineType.DSL.ToString(),
            transformationDsl = (task.Transformation?.Configuration as DslTransformationConfiguration)?.Dsl ?? string.Empty,
            sourceContextHash = (task.Transformation?.Configuration as DslTransformationConfiguration)?.SourceContextHash ?? string.Empty,
            targetSchemaHash = (task.Transformation?.Configuration as DslTransformationConfiguration)?.TargetSchemaHash ?? string.Empty,
        };
    }

    private static object BuildTaskSchemaContextPayload(OrchestrationSchemaContext context)
    {
        return new
        {
            orchestrationVersionId = context.OrchestrationVersionId,
            orchestrationVersion = context.OrchestrationVersion,
            stageKey = context.StageKey,
            taskKey = context.TaskKey,
            signature = context.Signature,
            sources = context.Sources.Select(BuildTaskSchemaSourcePayload).ToArray(),
            target = BuildTaskSchemaTargetPayload(context.Target)
        };
    }

    private static object BuildTaskSchemaSourcePayload(OrchestrationSchemaSource source)
    {
        return new
        {
            alias = source.Alias,
            sourceKind = source.SourceKind.ToString(),
            stageKey = source.StageKey ?? string.Empty,
            taskKey = source.TaskKey ?? string.Empty,
            schema = BuildSchemaBindingPayload(source.SchemaBinding)
        };
    }

    private static object BuildTaskSchemaTargetPayload(OrchestrationSchemaTarget target)
    {
        return new
        {
            alias = target?.Alias ?? string.Empty,
            stageKey = target?.StageKey ?? string.Empty,
            taskKey = target?.TaskKey ?? string.Empty,
            schema = BuildSchemaBindingPayload(target?.SchemaBinding)
        };
    }

    private static object BuildSchemaBindingPayload(SchemaBinding binding)
    {
        if (binding is null)
        {
            return null;
        }

        return new
        {
            contractKey = binding.ContractKey,
            contractVersion = binding.ContractVersion.ToString(),
            contractKind = binding.ContractKind.ToString(),
            registryProviderKey = binding.RegistryProviderKey,
            strictMode = binding.StrictMode,
            isValidationEnabled = binding.IsValidationEnabled,
            snapshotHash = binding.Snapshot?.ContentHash ?? string.Empty,
            schemaFormat = binding.Snapshot?.SchemaFormat ?? string.Empty,
            schemaJson = binding.Snapshot?.SchemaJson ?? string.Empty,
            sourceArtifactId = binding.Snapshot?.SourceArtifactId ?? string.Empty,
            resolvedBy = binding.Snapshot?.ResolvedBy ?? string.Empty,
            resolvedAtUtc = binding.Snapshot?.ResolvedAtUtc
        };
    }

    private async Task NormalizeTaskOrderAsync(string stageId, CancellationToken cancellationToken)
    {
        var tasks = (await _taskService.GetAll(new GetTaskDefinitionsQuery(stageId), cancellationToken))
            .OrderBy(x => x.Order)
            .ToArray();

        for (var index = 0; index < tasks.Length; index++)
        {
            var task = tasks[index];
            if (task.Order == index)
            {
                continue;
            }

            await _taskService.Update(
                new UpdateTaskDefinitionCommand(
                    task.Id,
                    task.Key,
                    task.Name,
                    index,
                    task.Notes,
                    task.Kind,
                    task.ExecutionMode,
                    task.ParallelGroupId,
                    task.ExecutionCondition,
                    task.Transformation,
                    task.Configuration,
                    task.RetryPolicy,
                    task.TimeoutPolicy,
                    task.OnErrorPolicy,
                    task.CompensationDefinition,
                    task.DispatchType,
                    task.IsEnabled),
                cancellationToken);
        }
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed)
            ? parsed
            : fallback;
    }

    private static string NormalizeUlid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Ulid.TryParse(value, out var parsed)
            ? parsed.ToString()
            : string.Empty;
    }

    private static string NormalizeGroupName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "Parallel group" : fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 128
            ? trimmed[..128]
            : trimmed;
    }

    private static string GenerateDefaultGroupName(IEnumerable<ParallelGroupDefinitionModel> groups)
    {
        var maxGroupNumber = 0;
        foreach (var group in groups)
        {
            var name = group?.Name ?? string.Empty;
            if (!name.StartsWith("Group", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = name[5..].Trim();
            if (!int.TryParse(suffix, out var parsed))
            {
                continue;
            }

            if (parsed > maxGroupNumber)
            {
                maxGroupNumber = parsed;
            }
        }

        return $"Group{Math.Max(1, maxGroupNumber + 1)}";
    }

    private static int? NormalizeMaxParallelAgents(int? maxParallelAgents)
    {
        return maxParallelAgents is > 0 ? maxParallelAgents : null;
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

    private static TransformationDefinition BuildTransformation(
        string engineText,
        string dsl,
        string sourceContextHash = "",
        string targetSchemaHash = "",
        string semanticDiagnosticsJson = "{}")
    {
        var engine = ParseEnum(engineText, EngineType.DSL);
        if (engine == EngineType.DSL)
        {
            return new TransformationDefinition
            {
                Engine = engine,
                Configuration = new DslTransformationConfiguration
                {
                    Dsl = dsl ?? string.Empty,
                    SourceContextHash = sourceContextHash ?? string.Empty,
                    TargetSchemaHash = targetSchemaHash ?? string.Empty,
                    SemanticDiagnosticsJson = string.IsNullOrWhiteSpace(semanticDiagnosticsJson)
                        ? "{}"
                        : semanticDiagnosticsJson
                }
            };
        }

        return new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration
            {
                Dsl = dsl ?? string.Empty,
                SourceContextHash = sourceContextHash ?? string.Empty,
                TargetSchemaHash = targetSchemaHash ?? string.Empty,
                SemanticDiagnosticsJson = string.IsNullOrWhiteSpace(semanticDiagnosticsJson)
                    ? "{}"
                    : semanticDiagnosticsJson
            }
        };
    }

    private static ValidationDefinition BuildValidation(string dsl, string errorCode)
        => new()
        {
            Engine = EngineType.DSL,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "PayloadValidationFailed" : errorCode.Trim(),
            Configuration = new DslValidationConfiguration
            {
                Dsl = dsl ?? string.Empty
            }
        };

    private static ITaskConfiguration BuildTaskConfiguration(
        TaskKind kind,
        CreateTaskInput input,
        string defaultSchemaRegistryProviderKey)
    {
        return kind switch
        {
            TaskKind.Http => new HttpTaskConfiguration
            {
                HasSchemaValidation = input.HasHttpSchemaValidation,
                BaseUrlVariableRef = input.HttpBaseUrlVariableRef.Trim(),
                RelativePath = input.HttpRelativePath.Trim(),
                Method = input.HttpMethod.Trim().ToUpperInvariant(),
                ExpectedStatusCodes = ParseIntList(input.HttpExpectedStatusCodes, new[] { 200 }),
                AllowSyncResponse = input.HttpAllowSyncResponse,
                SchemaBinding = input.HasHttpSchemaValidation ? CreateSchemaBinding(
                    ElementType.Task,
                    input.HttpSchemaContractKey,
                    input.HttpSchemaContractVersion,
                    input.HttpSchemaRegistryProviderId,
                    SchemaContractKind.CommandRequest,
                    defaultSchemaRegistryProviderKey,
                    input.HttpSchemaStrictMode,
                    input.HasHttpSchemaValidation) : null
            },
            TaskKind.Messaging => new MessagingTaskConfiguration
            {
                HasSchemaValidation = input.HasMessagingSchemaValidation,
                HasRequestValidation = input.HasMessagingRequestValidation,
                RequestValidation = input.HasMessagingRequestValidation || input.HasMessagingSchemaValidation
                    ? BuildValidation(input.MessagingRequestValidationDsl, input.MessagingRequestValidationErrorCode)
                    : null,
                HasResponseValidation = input.HasMessagingResponseValidation,
                ResponseValidation = input.HasMessagingResponseValidation
                    ? BuildValidation(input.MessagingResponseValidationDsl, input.MessagingResponseValidationErrorCode)
                    : null,
                Topic = input.MessagingTopic.Trim(),
                Version = ParseSemanticVersion(input.MessagingVersion, new SemanticVersion(1, 0, 0)),
                SchemaBinding = input.HasMessagingSchemaValidation ? CreateSchemaBinding(
                    ElementType.Task,
                    input.MessagingSchemaContractKey,
                    input.MessagingSchemaContractVersion,
                    input.MessagingSchemaRegistryProviderId,
                    SchemaContractKind.CommandRequest,
                    defaultSchemaRegistryProviderKey,
                    input.MessagingSchemaStrictMode,
                    input.HasMessagingSchemaValidation) : null
            },
            TaskKind.Plugin => new PluginTaskConfiguration
            {
                PluginId = ParseId(input.PluginId)
            },
            _ => new HumanApprovalTaskConfiguration()
        };
    }

    private static ITaskConfiguration BuildCompensationTaskConfiguration(
        TaskKind kind,
        CreateTaskInput input,
        string defaultSchemaRegistryProviderKey)
    {
        return kind switch
        {
            TaskKind.Http => new HttpTaskConfiguration
            {
                HasSchemaValidation = input.HasCompensationHttpSchemaValidation,
                BaseUrlVariableRef = input.CompensationHttpBaseUrlVariableRef.Trim(),
                RelativePath = input.CompensationHttpRelativePath.Trim(),
                Method = input.CompensationHttpMethod.Trim().ToUpperInvariant(),
                ExpectedStatusCodes = ParseIntList(input.CompensationHttpExpectedStatusCodes, new[] { 200 }),
                AllowSyncResponse = input.CompensationHttpAllowSyncResponse,
                SchemaBinding = input.HasCompensationHttpSchemaValidation ? CreateSchemaBinding(
                    ElementType.Task,
                    input.CompensationHttpSchemaContractKey,
                    input.CompensationHttpSchemaContractVersion,
                    input.CompensationHttpSchemaRegistryProviderId,
                    SchemaContractKind.CommandRequest,
                    defaultSchemaRegistryProviderKey,
                    input.CompensationHttpSchemaStrictMode,
                    input.HasCompensationHttpSchemaValidation) : null
            },
            TaskKind.Messaging => new MessagingTaskConfiguration
            {
                HasSchemaValidation = input.HasCompensationMessagingSchemaValidation,
                HasRequestValidation = input.HasCompensationMessagingRequestValidation,
                RequestValidation = input.HasCompensationMessagingRequestValidation || input.HasCompensationMessagingSchemaValidation
                    ? BuildValidation(input.CompensationMessagingRequestValidationDsl, input.CompensationMessagingRequestValidationErrorCode)
                    : null,
                HasResponseValidation = input.HasCompensationMessagingResponseValidation,
                ResponseValidation = input.HasCompensationMessagingResponseValidation
                    ? BuildValidation(input.CompensationMessagingResponseValidationDsl, input.CompensationMessagingResponseValidationErrorCode)
                    : null,
                Topic = input.CompensationMessagingTopic.Trim(),
                Version = ParseSemanticVersion(input.CompensationMessagingVersion, new SemanticVersion(1, 0, 0)),
                SchemaBinding = input.HasCompensationMessagingSchemaValidation ? CreateSchemaBinding(
                    ElementType.Task,
                    input.CompensationMessagingSchemaContractKey,
                    input.CompensationMessagingSchemaContractVersion,
                    input.CompensationMessagingSchemaRegistryProviderId,
                    SchemaContractKind.CommandRequest,
                    defaultSchemaRegistryProviderKey,
                    input.CompensationMessagingSchemaStrictMode,
                    input.HasCompensationMessagingSchemaValidation) : null
            },
            TaskKind.Plugin => new PluginTaskConfiguration
            {
                PluginId = ParseId(input.CompensationPluginId)
            },
            _ => new HumanApprovalTaskConfiguration()
        };
    }

    private static RetryPolicy BuildRetryPolicy(
        string strategyType,
        int maxRetries,
        double fixedDelaySeconds,
        string retryableErrorCodes,
        bool stopOnNonRetryableError)
    {
        var requestedStrategy = ParseEnum(strategyType, RetryStrategyType.Fixed);
        var strategy = new FixedRetryStrategy
        {
            Delay = Duration.FromSeconds(Math.Max(0, fixedDelaySeconds))
        };

        return new RetryPolicy
        {
            MaxRetries = Math.Max(0, maxRetries),
            StrategyType = requestedStrategy == RetryStrategyType.Fixed ? requestedStrategy : RetryStrategyType.Fixed,
            Strategy = strategy,
            RetryableErrorCodes = ParseStringList(retryableErrorCodes),
            StopOnNonRetryableError = stopOnNonRetryableError
        };
    }

    private static TimeoutPolicy BuildTimeoutPolicy(
        double timeoutSeconds,
        string timeoutBehavior,
        string failErrorCode,
        double waitSeconds,
        string timeoutAction,
        string reconcileRetryStrategyType,
        int reconcileRetries,
        double reconcileDelaySeconds,
        string reconcileRetryableErrorCodes,
        bool reconcileStopOnNonRetryableError)
    {
        var behavior = ParseEnum(timeoutBehavior, TimeoutBehavior.Fail);
        var action = ParseEnum(timeoutAction, OrchestrationActionOnTimeout.Block);

        return behavior switch
        {
            TimeoutBehavior.Wait => new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(Math.Max(1, timeoutSeconds)),
                TimeoutBehavior = behavior,
                TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicy
                {
                    OrchestrationAction = action,
                    WaitingTime = Duration.FromSeconds(Math.Max(1, waitSeconds))
                }
            },
            TimeoutBehavior.Reconcile => new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(Math.Max(1, timeoutSeconds)),
                TimeoutBehavior = behavior,
                TimeoutBehaviorPolicy = new ReconcileTimeoutBehaviorPolicy
                {
                    OrchestrationAction = action,
                    RetryPolicy = BuildRetryPolicy(
                        reconcileRetryStrategyType,
                        reconcileRetries,
                        reconcileDelaySeconds,
                        reconcileRetryableErrorCodes,
                        reconcileStopOnNonRetryableError)
                }
            },
            _ => new TimeoutPolicy
            {
                Timeout = Duration.FromSeconds(Math.Max(1, timeoutSeconds)),
                TimeoutBehavior = TimeoutBehavior.Fail,
                TimeoutBehaviorPolicy = new FailTimeoutBehaviorPolicy
                {
                    ErrorCode = string.IsNullOrWhiteSpace(failErrorCode) ? "TIMEOUT" : failErrorCode
                }
            }
        };
    }

    private static CompensationDefinition BuildCompensationDefinition(
        CreateTaskInput input,
        string defaultSchemaRegistryProviderKey)
    {
        var compensationKind = ResolveTaskKind(ParseEnum(input.CompensationKind, TaskKind.Messaging));
        var compensationDispatchType = ResolveCompensationDispatchType(compensationKind, ParseEnum(input.CompensationDispatchType, TaskDispatchType.FireAndForget));

        var compensation = DefinitionDefaults.CreateCompensationDefinition(compensationKind);
        compensation.DispatchType = compensationDispatchType;
        compensation.ExecutionCondition = input.HasCompensationExecutionCondition
            ? BuildExecutionCondition(input.CompensationConditionEngine, input.CompensationConditionDslExpression)
            : null;
        compensation.HasExecutionCondition = input.HasCompensationExecutionCondition;
        compensation.Transformation = input.HasCompensationTransformation
            ? BuildTransformation(input.CompensationTransformationEngine, input.CompensationTransformationDsl)
            : null;
        compensation.HasTransformation = input.HasCompensationTransformation;
        compensation.Configuration = BuildCompensationTaskConfiguration(compensationKind, input, defaultSchemaRegistryProviderKey);
        compensation.RetryPolicy = input.HasCompensationRetryPolicy
            ? BuildRetryPolicy(
                input.CompensationRetryStrategyType,
                input.CompensationRetryMaxRetries,
                input.CompensationRetryDelaySeconds,
                input.CompensationRetryableErrorCodes,
                input.CompensationRetryStopOnNonRetryableError)
            : null;
        compensation.TimeoutPolicy = input.HasCompensationTimeoutPolicy
            ? BuildTimeoutPolicy(
                input.CompensationTimeoutSeconds,
                input.CompensationTimeoutBehavior,
                input.CompensationTimeoutFailErrorCode,
                input.CompensationTimeoutWaitSeconds,
                input.CompensationTimeoutAction,
                input.CompensationTimeoutReconcileRetryStrategyType,
                input.CompensationTimeoutReconcileRetries,
                input.CompensationTimeoutReconcileDelaySeconds,
                input.CompensationTimeoutReconcileRetryableErrorCodes,
                input.CompensationTimeoutReconcileStopOnNonRetryableError)
            : null;

        return compensation;
    }

    private static TaskDispatchType ResolveDispatchType(TaskKind kind, TaskDispatchType requested)
    {
        var allowed = GetAllowedDispatchTypes(kind);
        return allowed.Contains(requested) ? requested : allowed[0];
    }

    private static TaskDispatchType ResolveCompensationDispatchType(TaskKind kind, TaskDispatchType requested)
    {
        var allowed = GetAllowedCompensationDispatchTypes(kind);
        return allowed.Contains(requested) ? requested : allowed[0];
    }

    private static TaskKind ResolveTaskKind(TaskKind requested)
        => requested == TaskKind.Messaging ? requested : TaskKind.Messaging;

    private static TaskDispatchType[] GetAllowedDispatchTypes(TaskKind kind)
    {
        return kind switch
        {
            TaskKind.Messaging => new[] { TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback },
            _ => new[] { TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback },
        };
    }

    private static TaskDispatchType[] GetAllowedCompensationDispatchTypes(TaskKind kind)
    {
        return kind switch
        {
            TaskKind.Messaging => new[] { TaskDispatchType.FireAndForget },
            _ => new[] { TaskDispatchType.FireAndForget },
        };
    }

    private static List<string> ParseStringList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static List<int> ParseIntList(string value, IEnumerable<int> fallback)
    {
        var result = string.IsNullOrWhiteSpace(value)
            ? new List<int>()
            : value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var number) ? number : -1)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

        if (result.Count > 0)
        {
            return result;
        }

        return fallback.ToList();
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

    private static SchemaBinding CreateSchemaBinding(
        ElementType elementType,
        string contractKey,
        string contractVersion,
        string registryProviderId,
        SchemaContractKind contractKind,
        string defaultSchemaRegistryProviderKey,
        bool strictMode,
        bool isValidationEnabled = true)
    {
        return new SchemaBinding
        {
            Id = Id.New(),
            ElementType = elementType,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = contractKey.Trim(),
            ContractVersion = ParseSemanticVersion(contractVersion, new SemanticVersion(1, 0, 0)),
            RegistryProviderId = ParseId(registryProviderId),
            RegistryProviderKey = defaultSchemaRegistryProviderKey,
            ContractKind = contractKind,
            StrictMode = strictMode,
            IsValidationEnabled = isValidationEnabled
        };
    }

    private static string NormalizeProviderKey(string providerKey)
        => string.IsNullOrWhiteSpace(providerKey) ? "knowl" : providerKey.Trim();

    public sealed class CreateTaskInput
    {
        public string TaskId { get; set; } = string.Empty;

        [Required]
        public string StageId { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Kind { get; set; } = TaskKind.Messaging.ToString();

        [Required]
        public string ExecutionMode { get; set; } = TaskExecutionMode.Sequential.ToString();

        public string ParallelGroupId { get; set; } = string.Empty;

        [Required]
        public string DispatchType { get; set; } = TaskDispatchType.FireAndWaitCallback.ToString();

        [Required]
        public string OnErrorPolicy { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.OnErrorPolicy.Stop.ToString();

        public bool IsEnabled { get; set; } = true;

        public string Notes { get; set; } = string.Empty;

        public bool HasExecutionCondition { get; set; }

        [Required]
        public string ConditionEngine { get; set; } = EngineType.DSL.ToString();

        public string ConditionDslExpression { get; set; } = "true";

        [Required]
        public string TransformationEngine { get; set; } = EngineType.DSL.ToString();

        public bool HasTransformation { get; set; }

        public string TransformationDsl { get; set; } = string.Empty;

        public string HttpBaseUrlVariableRef { get; set; } = string.Empty;

        public string HttpRelativePath { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = "GET";

        [RegularExpression(@"^([1-5]\d{2})(\s*,\s*[1-5]\d{2})*$", ErrorMessage = "Use HTTP status codes separated by commas.")]
        public string HttpExpectedStatusCodes { get; set; } = "200";

        public bool HttpAllowSyncResponse { get; set; }

        public bool HasHttpSchemaValidation { get; set; }

        public string HttpSchemaContractKey { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string HttpSchemaContractVersion { get; set; } = "1.0.0";

        public string HttpSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool HttpSchemaStrictMode { get; set; }

        [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
        public string MessagingTopic { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string MessagingVersion { get; set; } = "1.0.0";

        public bool HasMessagingSchemaValidation { get; set; }

        public string MessagingSchemaContractKey { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string MessagingSchemaContractVersion { get; set; } = "1.0.0";

        public string MessagingSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool MessagingSchemaStrictMode { get; set; }

        public bool HasMessagingRequestValidation { get; set; }

        public string MessagingRequestValidationDsl { get; set; } = string.Empty;

        public string MessagingRequestValidationErrorCode { get; set; } = "RequestValidationFailed";

        public bool HasMessagingResponseValidation { get; set; }

        public string MessagingResponseValidationDsl { get; set; } = string.Empty;

        public string MessagingResponseValidationErrorCode { get; set; } = "ResponseValidationFailed";

        public string PluginId { get; set; } = string.Empty;

        [Required]
        public string RetryStrategyType { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.RetryStrategyType.Fixed.ToString();

        public int RetryMaxRetries { get; set; } = 0;

        public double RetryDelaySeconds { get; set; } = 1;

        public string RetryableErrorCodes { get; set; } = string.Empty;

        public bool RetryStopOnNonRetryableError { get; set; }

        public bool HasRetryPolicy { get; set; }

        public double TimeoutSeconds { get; set; } = 30;

        [Required]
        public string TimeoutBehavior { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.TimeoutBehavior.Fail.ToString();

        public string TimeoutFailErrorCode { get; set; } = "TIMEOUT";

        public double TimeoutWaitSeconds { get; set; } = 15;

        [Required]
        public string TimeoutAction { get; set; } = OrchestrationActionOnTimeout.Block.ToString();

        [Required]
        public string TimeoutReconcileRetryStrategyType { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.RetryStrategyType.Fixed.ToString();

        public int TimeoutReconcileRetries { get; set; } = 3;

        public double TimeoutReconcileDelaySeconds { get; set; } = 5;

        public string TimeoutReconcileRetryableErrorCodes { get; set; } = string.Empty;

        public bool TimeoutReconcileStopOnNonRetryableError { get; set; }

        public bool HasTimeoutPolicy { get; set; }

        [Required]
        public string CompensationKind { get; set; } = TaskKind.Messaging.ToString();

        public bool HasCompensation { get; set; }

        public bool HasCompensationExecutionCondition { get; set; }

        public bool HasCompensationTransformation { get; set; }

        public bool HasCompensationRetryPolicy { get; set; }

        public bool HasCompensationTimeoutPolicy { get; set; }

        public string CompensationHttpBaseUrlVariableRef { get; set; } = string.Empty;

        public string CompensationHttpRelativePath { get; set; } = string.Empty;

        public string CompensationHttpMethod { get; set; } = "GET";

        [RegularExpression(@"^([1-5]\d{2})(\s*,\s*[1-5]\d{2})*$", ErrorMessage = "Use HTTP status codes separated by commas.")]
        public string CompensationHttpExpectedStatusCodes { get; set; } = "200";

        public bool CompensationHttpAllowSyncResponse { get; set; }

        public bool HasCompensationHttpSchemaValidation { get; set; }

        public string CompensationHttpSchemaContractKey { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string CompensationHttpSchemaContractVersion { get; set; } = "1.0.0";

        public string CompensationHttpSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool CompensationHttpSchemaStrictMode { get; set; }

        [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
        public string CompensationMessagingTopic { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string CompensationMessagingVersion { get; set; } = "1.0.0";

        public bool HasCompensationMessagingSchemaValidation { get; set; }

        public string CompensationMessagingSchemaContractKey { get; set; } = string.Empty;

        [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Use semantic version format, for example 1.0.0.")]
        public string CompensationMessagingSchemaContractVersion { get; set; } = "1.0.0";

        public string CompensationMessagingSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool CompensationMessagingSchemaStrictMode { get; set; }

        public string CompensationPluginId { get; set; } = string.Empty;

        [Required]
        public string CompensationDispatchType { get; set; } = TaskDispatchType.FireAndForget.ToString();

        [Required]
        public string CompensationConditionEngine { get; set; } = EngineType.DSL.ToString();

        public string CompensationConditionDslExpression { get; set; } = "true";

        [Required]
        public string CompensationTransformationEngine { get; set; } = EngineType.DSL.ToString();

        public string CompensationTransformationDsl { get; set; } = string.Empty;

        public bool HasCompensationMessagingRequestValidation { get; set; }

        public string CompensationMessagingRequestValidationDsl { get; set; } = string.Empty;

        public string CompensationMessagingRequestValidationErrorCode { get; set; } = "RequestValidationFailed";

        public bool HasCompensationMessagingResponseValidation { get; set; }

        public string CompensationMessagingResponseValidationDsl { get; set; } = string.Empty;

        public string CompensationMessagingResponseValidationErrorCode { get; set; } = "ResponseValidationFailed";

        [Required]
        public string CompensationRetryStrategyType { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.RetryStrategyType.Fixed.ToString();

        public int CompensationRetryMaxRetries { get; set; } = 0;

        public double CompensationRetryDelaySeconds { get; set; } = 1;

        public string CompensationRetryableErrorCodes { get; set; } = string.Empty;

        public bool CompensationRetryStopOnNonRetryableError { get; set; }

        public double CompensationTimeoutSeconds { get; set; } = 30;

        [Required]
        public string CompensationTimeoutBehavior { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.TimeoutBehavior.Fail.ToString();

        public string CompensationTimeoutFailErrorCode { get; set; } = "TIMEOUT";

        public double CompensationTimeoutWaitSeconds { get; set; } = 15;

        [Required]
        public string CompensationTimeoutAction { get; set; } = OrchestrationActionOnTimeout.Block.ToString();

        [Required]
        public string CompensationTimeoutReconcileRetryStrategyType { get; set; } = Krackend.Sagas.Orchestrations.Abstractions.Primitives.RetryStrategyType.Fixed.ToString();

        public int CompensationTimeoutReconcileRetries { get; set; } = 3;

        public double CompensationTimeoutReconcileDelaySeconds { get; set; } = 5;

        public string CompensationTimeoutReconcileRetryableErrorCodes { get; set; } = string.Empty;

        public bool CompensationTimeoutReconcileStopOnNonRetryableError { get; set; }
    }

    /// <summary>
    /// Represents reorder payload for task drag and drop.
    /// </summary>
    public sealed class ReorderTasksRequest
    {
        /// <summary>
        /// Gets or sets stage identifier.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets ordered task placements.
        /// </summary>
        public List<ReorderTaskPlacementRequest> Items { get; set; } = new();
    }

    /// <summary>
    /// Represents placement data of a task inside/outside a parallel group.
    /// </summary>
    public sealed class ReorderTaskPlacementRequest
    {
        /// <summary>
        /// Gets or sets task identifier.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets target parallel group identifier. Empty means sequential lane.
        /// </summary>
        public string ParallelGroupId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents create payload for a parallel group.
    /// </summary>
    public sealed class CreateParallelGroupRequest
    {
        /// <summary>
        /// Gets or sets stage identifier.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets requested group name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents update payload for a parallel group.
    /// </summary>
    public sealed class UpdateParallelGroupRequest
    {
        /// <summary>
        /// Gets or sets stage identifier.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets group identifier.
        /// </summary>
        public string GroupId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets group name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets group join policy.
        /// </summary>
        public string JoinPolicy { get; set; } = ParallelJoinPolicy.WaitAll.ToString();

        /// <summary>
        /// Gets or sets max parallel agents.
        /// </summary>
        public int? MaxParallelAgents { get; set; }
    }

    /// <summary>
    /// Represents delete payload for a parallel group.
    /// </summary>
    public sealed class DeleteParallelGroupRequest
    {
        /// <summary>
        /// Gets or sets stage identifier.
        /// </summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets group identifier.
        /// </summary>
        public string GroupId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents toggle payload for task enabled state.
    /// </summary>
    public sealed class SetTaskEnabledRequest
    {
        /// <summary>
        /// Gets or sets task identifier.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets enabled state.
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Represents payload to set task execution condition.
    /// </summary>
    public sealed class SetTaskExecutionConditionRequest
    {
        /// <summary>
        /// Gets or sets task identifier.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether execution condition is active.
        /// </summary>
        public bool HasExecutionCondition { get; set; }

        /// <summary>
        /// Gets or sets condition engine.
        /// </summary>
        public string ConditionEngine { get; set; } = EngineType.DSL.ToString();

        /// <summary>
        /// Gets or sets DSL expression.
        /// </summary>
        public string ConditionDslExpression { get; set; } = "true";
    }

    /// <summary>
    /// Represents payload to set task transformation.
    /// </summary>
    public sealed class SetTaskTransformationRequest
    {
        /// <summary>
        /// Gets or sets task identifier.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether transformation is active.
        /// </summary>
        public bool HasTransformation { get; set; }

        /// <summary>
        /// Gets or sets transformation engine.
        /// </summary>
        public string TransformationEngine { get; set; } = EngineType.DSL.ToString();

        /// <summary>
        /// Gets or sets transformation DSL.
        /// </summary>
        public string TransformationDsl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets schema context signature used to author the transformation.
        /// </summary>
        public string SourceContextHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets target schema snapshot hash used to author the transformation.
        /// </summary>
        public string TargetSchemaHash { get; set; } = string.Empty;
    }
}
