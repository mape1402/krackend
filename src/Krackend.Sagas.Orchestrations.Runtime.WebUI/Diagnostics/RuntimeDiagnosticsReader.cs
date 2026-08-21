using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

public sealed class RuntimeDiagnosticsReader : IRuntimeDiagnosticsReader
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRuntimeArtifactRepository _runtimeArtifactRepository;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IStageExecutionRepository _stageRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly ICompensationExecutionRepository _compensationRepository;
    private readonly IExecutionTransitionRepository _transitionRepository;
    private readonly OrchestratorRuntimeWebUIOptions _options;

    public RuntimeDiagnosticsReader(
        IRuntimeArtifactRepository runtimeArtifactRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        ICompensationExecutionRepository compensationRepository,
        IExecutionTransitionRepository transitionRepository,
        IOptions<OrchestratorRuntimeWebUIOptions> options)
    {
        _runtimeArtifactRepository = runtimeArtifactRepository;
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _dispatchRepository = dispatchRepository;
        _compensationRepository = compensationRepository;
        _transitionRepository = transitionRepository;
        _options = options.Value;
    }

    public async Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default)
    {
        var summary = await BuildRuntimeSummary(cancellationToken);
        var environmentKey = GetEnvironmentKey();
        var instances = await _instanceRepository.GetRecent(environmentKey, 1000, cancellationToken);
        var artifactVersions = await BuildArtifactVersionMap(environmentKey, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var traffic = await _transitionRepository.GetTraffic(environmentKey, nowUtc.AddHours(-1), cancellationToken);
        var hourlyTraffic = await _transitionRepository.GetTraffic(environmentKey, nowUtc.AddHours(-24), cancellationToken);

        return new RuntimeDashboardSnapshotModel(
            summary,
            instances.Select(instance => ToRow(instance, artifactVersions.GetValueOrDefault(instance.RuntimeOrchestrationArtifactId))).ToArray(),
            traffic.Select(ToTraffic).ToArray(),
            hourlyTraffic.Select(ToTraffic).ToArray());
    }

    public async Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default)
    {
        var summary = await BuildRuntimeSummary(cancellationToken);
        var environmentKey = GetEnvironmentKey();
        var nowUtc = DateTime.UtcNow;
        var traffic = await _transitionRepository.GetTraffic(environmentKey, nowUtc.AddHours(-1), cancellationToken);
        var hourlyTraffic = await _transitionRepository.GetTraffic(environmentKey, nowUtc.AddHours(-24), cancellationToken);

        return new RuntimeDashboardSummaryModel(
            summary,
            traffic.Select(ToTraffic).ToArray(),
            hourlyTraffic.Select(ToTraffic).ToArray());
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
        var runtimeArtifact = await TryGetRuntimeArtifact(instance, cancellationToken);
        var artifact = DeserializeArtifact(runtimeArtifact);

        var taskDetails = await BuildTaskDetails(tasks, cancellationToken);
        var stageDetails = BuildStageDetails(stages, taskDetails, artifact);
        var allTaskDetails = stageDetails.SelectMany(x => x.Tasks).ToArray();
        var stageById = stages.ToDictionary(x => x.Id.ToString(), x => x.StageKey);
        var taskById = tasks.ToDictionary(x => x.Id.ToString(), x => x.TaskKey);
        var transitionDetails = transitions.Select(x => ToTransition(x, stageById, taskById)).ToArray();
        var timeline = transitionDetails.Select(ToTimelineEntry).ToArray();

        return new InstanceDetailModel(
            ToRow(instance, runtimeArtifact?.Version.ToString()),
            stageDetails,
            allTaskDetails,
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
            "Created" or "Pending" or "Skipped" or "Stopped" => "od-status-inactive",
            "Running" => "od-status-running",
            "Retrying" or "CompletedWithErrors" => "od-status-warning",
            "Waiting" or "WaitingResponse" or "Compensating" => "od-status-waiting",
            "Completed" or "Compensated" => "od-status-active",
            "Failed" or "TimedOut" or "Cancelled" => "od-status-danger",
            _ => "od-status-inactive"
        };
    }

    private async Task<RuntimeOrchestrationArtifact> TryGetRuntimeArtifact(
        OrchestrationInstance instance,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _runtimeArtifactRepository.GetById(instance.RuntimeOrchestrationArtifactId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static OrchestrationArtifact DeserializeArtifact(RuntimeOrchestrationArtifact runtimeArtifact)
    {
        if (runtimeArtifact is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OrchestrationArtifact>(
                runtimeArtifact.ArtifactPayload.ToJsonString(),
                ArtifactJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<Id, string>> BuildArtifactVersionMap(
        string environmentKey,
        CancellationToken cancellationToken)
    {
        var artifacts = await _runtimeArtifactRepository.GetAll(environmentKey, cancellationToken);
        return artifacts.ToDictionary(x => x.Id, x => x.Version.ToString());
    }

    private async Task<RuntimeSummaryModel> BuildRuntimeSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var minuteSinceUtc = now.AddMinutes(-1);
        var hourSinceUtc = now.AddHours(-1);
        var environmentKey = GetEnvironmentKey();
        var minute = await _instanceRepository.GetSummary(environmentKey, minuteSinceUtc, cancellationToken);
        var hour = await _instanceRepository.GetSummary(environmentKey, hourSinceUtc, cancellationToken);

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

    private string GetEnvironmentKey()
        => string.IsNullOrWhiteSpace(_options.EnvironmentKey) ? "local" : _options.EnvironmentKey.Trim();

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

            var errorSummary = attemptDetails
                .LastOrDefault(x => !string.IsNullOrWhiteSpace(x.ErrorMessage) || !string.IsNullOrWhiteSpace(x.ErrorCode));

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
                task.ExecutionConditionResult,
                true,
                null,
                null,
                0,
                errorSummary?.ErrorMessage ?? errorSummary?.ErrorCode));
        }

        return taskDetails;
    }

    private static IReadOnlyCollection<StageDetailModel> BuildStageDetails(
        IReadOnlyCollection<StageExecution> stages,
        IReadOnlyCollection<TaskDetailModel> taskDetails,
        OrchestrationArtifact artifact)
    {
        var stageDetails = new List<StageDetailModel>();
        var consumedStageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var executedByKey = stages
            .GroupBy(x => x.StageKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(stage => stage.StartedOnUtc).First(), StringComparer.OrdinalIgnoreCase);

        foreach (var stageArtifact in artifact?.StageDefinitions?.OrderBy(x => x.Order) ?? Enumerable.Empty<StageArtifact>())
        {
            if (executedByKey.TryGetValue(stageArtifact.Key, out var stage))
            {
                var stageId = stage.Id.ToString();
                consumedStageIds.Add(stageId);
                var executedTasks = taskDetails.Where(x => x.StageExecutionId == stageId).ToArray();
                stageDetails.Add(ToStage(stage, MergeStageTasks(stageArtifact, stageId, executedTasks), stageArtifact));
                continue;
            }

            stageDetails.Add(ToPendingStage(stageArtifact));
        }

        foreach (var stage in stages.Where(x => !consumedStageIds.Contains(x.Id.ToString())).OrderBy(x => x.Order))
        {
            var stageId = stage.Id.ToString();
            stageDetails.Add(ToStage(stage, taskDetails.Where(x => x.StageExecutionId == stageId).ToArray(), null));
        }

        return stageDetails.OrderBy(x => x.Order).ToArray();
    }

    private static IReadOnlyCollection<TaskDetailModel> MergeStageTasks(
        StageArtifact stageArtifact,
        string stageExecutionId,
        IReadOnlyCollection<TaskDetailModel> executedTasks)
    {
        var merged = new List<TaskDetailModel>();
        var consumedTaskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var executedByKey = executedTasks
            .GroupBy(x => x.TaskKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(task => task.StartedOnUtc).First(), StringComparer.OrdinalIgnoreCase);

        foreach (var taskArtifact in stageArtifact.TaskDefinitions.OrderBy(x => x.Order))
        {
            if (executedByKey.TryGetValue(taskArtifact.Key, out var executedTask))
            {
                consumedTaskIds.Add(executedTask.Id);
                merged.Add(executedTask with
                {
                    ConfiguredName = taskArtifact.Name,
                    ConfiguredNotes = taskArtifact.Notes,
                    Order = taskArtifact.Order
                });
                continue;
            }

            merged.Add(ToPendingTask(stageExecutionId, taskArtifact));
        }

        foreach (var task in executedTasks.Where(x => !consumedTaskIds.Contains(x.Id)))
        {
            merged.Add(task);
        }

        return merged.OrderBy(x => x.Order == 0 ? int.MaxValue : x.Order).ThenBy(x => x.StartedOnUtc).ToArray();
    }

    private static StageDetailModel ToPendingStage(StageArtifact stage)
    {
        var stageId = $"configured-stage:{stage.Key}";
        return new StageDetailModel(
            stageId,
            stage.Key,
            stage.Order,
            nameof(StageExecutionStatus.Pending),
            StatusClass(nameof(StageExecutionStatus.Pending)),
            null,
            null,
            null,
            null,
            FormatJson(BuildStageArtifactMetadata(stage)),
            stage.TaskDefinitions.OrderBy(x => x.Order).Select(task => ToPendingTask(stageId, task)).ToArray(),
            false,
            null,
            null,
            stage.ParallelGroups?.Count ?? 0,
            false,
            stage.Name,
            stage.Description);
    }

    private static TaskDetailModel ToPendingTask(string stageExecutionId, TaskArtifact task)
        => new(
            $"configured-task:{stageExecutionId}:{task.Key}",
            stageExecutionId,
            task.Key,
            task.Kind.ToString(),
            task.ExecutionMode.ToString(),
            nameof(TaskExecutionStatus.Pending),
            task.DispatchType != TaskDispatchType.FireAndForget,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            string.Empty,
            FormatJson(BuildTaskArtifactMetadata(task)),
            Array.Empty<TaskAttemptDetailModel>(),
            null,
            task.OnErrorPolicy.ToString(),
            task.ParallelGroupId?.ToString(),
            false,
            null,
            null,
            false,
            task.Name,
            task.Notes,
            task.Order);

    private static InstanceRowModel ToRow(OrchestrationInstance instance, string orchestrationVersion = null)
    {
        return new InstanceRowModel(
            instance.Id.ToString(),
            instance.OrchestrationDefinitionKey,
            orchestrationVersion ?? string.Empty,
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

    private static StageDetailModel ToStage(
        StageExecution stage,
        IReadOnlyCollection<TaskDetailModel> tasks,
        StageArtifact stageArtifact)
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
            stage.ParallelGroupCount,
            true,
            stageArtifact?.Name,
            stageArtifact?.Description);
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

    private static JsonNode BuildStageArtifactMetadata(StageArtifact stage)
        => JsonSerializer.SerializeToNode(new
        {
            Id = stage.Id.ToString(),
            stage.Name,
            stage.Description,
            stage.Order,
            TaskCount = stage.TaskDefinitions?.Count ?? 0,
            ParallelGroupCount = stage.ParallelGroups?.Count ?? 0,
            BranchRuleCount = stage.BranchRules?.Count ?? 0,
            HasExecutionCondition = stage.ExecutionCondition is not null
        }, IndentedJsonOptions);

    private static JsonNode BuildTaskArtifactMetadata(TaskArtifact task)
        => JsonSerializer.SerializeToNode(new
        {
            Id = task.Id.ToString(),
            task.Name,
            task.Notes,
            Kind = task.Kind.ToString(),
            ExecutionMode = task.ExecutionMode.ToString(),
            DispatchType = task.DispatchType.ToString(),
            OnErrorPolicy = task.OnErrorPolicy.ToString(),
            task.IsEnabled,
            ParallelGroupId = task.ParallelGroupId?.ToString(),
            AwaitResponse = task.DispatchType != TaskDispatchType.FireAndForget,
            HasExecutionCondition = task.ExecutionCondition is not null,
            HasTransformation = task.Transformation is not null,
            HasRetryPolicy = task.RetryPolicy is not null,
            HasTimeoutPolicy = task.TimeoutPolicy is not null,
            HasCompensation = task.Compensation is not null
        }, IndentedJsonOptions);

    private static TrafficPointModel ToTraffic(RuntimeTrafficPoint point)
        => new(point.BucketUtc, point.Active, point.Started, point.Completed, point.Failed);

    private static string FormatJson(JsonNode node)
        => node?.ToJsonString(IndentedJsonOptions) ?? string.Empty;

    private static string FormatJson(Dictionary<string, JsonNode> values)
        => values is null || values.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(values.ToDictionary(x => x.Key, x => x.Value), IndentedJsonOptions);

    private static Id ParseId(string value) => new(Ulid.Parse(value));
}
