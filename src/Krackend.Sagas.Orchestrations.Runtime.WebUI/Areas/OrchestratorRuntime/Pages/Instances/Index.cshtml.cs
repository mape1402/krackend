using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;

public sealed class IndexModel : PageModel
{
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly RuntimeEnvironmentDescriptor _runtimeEnvironment;
    private readonly OrchestratorRuntimeWebUIOptions _options;

    public IndexModel(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        IExecutionTransitionRepository transitionRepository,
        RuntimeEnvironmentDescriptor runtimeEnvironment,
        IOptions<OrchestratorRuntimeWebUIOptions> options)
    {
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _dispatchRepository = dispatchRepository;
        _transitionRepository = transitionRepository;
        _runtimeEnvironment = runtimeEnvironment;
        _options = options.Value;
    }

    public string EnvironmentKey => _runtimeEnvironment.EnvironmentKey;

    public string LiveHubPath
    {
        get
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/');
            return $"/{prefix}/live";
        }
    }

    public IReadOnlyCollection<InstanceRowModel> Instances { get; private set; } = Array.Empty<InstanceRowModel>();

    public IReadOnlyCollection<TrafficPointModel> Traffic { get; private set; } = Array.Empty<TrafficPointModel>();

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var instances = await _instanceRepository.GetRecent(EnvironmentKey, 75, cancellationToken);
        Instances = instances.Select(ToRow).ToArray();

        var transitions = await _transitionRepository.GetRecent(EnvironmentKey, 250, cancellationToken);
        Traffic = transitions
            .GroupBy(x => new DateTime(x.OccurredOnUtc.Year, x.OccurredOnUtc.Month, x.OccurredOnUtc.Day, x.OccurredOnUtc.Hour, x.OccurredOnUtc.Minute, 0, DateTimeKind.Utc))
            .OrderBy(x => x.Key)
            .Select(x => new TrafficPointModel(x.Key, x.Count()))
            .ToArray();
    }

    public async Task<IActionResult> OnGetDetailAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return BadRequest();

        var id = ParseId(instanceId);
        var instance = await _instanceRepository.GetById(id, cancellationToken);
        var stages = (await _stageRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.Order).ToArray();
        var tasks = (await _taskRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.StartedOnUtc).ToArray();
        var transitions = (await _transitionRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.OccurredOnUtc).ToArray();

        var taskDetails = new List<TaskDetailModel>();
        foreach (var task in tasks)
        {
            var attempts = (await _attemptRepository.GetByTaskExecutionId(task.Id, cancellationToken)).OrderBy(x => x.AttemptNumber).ToArray();
            var attemptDetails = new List<TaskAttemptDetailModel>();
            foreach (var attempt in attempts)
            {
                var dispatch = attempt.DispatchId is null
                    ? await _dispatchRepository.GetByAttemptId(attempt.Id, cancellationToken)
                    : await _dispatchRepository.GetById(attempt.DispatchId.Value, cancellationToken);

                attemptDetails.Add(new TaskAttemptDetailModel(
                    attempt.Id.ToString(),
                    attempt.AttemptNumber,
                    attempt.Status.ToString(),
                    attempt.StartedOnUtc,
                    attempt.WaitingSinceUtc,
                    attempt.CompletedOnUtc,
                    attempt.FailedOnUtc,
                    attempt.TimedOutOnUtc,
                    FormatJson(attempt.RequestPayload),
                    FormatJson(attempt.ResponsePayload),
                    attempt.ErrorCode,
                    attempt.ErrorMessage,
                    dispatch is null ? null : ToDispatch(dispatch),
                    FormatJson(attempt.Metadata)));
            }

            taskDetails.Add(new TaskDetailModel(
                task.Id.ToString(),
                task.StageExecutionId.ToString(),
                task.TaskKey,
                task.TaskKind.ToString(),
                task.ExecutionMode.ToString(),
                task.Status.ToString(),
                task.AwaitResponse,
                task.CorrelationId,
                task.StartedOnUtc,
                task.WaitingSinceUtc,
                task.CompletedOnUtc,
                task.FailedOnUtc,
                task.TimedOutOnUtc,
                task.LastAttemptNumber,
                FormatJson(task.OutputVariablesPayload),
                FormatJson(task.Metadata),
                attemptDetails));
        }

        return new JsonResult(new InstanceDetailModel(
            ToRow(instance),
            stages.Select(stage => ToStage(stage, taskDetails)).ToArray(),
            taskDetails,
            transitions.Select(ToTransition).ToArray(),
            FormatJson(instance.SnapshotPayload),
            FormatJson(instance.Metadata)));
    }

    public static string StatusClass(string status)
    {
        return status switch
        {
            nameof(OrchestrationInstanceStatus.Running) => "od-status-running",
            nameof(OrchestrationInstanceStatus.Waiting) => "od-status-waiting",
            nameof(OrchestrationInstanceStatus.Completed) => "od-status-active",
            nameof(OrchestrationInstanceStatus.Failed) => "od-status-danger",
            _ => "od-status-inactive"
        };
    }

    private static InstanceRowModel ToRow(OrchestrationInstance instance)
    {
        return new InstanceRowModel(
            instance.Id.ToString(),
            instance.OrchestrationDefinitionKey,
            instance.CorrelationId,
            instance.ExecutionKey,
            instance.Status.ToString(),
            StatusClass(instance.Status.ToString()),
            instance.CurrentStageKey,
            instance.CurrentTaskKey,
            instance.StartedOnUtc,
            instance.LastUpdatedOnUtc,
            instance.WaitingSinceUtc,
            instance.CompletedOnUtc,
            instance.FailedOnUtc,
            instance.ErrorSummary);
    }

    private static StageDetailModel ToStage(StageExecution stage, IReadOnlyCollection<TaskDetailModel> tasks)
    {
        return new StageDetailModel(
            stage.Id.ToString(),
            stage.StageKey,
            stage.Order,
            stage.Status.ToString(),
            StatusClass(stage.Status.ToString()),
            stage.StartedOnUtc,
            stage.CompletedOnUtc,
            stage.FailedOnUtc,
            stage.ErrorSummary,
            FormatJson(stage.Metadata),
            tasks.Where(x => x.StageExecutionId == stage.Id.ToString()).ToArray());
    }

    private static DispatchDetailModel ToDispatch(TaskDispatch dispatch)
    {
        return new DispatchDetailModel(
            dispatch.Id.ToString(),
            dispatch.DispatchType,
            dispatch.Destination,
            dispatch.DispatchStatus,
            dispatch.CommandId,
            dispatch.CorrelationId,
            dispatch.SentOnUtc,
            dispatch.AcknowledgedOnUtc,
            dispatch.FailedOnUtc,
            dispatch.FailureReason,
            FormatJson(dispatch.RequestPayload),
            FormatJson(dispatch.Metadata));
    }

    private static TransitionDetailModel ToTransition(ExecutionTransition transition)
    {
        return new TransitionDetailModel(
            transition.Id.ToString(),
            transition.TransitionType,
            transition.FromStatus,
            transition.ToStatus,
            transition.OccurredOnUtc,
            transition.StageExecutionId?.ToString(),
            transition.TaskExecutionId?.ToString(),
            transition.TaskExecutionAttemptId?.ToString(),
            transition.Message,
            transition.ProducedBy,
            FormatJson(transition.Payload));
    }

    private static string FormatJson(JsonNode node)
        => node?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) ?? string.Empty;

    private static string FormatJson(Dictionary<string, JsonNode> values)
        => values is null || values.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(values.ToDictionary(x => x.Key, x => x.Value), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    private static Id ParseId(string value) => new(Ulid.Parse(value));
}

public sealed record InstanceRowModel(
    string Id,
    string OrchestrationDefinitionKey,
    string CorrelationId,
    string ExecutionKey,
    string Status,
    string StatusClass,
    string CurrentStageKey,
    string CurrentTaskKey,
    DateTime StartedOnUtc,
    DateTime LastUpdatedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    string ErrorSummary);

public sealed record TrafficPointModel(DateTime BucketUtc, int Count);

public sealed record InstanceDetailModel(
    InstanceRowModel Instance,
    IReadOnlyCollection<StageDetailModel> Stages,
    IReadOnlyCollection<TaskDetailModel> Tasks,
    IReadOnlyCollection<TransitionDetailModel> Transitions,
    string SnapshotPayload,
    string Metadata);

public sealed record StageDetailModel(
    string Id,
    string StageKey,
    int Order,
    string Status,
    string StatusClass,
    DateTime? StartedOnUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    string ErrorSummary,
    string Metadata,
    IReadOnlyCollection<TaskDetailModel> Tasks);

public sealed record TaskDetailModel(
    string Id,
    string StageExecutionId,
    string TaskKey,
    string TaskKind,
    string ExecutionMode,
    string Status,
    bool AwaitResponse,
    string CorrelationId,
    DateTime? StartedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    DateTime? TimedOutOnUtc,
    int LastAttemptNumber,
    string OutputVariablesPayload,
    string Metadata,
    IReadOnlyCollection<TaskAttemptDetailModel> Attempts);

public sealed record TaskAttemptDetailModel(
    string Id,
    int AttemptNumber,
    string Status,
    DateTime? StartedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    DateTime? TimedOutOnUtc,
    string RequestPayload,
    string ResponsePayload,
    string ErrorCode,
    string ErrorMessage,
    DispatchDetailModel Dispatch,
    string Metadata);

public sealed record DispatchDetailModel(
    string Id,
    string DispatchType,
    string Destination,
    string DispatchStatus,
    string CommandId,
    string CorrelationId,
    DateTime? SentOnUtc,
    DateTime? AcknowledgedOnUtc,
    DateTime? FailedOnUtc,
    string FailureReason,
    string RequestPayload,
    string Metadata);

public sealed record TransitionDetailModel(
    string Id,
    string TransitionType,
    string FromStatus,
    string ToStatus,
    DateTime OccurredOnUtc,
    string StageExecutionId,
    string TaskExecutionId,
    string TaskExecutionAttemptId,
    string Message,
    string ProducedBy,
    string Payload);
