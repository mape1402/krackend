using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.Design.Interaction;
using Krackend.Sagas.Orchestrations.Design.WebUI.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Design.WebUI.Areas.OrchestratorDesign.Pages.OrchestrationStages;

/// <summary>
/// Represents stage-level task management.
/// </summary>
public sealed class DetailsModel : PageModel
{
    private readonly IStageInteractionService _stageService;
    private readonly ITaskInteractionService _taskService;
    private readonly IParallelGroupInteractionService _parallelGroupService;

    public DetailsModel(
        IStageInteractionService stageService,
        ITaskInteractionService taskService,
        IParallelGroupInteractionService parallelGroupService)
    {
        _stageService = stageService ?? throw new ArgumentNullException(nameof(stageService));
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _parallelGroupService = parallelGroupService ?? throw new ArgumentNullException(nameof(parallelGroupService));
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

    public IReadOnlyCollection<TaskDefinitionModel> Tasks { get; private set; } = Array.Empty<TaskDefinitionModel>();

    public IReadOnlyCollection<ParallelGroupDefinitionModel> ParallelGroups { get; private set; } = Array.Empty<ParallelGroupDefinitionModel>();

    [BindProperty]
    public CreateTaskInput NewTask { get; set; } = new();

    public IEnumerable<SelectListItem> TaskKinds => Enum.GetValues<TaskKind>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> ExecutionModes => Enum.GetValues<TaskExecutionMode>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> DispatchTypes => Enum.GetValues<TaskDispatchType>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> OnErrorPolicies => Enum.GetValues<OnErrorPolicy>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> EngineTypes => Enum.GetValues<EngineType>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

    public IEnumerable<SelectListItem> RetryStrategyTypes => Enum.GetValues<RetryStrategyType>().Select(x => new SelectListItem(x.ToString(), x.ToString()));

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
        if (Stage is not null && !string.IsNullOrWhiteSpace(EditTaskId))
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
        var effectiveTaskId = string.IsNullOrWhiteSpace(taskId) ? NewTask.TaskId : taskId;
        if (!string.IsNullOrWhiteSpace(effectiveTaskId))
        {
            var current = await _taskService.GetById(new GetTaskDefinitionByIdQuery(effectiveTaskId), cancellationToken);
            if (current is null)
            {
                return RedirectToPage("/OrchestrationStages/Details", new { area = "OrchestratorDesign", orchestrationId, versionId, stageId = NewTask.StageId });
            }

            var editKind = ParseEnum(NewTask.Kind, current.Kind);
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
                        ? BuildTransformation(NewTask.TransformationEngine)
                        : null,
                    BuildTaskConfiguration(editKind, NewTask),
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
                    NewTask.HasCompensation ? BuildCompensationDefinition(NewTask) : null,
                    editDispatchType,
                    current.IsEnabled),
                cancellationToken);

            return RedirectToPage("/OrchestrationStages/Details", new { area = "OrchestratorDesign", orchestrationId, versionId, stageId = NewTask.StageId });
        }

        var kind = ParseEnum(NewTask.Kind, TaskKind.HumanApproval);
        var executionMode = ParseEnum(NewTask.ExecutionMode, TaskExecutionMode.Sequential);
        var dispatchType = ResolveDispatchType(kind, ParseEnum(NewTask.DispatchType, TaskDispatchType.FireAndWait));
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
                    ? BuildTransformation(NewTask.TransformationEngine)
                    : null,
                BuildTaskConfiguration(kind, NewTask),
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
                NewTask.HasCompensation ? BuildCompensationDefinition(NewTask) : null,
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
    /// Updates transformation of one task.
    /// </summary>
    public async Task<IActionResult> OnPostSetTaskTransformationAsync([FromBody] SetTaskTransformationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TaskId))
        {
            return BadRequest();
        }

        var transformation = request.HasTransformation
            ? BuildTransformation(request.TransformationEngine)
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
            ["HasExecutionCondition"] = task.ExecutionCondition is not null,
            ["ConditionEngine"] = task.ExecutionCondition?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["ConditionDslExpression"] = (task.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? "true",
            ["HasTransformation"] = task.Transformation is not null,
            ["TransformationEngine"] = task.Transformation?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["HasRetryPolicy"] = task.RetryPolicy is not null,
            ["HasTimeoutPolicy"] = task.TimeoutPolicy is not null,
            ["HasCompensation"] = task.CompensationDefinition is not null,
            ["CompensationKind"] = task.CompensationDefinition?.CompensationTaskKind.ToString() ?? TaskKind.HumanApproval.ToString(),
            ["CompensationDispatchType"] = task.CompensationDefinition?.DispatchType.ToString() ?? TaskDispatchType.FireAndWait.ToString(),
            ["HasCompensationExecutionCondition"] = task.CompensationDefinition?.ExecutionCondition is not null,
            ["CompensationConditionEngine"] = task.CompensationDefinition?.ExecutionCondition?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["CompensationConditionDslExpression"] = (task.CompensationDefinition?.ExecutionCondition?.Configuration as DslConditionConfiguration)?.Expression.ToString() ?? "true",
            ["HasCompensationTransformation"] = task.CompensationDefinition?.Transformation is not null,
            ["CompensationTransformationEngine"] = task.CompensationDefinition?.Transformation?.Engine.ToString() ?? EngineType.DSL.ToString(),
            ["HasCompensationRetryPolicy"] = task.CompensationDefinition?.RetryPolicy is not null,
            ["HasCompensationTimeoutPolicy"] = task.CompensationDefinition?.TimeoutPolicy is not null,
        };

        if (task.Configuration is HttpTaskConfiguration http)
        {
            payload["HttpBaseUrlVariableRef"] = http.BaseUrlVariableRef ?? "vars.baseUrl";
            payload["HttpRelativePath"] = http.RelativePath ?? "/";
            payload["HttpMethod"] = http.Method ?? "GET";
            payload["HttpExpectedStatusCodes"] = string.Join(',', http.ExpectedStatusCodes ?? new List<int> { 200 });
            payload["HttpAllowSyncResponse"] = http.AllowSyncResponse;
            payload["HttpSchemaContractKey"] = http.SchemaBinding?.ContractKey ?? "contract.placeholder";
            payload["HttpSchemaContractVersion"] = http.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
            payload["HttpSchemaRegistryProviderId"] = http.SchemaBinding is null ? string.Empty : http.SchemaBinding.RegistryProviderId.ToString();
            payload["HttpSchemaStrictMode"] = http.SchemaBinding?.StrictMode ?? false;
        }
        else if (task.Configuration is MessagingTaskConfiguration messaging)
        {
            payload["MessagingTopic"] = messaging.Topic ?? "orchestrator.topic";
            payload["MessagingVersion"] = messaging.Version.ToString();
            payload["MessagingSchemaContractKey"] = messaging.SchemaBinding?.ContractKey ?? "contract.placeholder";
            payload["MessagingSchemaContractVersion"] = messaging.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
            payload["MessagingSchemaRegistryProviderId"] = messaging.SchemaBinding is null ? string.Empty : messaging.SchemaBinding.RegistryProviderId.ToString();
            payload["MessagingSchemaStrictMode"] = messaging.SchemaBinding?.StrictMode ?? false;
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
                payload["CompensationHttpBaseUrlVariableRef"] = compHttp.BaseUrlVariableRef ?? "vars.baseUrl";
                payload["CompensationHttpRelativePath"] = compHttp.RelativePath ?? "/";
                payload["CompensationHttpMethod"] = compHttp.Method ?? "GET";
                payload["CompensationHttpExpectedStatusCodes"] = string.Join(',', compHttp.ExpectedStatusCodes ?? new List<int> { 200 });
                payload["CompensationHttpAllowSyncResponse"] = compHttp.AllowSyncResponse;
                payload["CompensationHttpSchemaContractKey"] = compHttp.SchemaBinding?.ContractKey ?? "contract.placeholder";
                payload["CompensationHttpSchemaContractVersion"] = compHttp.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
                payload["CompensationHttpSchemaRegistryProviderId"] = compHttp.SchemaBinding is null ? string.Empty : compHttp.SchemaBinding.RegistryProviderId.ToString();
                payload["CompensationHttpSchemaStrictMode"] = compHttp.SchemaBinding?.StrictMode ?? false;
            }
            else if (task.CompensationDefinition.Configuration is MessagingTaskConfiguration compMsg)
            {
                payload["CompensationMessagingTopic"] = compMsg.Topic ?? "orchestrator.topic";
                payload["CompensationMessagingVersion"] = compMsg.Version.ToString();
                payload["CompensationMessagingSchemaContractKey"] = compMsg.SchemaBinding?.ContractKey ?? "contract.placeholder";
                payload["CompensationMessagingSchemaContractVersion"] = compMsg.SchemaBinding?.ContractVersion.ToString() ?? "1.0.0";
                payload["CompensationMessagingSchemaRegistryProviderId"] = compMsg.SchemaBinding is null ? string.Empty : compMsg.SchemaBinding.RegistryProviderId.ToString();
                payload["CompensationMessagingSchemaStrictMode"] = compMsg.SchemaBinding?.StrictMode ?? false;
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
        var hasExecutionCondition = task.ExecutionCondition is not null;

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
            hasTransformation = task.Transformation is not null,
            transformationEngine = task.Transformation?.Engine.ToString() ?? EngineType.DSL.ToString(),
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

    private static TransformationDefinition BuildTransformation(string engineText)
    {
        var engine = ParseEnum(engineText, EngineType.DSL);
        if (engine == EngineType.DSL)
        {
            return new TransformationDefinition
            {
                Engine = engine,
                Configuration = new DslTransformationConfiguration()
            };
        }

        return new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration()
        };
    }

    private static ITaskConfiguration BuildTaskConfiguration(TaskKind kind, CreateTaskInput input)
    {
        return kind switch
        {
            TaskKind.Http => new HttpTaskConfiguration
            {
                BaseUrlVariableRef = string.IsNullOrWhiteSpace(input.HttpBaseUrlVariableRef) ? "vars.baseUrl" : input.HttpBaseUrlVariableRef,
                RelativePath = string.IsNullOrWhiteSpace(input.HttpRelativePath) ? "/" : input.HttpRelativePath,
                Method = string.IsNullOrWhiteSpace(input.HttpMethod) ? "GET" : input.HttpMethod.ToUpperInvariant(),
                ExpectedStatusCodes = ParseIntList(input.HttpExpectedStatusCodes, new[] { 200 }),
                AllowSyncResponse = input.HttpAllowSyncResponse,
                SchemaBinding = CreateSchemaBinding(
                    ElementType.Task,
                    input.HttpSchemaContractKey,
                    input.HttpSchemaContractVersion,
                    input.HttpSchemaRegistryProviderId,
                    input.HttpSchemaStrictMode)
            },
            TaskKind.Messaging => new MessagingTaskConfiguration
            {
                Topic = string.IsNullOrWhiteSpace(input.MessagingTopic) ? "orchestrator.topic" : input.MessagingTopic,
                Version = ParseSemanticVersion(input.MessagingVersion, new SemanticVersion(1, 0, 0)),
                SchemaBinding = CreateSchemaBinding(
                    ElementType.Task,
                    input.MessagingSchemaContractKey,
                    input.MessagingSchemaContractVersion,
                    input.MessagingSchemaRegistryProviderId,
                    input.MessagingSchemaStrictMode)
            },
            TaskKind.Plugin => new PluginTaskConfiguration
            {
                PluginId = ParseId(input.PluginId)
            },
            _ => new HumanApprovalTaskConfiguration()
        };
    }

    private static ITaskConfiguration BuildCompensationTaskConfiguration(TaskKind kind, CreateTaskInput input)
    {
        return kind switch
        {
            TaskKind.Http => new HttpTaskConfiguration
            {
                BaseUrlVariableRef = string.IsNullOrWhiteSpace(input.CompensationHttpBaseUrlVariableRef) ? "vars.baseUrl" : input.CompensationHttpBaseUrlVariableRef,
                RelativePath = string.IsNullOrWhiteSpace(input.CompensationHttpRelativePath) ? "/" : input.CompensationHttpRelativePath,
                Method = string.IsNullOrWhiteSpace(input.CompensationHttpMethod) ? "GET" : input.CompensationHttpMethod.ToUpperInvariant(),
                ExpectedStatusCodes = ParseIntList(input.CompensationHttpExpectedStatusCodes, new[] { 200 }),
                AllowSyncResponse = input.CompensationHttpAllowSyncResponse,
                SchemaBinding = CreateSchemaBinding(
                    ElementType.Task,
                    input.CompensationHttpSchemaContractKey,
                    input.CompensationHttpSchemaContractVersion,
                    input.CompensationHttpSchemaRegistryProviderId,
                    input.CompensationHttpSchemaStrictMode)
            },
            TaskKind.Messaging => new MessagingTaskConfiguration
            {
                Topic = string.IsNullOrWhiteSpace(input.CompensationMessagingTopic) ? "orchestrator.topic" : input.CompensationMessagingTopic,
                Version = ParseSemanticVersion(input.CompensationMessagingVersion, new SemanticVersion(1, 0, 0)),
                SchemaBinding = CreateSchemaBinding(
                    ElementType.Task,
                    input.CompensationMessagingSchemaContractKey,
                    input.CompensationMessagingSchemaContractVersion,
                    input.CompensationMessagingSchemaRegistryProviderId,
                    input.CompensationMessagingSchemaStrictMode)
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
        var strategy = requestedStrategy switch
        {
            RetryStrategyType.Fixed => new FixedRetryStrategy
            {
                Delay = Duration.FromSeconds(Math.Max(0, fixedDelaySeconds))
            },
            _ => new FixedRetryStrategy
            {
                Delay = Duration.FromSeconds(Math.Max(0, fixedDelaySeconds))
            }
        };

        return new RetryPolicy
        {
            MaxRetries = Math.Max(0, maxRetries),
            StrategyType = requestedStrategy,
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

    private static CompensationDefinition BuildCompensationDefinition(CreateTaskInput input)
    {
        var compensationKind = ParseEnum(input.CompensationKind, TaskKind.HumanApproval);
        var compensationDispatchType = ResolveDispatchType(compensationKind, ParseEnum(input.CompensationDispatchType, TaskDispatchType.FireAndWait));

        var compensation = DefinitionDefaults.CreateCompensationDefinition(compensationKind);
        compensation.DispatchType = compensationDispatchType;
        compensation.ExecutionCondition = input.HasCompensationExecutionCondition
            ? BuildExecutionCondition(input.CompensationConditionEngine, input.CompensationConditionDslExpression)
            : null;
        compensation.Transformation = input.HasCompensationTransformation
            ? BuildTransformation(input.CompensationTransformationEngine)
            : null;
        compensation.Configuration = BuildCompensationTaskConfiguration(compensationKind, input);
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

    private static TaskDispatchType[] GetAllowedDispatchTypes(TaskKind kind)
    {
        return kind switch
        {
            TaskKind.HumanApproval => new[] { TaskDispatchType.FireAndWaitCallback },
            TaskKind.Messaging => new[] { TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback },
            TaskKind.Http => new[] { TaskDispatchType.FireAndWait, TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback },
            TaskKind.Plugin => new[] { TaskDispatchType.FireAndWait, TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback },
            _ => new[] { TaskDispatchType.FireAndWait, TaskDispatchType.FireAndForget, TaskDispatchType.FireAndWaitCallback },
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
        bool strictMode)
    {
        return new SchemaBinding
        {
            Id = Id.New(),
            ElementType = elementType,
            ElementId = Id.New(),
            ContractId = Id.New(),
            ContractKey = string.IsNullOrWhiteSpace(contractKey) ? "contract.placeholder" : contractKey,
            ContractVersion = ParseSemanticVersion(contractVersion, new SemanticVersion(1, 0, 0)),
            RegistryProviderId = ParseId(registryProviderId),
            StrictMode = strictMode
        };
    }

    public sealed class CreateTaskInput
    {
        public string TaskId { get; set; } = string.Empty;

        [Required]
        public string StageId { get; set; } = string.Empty;

        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Kind { get; set; } = TaskKind.HumanApproval.ToString();

        [Required]
        public string ExecutionMode { get; set; } = TaskExecutionMode.Sequential.ToString();

        public string ParallelGroupId { get; set; } = string.Empty;

        [Required]
        public string DispatchType { get; set; } = TaskDispatchType.FireAndWait.ToString();

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

        public string HttpBaseUrlVariableRef { get; set; } = "vars.baseUrl";

        public string HttpRelativePath { get; set; } = "/";

        public string HttpMethod { get; set; } = "GET";

        public string HttpExpectedStatusCodes { get; set; } = "200";

        public bool HttpAllowSyncResponse { get; set; }

        public string HttpSchemaContractKey { get; set; } = "contract.placeholder";

        public string HttpSchemaContractVersion { get; set; } = "1.0.0";

        public string HttpSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool HttpSchemaStrictMode { get; set; }

        public string MessagingTopic { get; set; } = "orchestrator.topic";

        public string MessagingVersion { get; set; } = "1.0.0";

        public string MessagingSchemaContractKey { get; set; } = "contract.placeholder";

        public string MessagingSchemaContractVersion { get; set; } = "1.0.0";

        public string MessagingSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool MessagingSchemaStrictMode { get; set; }

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
        public string CompensationKind { get; set; } = TaskKind.HumanApproval.ToString();

        public bool HasCompensation { get; set; }

        public bool HasCompensationExecutionCondition { get; set; }

        public bool HasCompensationTransformation { get; set; }

        public bool HasCompensationRetryPolicy { get; set; }

        public bool HasCompensationTimeoutPolicy { get; set; }

        public string CompensationHttpBaseUrlVariableRef { get; set; } = "vars.baseUrl";

        public string CompensationHttpRelativePath { get; set; } = "/";

        public string CompensationHttpMethod { get; set; } = "GET";

        public string CompensationHttpExpectedStatusCodes { get; set; } = "200";

        public bool CompensationHttpAllowSyncResponse { get; set; }

        public string CompensationHttpSchemaContractKey { get; set; } = "contract.placeholder";

        public string CompensationHttpSchemaContractVersion { get; set; } = "1.0.0";

        public string CompensationHttpSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool CompensationHttpSchemaStrictMode { get; set; }

        public string CompensationMessagingTopic { get; set; } = "orchestrator.topic";

        public string CompensationMessagingVersion { get; set; } = "1.0.0";

        public string CompensationMessagingSchemaContractKey { get; set; } = "contract.placeholder";

        public string CompensationMessagingSchemaContractVersion { get; set; } = "1.0.0";

        public string CompensationMessagingSchemaRegistryProviderId { get; set; } = string.Empty;

        public bool CompensationMessagingSchemaStrictMode { get; set; }

        public string CompensationPluginId { get; set; } = string.Empty;

        [Required]
        public string CompensationDispatchType { get; set; } = TaskDispatchType.FireAndWait.ToString();

        [Required]
        public string CompensationConditionEngine { get; set; } = EngineType.DSL.ToString();

        public string CompensationConditionDslExpression { get; set; } = "true";

        [Required]
        public string CompensationTransformationEngine { get; set; } = EngineType.DSL.ToString();

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
    }
}
