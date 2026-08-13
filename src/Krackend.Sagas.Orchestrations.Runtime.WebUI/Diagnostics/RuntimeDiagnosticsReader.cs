using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

public sealed class RuntimeDiagnosticsReader : IRuntimeDiagnosticsReader
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly RuntimeEnvironmentDescriptor _runtimeEnvironment;

    public RuntimeDiagnosticsReader(
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        ICompensationExecutionRepository compensationRepository,
        IExecutionTransitionRepository transitionRepository,
        RuntimeEnvironmentDescriptor runtimeEnvironment)
    {
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _dispatchRepository = dispatchRepository;
        _compensationRepository = compensationRepository;
        _transitionRepository = transitionRepository;
        _runtimeEnvironment = runtimeEnvironment;
    }

    public async Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default)
    {
        var summary = await BuildRuntimeSummary(cancellationToken);
        var instances = await _instanceRepository.GetRecent(_runtimeEnvironment.EnvironmentKey, 1000, cancellationToken);
        var traffic = await _transitionRepository.GetTraffic(_runtimeEnvironment.EnvironmentKey, DateTime.UtcNow.AddHours(-1), cancellationToken);

        return new RuntimeDashboardSnapshotModel(
            summary,
            instances.Select(ToRow).ToArray(),
            traffic.Select(ToTraffic).ToArray());
    }

    public async Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default)
    {
        var summary = await BuildRuntimeSummary(cancellationToken);
        var traffic = await _transitionRepository.GetTraffic(_runtimeEnvironment.EnvironmentKey, DateTime.UtcNow.AddHours(-1), cancellationToken);

        return new RuntimeDashboardSummaryModel(
            summary,
            traffic.Select(ToTraffic).ToArray());
    }

    public async Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance id is required.", nameof(instanceId));

        var id = ParseId(instanceId);
        var instance = await _instanceRepository.GetById(id, cancellationToken);
        var stages = (await _stageRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.Order).ToArray();
        var tasks = (await _taskRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.StartedOnUtc).ToArray();
        var compensations = (await _compensationRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.StartedOnUtc).ToArray();
        var transitions = (await _transitionRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.OccurredOnUtc).ToArray();

        var taskDetails = await BuildTaskDetails(tasks, cancellationToken);
        var stageById = stages.ToDictionary(x => x.Id.ToString(), x => x.StageKey);
        var taskById = tasks.ToDictionary(x => x.Id.ToString(), x => x.TaskKey);
        var transitionDetails = transitions.Select(x => ToTransition(x, stageById, taskById)).ToArray();
        var timeline = transitionDetails.Select(ToTimelineEntry).ToArray();

        return new InstanceDetailModel(
            ToRow(instance),
            stages.Select(stage => ToStage(stage, taskDetails)).ToArray(),
            taskDetails,
            transitionDetails,
            FormatJson(instance.SnapshotPayload),
            FormatJson(instance.Metadata),
            compensations.Select(compensation => ToCompensation(compensation, taskById)).ToArray(),
            timeline.Where(x => x.TimelineKind == "Functional").ToArray(),
            timeline.Where(x => x.TimelineKind == "Technical").ToArray());
    }

    public static string StatusClass(string status)
    {
        return status switch
        {
            nameof(OrchestrationInstanceStatus.Running) => "od-status-running",
            nameof(OrchestrationInstanceStatus.Waiting) => "od-status-waiting",
            nameof(OrchestrationInstanceStatus.Completed) => "od-status-active",
            nameof(OrchestrationInstanceStatus.Failed) => "od-status-danger",
            nameof(OrchestrationInstanceStatus.Compensating) => "od-status-waiting",
            nameof(OrchestrationInstanceStatus.Compensated) => "od-status-active",
            _ => "od-status-inactive"
        };
    }

    private async Task<RuntimeSummaryModel> BuildRuntimeSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var minuteSinceUtc = now.AddMinutes(-1);
        var hourSinceUtc = now.AddHours(-1);
        var minute = await _instanceRepository.GetSummary(_runtimeEnvironment.EnvironmentKey, minuteSinceUtc, cancellationToken);
        var hour = await _instanceRepository.GetSummary(_runtimeEnvironment.EnvironmentKey, hourSinceUtc, cancellationToken);

        return new RuntimeSummaryModel(
            minute.Active,
            minute.Waiting,
            minute.CompletedRecent,
            minute.FailedRecent,
            hour.CompletedRecent,
            hour.FailedRecent,
            minuteSinceUtc,
            hourSinceUtc);
    }

    private async Task<IReadOnlyCollection<TaskDetailModel>> BuildTaskDetails(
        IReadOnlyCollection<TaskExecution> tasks,
        CancellationToken cancellationToken)
    {
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
                    FormatJson(attempt.Metadata),
                    attempt.TaskExecutionId.ToString(),
                    attempt.DispatchId?.ToString()));
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
                attemptDetails,
                task.OrchestrationInstanceId.ToString(),
                task.OnErrorPolicy.ToString(),
                task.ParallelGroupId?.ToString(),
                task.WasSkipped,
                task.SkipReason,
                task.ExecutionConditionResult));
        }

        return taskDetails;
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
            instance.ErrorSummary,
            instance.CurrentParallelGroupKey,
            instance.FinalOutcome,
            instance.StoppedOnUtc,
            instance.CompensationStartedOnUtc,
            instance.CompensatedOnUtc,
            instance.RetryCount);
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
            tasks.Where(x => x.StageExecutionId == stage.Id.ToString()).ToArray(),
            stage.WasSkipped,
            stage.SkipReason,
            stage.ExecutionConditionResult,
            stage.ParallelGroupCount);
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
            FormatJson(dispatch.Metadata),
            dispatch.TaskExecutionAttemptId.ToString());
    }

    private static CompensationDetailModel ToCompensation(
        CompensationExecution compensation,
        IReadOnlyDictionary<string, string> taskById)
    {
        var sourceTaskId = compensation.SourceTaskExecutionId.ToString();
        return new CompensationDetailModel(
            compensation.Id.ToString(),
            sourceTaskId,
            taskById.GetValueOrDefault(sourceTaskId),
            compensation.CompensationTaskKey,
            compensation.Status,
            compensation.StartedOnUtc,
            compensation.CompletedOnUtc,
            compensation.FailedOnUtc,
            FormatJson(compensation.RequestPayload),
            FormatJson(compensation.ResponsePayload),
            compensation.ErrorMessage,
            FormatJson(compensation.Metadata));
    }

    private static TransitionDetailModel ToTransition(
        ExecutionTransition transition,
        IReadOnlyDictionary<string, string> stageById,
        IReadOnlyDictionary<string, string> taskById)
    {
        var stageExecutionId = transition.StageExecutionId?.ToString();
        var taskExecutionId = transition.TaskExecutionId?.ToString();
        return new TransitionDetailModel(
            transition.Id.ToString(),
            transition.TransitionType,
            transition.FromStatus,
            transition.ToStatus,
            transition.OccurredOnUtc,
            stageExecutionId,
            taskExecutionId,
            transition.TaskExecutionAttemptId?.ToString(),
            transition.Message,
            transition.ProducedBy,
            FormatJson(transition.Payload),
            stageExecutionId is null ? null : stageById.GetValueOrDefault(stageExecutionId),
            taskExecutionId is null ? null : taskById.GetValueOrDefault(taskExecutionId));
    }

    private static RuntimeTimelineEntryModel ToTimelineEntry(TransitionDetailModel transition)
    {
        return new RuntimeTimelineEntryModel(
            transition.Id,
            IsTechnicalTransition(transition.TransitionType) ? "Technical" : "Functional",
            transition.TransitionType,
            transition.FromStatus,
            transition.ToStatus,
            transition.OccurredOnUtc,
            transition.StageExecutionId,
            transition.StageKey,
            transition.TaskExecutionId,
            transition.TaskKey,
            transition.TaskExecutionAttemptId,
            transition.Message,
            transition.ProducedBy,
            transition.Payload);
    }

    private static bool IsTechnicalTransition(string transitionType)
        => transitionType?.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) == true
            || transitionType?.Contains("Retry", StringComparison.OrdinalIgnoreCase) == true
            || transitionType?.Contains("Timeout", StringComparison.OrdinalIgnoreCase) == true
            || transitionType?.Contains("Recovery", StringComparison.OrdinalIgnoreCase) == true
            || transitionType?.Contains("Reconciliation", StringComparison.OrdinalIgnoreCase) == true
            || transitionType?.Contains("TriggerPromoted", StringComparison.OrdinalIgnoreCase) == true;

    private static TrafficPointModel ToTraffic(RuntimeTrafficPoint point)
        => new(point.BucketUtc, point.Started, point.Completed, point.Failed);

    private static string FormatJson(JsonNode node)
        => node?.ToJsonString(IndentedJsonOptions) ?? string.Empty;

    private static string FormatJson(Dictionary<string, JsonNode> values)
        => values is null || values.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(values.ToDictionary(x => x.Key, x => x.Value), IndentedJsonOptions);

    private static Id ParseId(string value) => new(Ulid.Parse(value));
}
