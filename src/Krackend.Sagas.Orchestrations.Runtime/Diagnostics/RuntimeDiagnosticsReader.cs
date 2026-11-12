using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Reads runtime storage and builds diagnostics models for dashboards and trace views.
/// </summary>
public sealed class RuntimeDiagnosticsReader : IRuntimeDiagnosticsReader, IRuntimeSectionDiagnosticsReader, IRuntimeInstanceCatalogReader, IRuntimeInstanceStatusCountsReader, IRuntimeVisibleStageSummaryReader
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

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeDiagnosticsReader"/> class.
    /// </summary>
    public RuntimeDiagnosticsReader(
        IRuntimeArtifactRepository runtimeArtifactRepository,
        IOrchestrationInstanceRepository instanceRepository,
        IStageExecutionRepository stageRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository,
        ITaskDispatchRepository dispatchRepository,
        ICompensationExecutionRepository compensationRepository,
        IExecutionTransitionRepository transitionRepository)
    {
        _runtimeArtifactRepository = runtimeArtifactRepository;
        _instanceRepository = instanceRepository;
        _stageRepository = stageRepository;
        _taskRepository = taskRepository;
        _attemptRepository = attemptRepository;
        _dispatchRepository = dispatchRepository;
        _compensationRepository = compensationRepository;
        _transitionRepository = transitionRepository;
    }

    /// <inheritdoc />
    public async Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default)
    {
        var summary = await BuildRuntimeSummary(cancellationToken);
        var instances = _instanceRepository is IRuntimeInstanceRecentSummaryReader summaries
            ? await summaries.GetRecentSummaries(1000, cancellationToken)
            : await _instanceRepository.GetRecent(1000, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var traffic = await _transitionRepository.GetTraffic(nowUtc.AddHours(-1), cancellationToken);
        var hourlyTraffic = await _transitionRepository.GetTraffic(nowUtc.AddHours(-24), cancellationToken);

        return new RuntimeDashboardSnapshotModel(
            summary,
            instances.Select(instance => ToRow(instance)).ToArray(),
            traffic.Select(ToTraffic).ToArray(),
            hourlyTraffic.Select(ToTraffic).ToArray());
    }

    /// <inheritdoc />
    public async Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default)
    {
        var summary = await BuildRuntimeSummary(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var traffic = await _transitionRepository.GetTraffic(nowUtc.AddHours(-1), cancellationToken);
        var hourlyTraffic = await _transitionRepository.GetTraffic(nowUtc.AddHours(-24), cancellationToken);

        return new RuntimeDashboardSummaryModel(
            summary,
            traffic.Select(ToTraffic).ToArray(),
            hourlyTraffic.Select(ToTraffic).ToArray());
    }

    /// <inheritdoc />
    public async Task<RuntimeInstancePageModel> GetInstances(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
    {
        if (_instanceRepository is not IRuntimeInstanceCatalogRepository catalog)
            throw new NotSupportedException("The configured runtime storage does not support paged instance diagnostics.");

        var result = await catalog.GetPage(query, cancellationToken);
        return new RuntimeInstancePageModel(result.Items.Select(x => ToRow(x)).ToArray(),
            result.TotalCount, result.PageNumber, result.PageSize);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(
        RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
    {
        if (_instanceRepository is not IRuntimeInstanceStatusCountsRepository counts)
            throw new NotSupportedException("The configured runtime storage does not support scoped instance counts.");
        return counts.GetStatusCounts(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuntimeStageSummaryPage> GetVisibleStageSummaries(Id instanceId, int take, CancellationToken cancellationToken = default)
    {
        if (_stageRepository is not IRuntimeStageSummaryReader summaries)
            throw new NotSupportedException("The configured runtime storage does not support stage summaries.");
        var page = await summaries.GetStageSummaries(instanceId, take, cancellationToken);
        var instance = await _instanceRepository.GetById(instanceId, cancellationToken);
        var artifact = DeserializeArtifact(await TryGetRuntimeArtifact(instance, cancellationToken));
        var disabled = (artifact?.StageDefinitions ?? []).Where(x => !x.IsEnabled).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new RuntimeStageSummaryPage(page.Items.Where(x => !disabled.Contains(x.StageKey)).ToArray(), page.HasMore);
    }

    /// <inheritdoc />
    public Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default)
        => GetDetailCore(instanceId, null, null, cancellationToken);

    /// <inheritdoc />
    public Task<InstanceDetailModel> GetDetailForSection(string instanceId, string section, CancellationToken cancellationToken = default, RuntimeDetailSelection selection = null)
        => GetDetailCore(instanceId, section ?? "execution", selection, cancellationToken);

    private async Task<InstanceDetailModel> GetDetailCore(string instanceId, string section, RuntimeDetailSelection selection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance id is required.", nameof(instanceId));

        var id = ParseId(instanceId);
        var instance = await _instanceRepository.GetById(id, cancellationToken);
        var stages = (await _stageRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.Order).ToArray();
        var tasks = (await _taskRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.StartedOnUtc).ToArray();
        var compensations = section is null || string.Equals(section, "compensations", StringComparison.OrdinalIgnoreCase) ? (await _compensationRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.StartedOnUtc).ToArray() : [];
        var transitions = section is null || string.Equals(section, "activity", StringComparison.OrdinalIgnoreCase) ? (await _transitionRepository.GetByInstanceId(id, cancellationToken)).OrderBy(x => x.OccurredOnUtc).ToArray() : [];
        var runtimeArtifact = await TryGetRuntimeArtifact(instance, cancellationToken);
        var artifact = DeserializeArtifact(runtimeArtifact);

        // Disabled design elements are not part of the visible execution, even
        // when older runtime versions persisted a skipped record for them.
        if (artifact?.StageDefinitions is not null)
        {
            var definitions = artifact.StageDefinitions.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
            var hiddenStages = stages.Where(x => definitions.TryGetValue(x.StageKey, out var definition) && !definition.IsEnabled)
                .Select(x => x.Id).ToHashSet();
            var stageKeys = stages.ToDictionary(x => x.Id, x => x.StageKey);
            var hiddenTasks = tasks.Where(x => hiddenStages.Contains(x.StageExecutionId)
                || (stageKeys.TryGetValue(x.StageExecutionId, out var key)
                    && definitions.TryGetValue(key, out var definition)
                    && definition.TaskDefinitions.Any(t => !t.IsEnabled && string.Equals(t.Key, x.TaskKey, StringComparison.OrdinalIgnoreCase))))
                .Select(x => x.Id).ToHashSet();
            stages = stages.Where(x => !hiddenStages.Contains(x.Id)).ToArray();
            tasks = tasks.Where(x => !hiddenTasks.Contains(x.Id)).ToArray();
            transitions = transitions.Where(x => (!x.StageExecutionId.HasValue || !hiddenStages.Contains(x.StageExecutionId.Value))
                && (!x.TaskExecutionId.HasValue || !hiddenTasks.Contains(x.TaskExecutionId.Value))).ToArray();
            compensations = compensations.Where(x => !hiddenTasks.Contains(x.SourceTaskExecutionId)).ToArray();
        }

        var stageById = stages.ToDictionary(x => x.Id.ToString(), x => x.StageKey);
        var taskById = tasks.ToDictionary(x => x.Id.ToString(), x => x.TaskKey);
        bool MatchesSelection(TaskExecution task)
        {
            if (selection is null) return false;
            var taskMatches = !string.IsNullOrWhiteSpace(selection.TaskExecutionId)
                ? task.Id.ToString() == selection.TaskExecutionId
                : string.Equals(task.TaskKey, selection.TaskKey, StringComparison.Ordinal);
            var stageMatches = !string.IsNullOrWhiteSpace(selection.StageExecutionId)
                ? task.StageExecutionId.ToString() == selection.StageExecutionId
                : string.Equals(stageById.GetValueOrDefault(task.StageExecutionId.ToString()), selection.StageKey, StringComparison.Ordinal);
            return taskMatches && stageMatches;
        }
        bool IncludeAttemptDetails(TaskExecution task, TaskExecutionAttempt attempt)
            => section is null || (string.Equals(section, "attempt", StringComparison.OrdinalIgnoreCase) && selection?.Attempt == attempt.AttemptNumber && MatchesSelection(task));
        bool IncludeTaskDetails(TaskExecution task)
            => section is null || (string.Equals(section, "task", StringComparison.OrdinalIgnoreCase) && MatchesSelection(task));
        var taskDetails = await BuildTaskDetails(instance, tasks, stageById, IncludeAttemptDetails, IncludeTaskDetails, section is null, cancellationToken);
        var stageDetails = BuildStageDetails(stages, taskDetails, artifact);
        var allTaskDetails = stageDetails.SelectMany(x => x.Tasks).ToArray();
        var transitionDetails = transitions.Select(x => ToTransition(x, stageById, taskById)).ToArray();
        var timeline = transitionDetails.Select(ToTimelineEntry).ToArray();

        return new InstanceDetailModel(
            ToRow(instance, InstanceArtifactInfo.From(runtimeArtifact, artifact)),
            stageDetails,
            allTaskDetails,
            transitionDetails,
            section is null || string.Equals(section, "data", StringComparison.OrdinalIgnoreCase) ? FormatJson(instance.SnapshotPayload) : string.Empty,
            FormatJson(instance.Metadata),
            compensations.Select(compensation => ToCompensation(compensation, taskById)).ToArray(),
            timeline.Where(x => x.TimelineKind == "Functional").ToArray(),
            timeline.Where(x => x.TimelineKind == "Technical").ToArray()) { ArtifactAvailable = artifact is not null };
    }

    /// <summary>
    /// Maps an execution status into the runtime UI status CSS class.
    /// </summary>
    /// <param name="status">Execution status.</param>
    /// <returns>Status CSS class.</returns>
    public static string StatusClass(string status)
    {
        return status switch
        {
            "Created" or "Pending" or "Skipped" or "Stopped" => "od-status-inactive",
            "Running" => "od-status-running",
            "Idle" or "Retrying" or "CompletedWithErrors" or "DeadLettered" => "od-status-warning",
            "Waiting" or "WaitingResponse" or "AwaitingResponse" or "Compensating" => "od-status-waiting",
            "Completed" or "Compensated" => "od-status-active",
            "Failed" or "TimedOut" or "Cancelled" or "Aborted" => "od-status-danger",
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
        catch (Exception exception) when (exception is not OperationCanceledException)
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

    private async Task<RuntimeSummaryModel> BuildRuntimeSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var minuteSinceUtc = now.AddMinutes(-1);
        var hourSinceUtc = now.AddHours(-1);
        var minute = await _instanceRepository.GetSummary(minuteSinceUtc, cancellationToken);
        var hour = await _instanceRepository.GetSummary(hourSinceUtc, cancellationToken);

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
        OrchestrationInstance instance,
        IReadOnlyCollection<TaskExecution> tasks,
        IReadOnlyDictionary<string, string> stageById,
        Func<TaskExecution, TaskExecutionAttempt, bool> includeAttemptDetails,
        Func<TaskExecution, bool> includeTaskDetails,
        bool includeAllPayloads,
        CancellationToken cancellationToken)
    {
        ILookup<Id, TaskExecutionAttempt> attemptsByTask = null;
        if (!includeAllPayloads && _attemptRepository is ITaskExecutionAttemptSummaryBatchReader summaryReader &&
            _attemptRepository is ITaskExecutionAttemptBatchReader detailReader)
        {
            var summaries = (await summaryReader.GetSummariesByTaskExecutionIds(tasks.Select(x => x.Id).ToArray(), cancellationToken)).ToArray();
            var taskLookup = tasks.ToDictionary(x => x.Id);
            var selected = summaries.Where(x => taskLookup.TryGetValue(x.TaskExecutionId, out var task) && includeAttemptDetails(task, x)).ToArray();
            if (selected.Length > 0)
            {
                IReadOnlyCollection<TaskExecutionAttempt> full;
                try
                {
                    full = selected.Length == 1
                        ? [await _attemptRepository.GetById(selected[0].Id, cancellationToken)]
                        : await detailReader.GetByTaskExecutionIds(selected.Select(x => x.TaskExecutionId).Distinct().ToArray(), cancellationToken);
                }
                catch (KeyNotFoundException) { full = []; }
                var fullById = full.ToDictionary(x => x.Id);
                for (var i = 0; i < summaries.Length; i++)
                    if (fullById.TryGetValue(summaries[i].Id, out var value) && taskLookup.TryGetValue(value.TaskExecutionId, out var task) && includeAttemptDetails(task, value))
                        summaries[i] = value;
            }
            attemptsByTask = summaries.ToLookup(x => x.TaskExecutionId);
        }
        else if (_attemptRepository is ITaskExecutionAttemptBatchReader attemptBatchReader)
        {
            var attempts = await attemptBatchReader.GetByTaskExecutionIds(tasks.Select(x => x.Id).ToArray(), cancellationToken);
            attemptsByTask = attempts.ToLookup(x => x.TaskExecutionId);
        }

        Dictionary<Id, TaskDispatch> dispatchesById = null;
        Dictionary<Id, TaskDispatch> dispatchesByAttempt = null;
        if (attemptsByTask is not null && _dispatchRepository is ITaskDispatchBatchReader dispatchBatchReader)
        {
            var tasksById = tasks.ToDictionary(x => x.Id);
            var attempts = attemptsByTask.SelectMany(x => x).Where(x => tasksById.TryGetValue(x.TaskExecutionId, out var task) && includeAttemptDetails(task, x)).ToArray();
            var ids = attempts.Where(x => x.DispatchId.HasValue).Select(x => x.DispatchId.Value).ToArray();
            var attemptIds = attempts.Where(x => !x.DispatchId.HasValue).Select(x => x.Id).ToArray();
            IReadOnlyCollection<TaskDispatch> byId = ids.Length == 0 ? [] : await dispatchBatchReader.GetByIds(ids, cancellationToken);
            IReadOnlyCollection<TaskDispatch> byAttempt = attemptIds.Length == 0 ? [] : await dispatchBatchReader.GetByAttemptIds(attemptIds, cancellationToken);
            dispatchesById = byId.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
            dispatchesByAttempt = byAttempt.GroupBy(x => x.TaskExecutionAttemptId).ToDictionary(x => x.Key, x => x.First());
        }

        var taskDetails = new List<TaskDetailModel>();
        foreach (var task in tasks)
        {
            var attempts = (attemptsByTask is null
                ? await _attemptRepository.GetByTaskExecutionId(task.Id, cancellationToken)
                : attemptsByTask[task.Id]).OrderBy(x => x.AttemptNumber).ToArray();
            var attemptDetails = new List<TaskAttemptDetailModel>();
            foreach (var attempt in attempts)
            {
                TaskDispatch dispatch = null;
                var includeDetails = includeAttemptDetails(task, attempt);
                if (includeDetails && dispatchesById is null)
                {
                    try
                    {
                        dispatch = attempt.DispatchId is null
                            ? await _dispatchRepository.GetByAttemptId(attempt.Id, cancellationToken)
                            : await _dispatchRepository.TryGetById(attempt.DispatchId.Value, cancellationToken);
                    }
                    catch (KeyNotFoundException) { dispatch = null; }
                }
                else if (includeDetails && attempt.DispatchId is { } dispatchId)
                {
                    dispatchesById.TryGetValue(dispatchId, out dispatch);
                }
                else if (includeDetails) { dispatchesByAttempt.TryGetValue(attempt.Id, out dispatch); }

                attemptDetails.Add(new TaskAttemptDetailModel(
                    attempt.Id.ToString(),
                    attempt.AttemptNumber,
                    attempt.Status.ToString(),
                    attempt.StartedOnUtc,
                    attempt.WaitingSinceUtc,
                    attempt.CompletedOnUtc,
                    attempt.FailedOnUtc,
                    attempt.TimedOutOnUtc,
                    includeDetails ? FormatJson(attempt.RequestPayload) : string.Empty,
                    includeDetails ? FormatJson(attempt.ResponsePayload) : string.Empty,
                    attempt.ErrorCode,
                    attempt.ErrorMessage,
                    dispatch is null ? null : ToDispatch(dispatch, instance, task, attempt, stageById),
                    includeDetails ? FormatJson(attempt.Metadata) : string.Empty,
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
                includeTaskDetails(task) ? FormatJson(task.OutputVariablesPayload) : string.Empty,
                includeTaskDetails(task) ? FormatJson(task.Metadata) : string.Empty,
                attemptDetails,
                instance.SagaId,
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
        var tasksByStage = taskDetails.ToLookup(x => x.StageExecutionId, StringComparer.OrdinalIgnoreCase);
        var executedByKey = stages
            .GroupBy(x => x.StageKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(stage => stage.StartedOnUtc).First(), StringComparer.OrdinalIgnoreCase);

        foreach (var stageArtifact in artifact?.StageDefinitions?.OrderBy(x => x.Order) ?? Enumerable.Empty<StageArtifact>())
        {
            if (!stageArtifact.IsEnabled) continue;
            if (executedByKey.TryGetValue(stageArtifact.Key, out var stage))
            {
                var stageId = stage.Id.ToString();
                consumedStageIds.Add(stageId);
                var executedTasks = tasksByStage[stageId].ToArray();
                stageDetails.Add(ToStage(stage, MergeStageTasks(stageArtifact, stageId, executedTasks), stageArtifact));
                continue;
            }

            stageDetails.Add(ToPendingStage(stageArtifact));
        }

        var disabledStages = (artifact?.StageDefinitions ?? []).Where(x => !x.IsEnabled).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in stages.Where(x => !consumedStageIds.Contains(x.Id.ToString()) && !disabledStages.Contains(x.StageKey)).OrderBy(x => x.Order))
        {
            var stageId = stage.Id.ToString();
            stageDetails.Add(ToStage(stage, tasksByStage[stageId].ToArray(), null));
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
            if (!taskArtifact.IsEnabled) continue;
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

        var disabledTasks = stageArtifact.TaskDefinitions.Where(x => !x.IsEnabled).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var task in executedTasks.Where(x => !consumedTaskIds.Contains(x.Id) && !disabledTasks.Contains(x.TaskKey)))
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
            stage.TaskDefinitions.Where(x => x.IsEnabled).OrderBy(x => x.Order).Select(task => ToPendingTask(stageId, task)).ToArray(),
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
        => ToRow(instance, new InstanceArtifactInfo(orchestrationVersion));

    private static InstanceRowModel ToRow(OrchestrationInstance instance, InstanceArtifactInfo artifact)
    {
        artifact ??= InstanceArtifactInfo.Empty;
        var progress = RuntimeReactiveProgressCalculator.TryCalculate(instance, null);
        var status = PublicInstanceStatus(instance.Status);
        return new InstanceRowModel(
            instance.Id.ToString(),
            instance.OrchestrationDefinitionKey,
            instance.OrchestrationVersion ?? artifact.Version ?? string.Empty,
            instance.CorrelationId,
            instance.SagaId,
            instance.ExecutionKey,
            status,
            StatusClass(status),
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
            instance.RetryCount)
        {
            CompletedTaskCount = progress?.CompletedTaskCount,
            TotalTaskCount = progress?.TotalTaskCount,
            TraceId = RuntimeInstanceTraceId.Get(instance),
            LastExecutedStageKey = instance.LastExecutedStageKey,
            LastExecutedTaskKey = instance.LastExecutedTaskKey
        };
    }

    private static string PublicInstanceStatus(OrchestrationInstanceStatus status)
        => status switch
        {
            OrchestrationInstanceStatus.Created or OrchestrationInstanceStatus.Running
                or OrchestrationInstanceStatus.Compensating or OrchestrationInstanceStatus.Waiting => "Running",
            OrchestrationInstanceStatus.Idle => "Idle",
            OrchestrationInstanceStatus.Completed or OrchestrationInstanceStatus.Compensated => "Completed",
            OrchestrationInstanceStatus.DeadLettered => "DeadLettered",
            OrchestrationInstanceStatus.Failed or OrchestrationInstanceStatus.Stopped
                or OrchestrationInstanceStatus.Aborted or OrchestrationInstanceStatus.CompletedWithErrors => "Failed",
            _ => status.ToString()
        };

    private sealed record InstanceArtifactInfo(string Version)
    {
        public static readonly InstanceArtifactInfo Empty = new((string)null);

        public static InstanceArtifactInfo From(RuntimeOrchestrationArtifact runtimeArtifact, OrchestrationArtifact artifact)
        {
            return new InstanceArtifactInfo(runtimeArtifact?.Version.ToString());
        }
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

    private static DispatchDetailModel ToDispatch(
        TaskDispatch dispatch,
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        IReadOnlyDictionary<string, string> stageById)
    {
        var command = dispatch.Metadata?.GetValueOrDefault("Runtime.Delivery.Command") as JsonObject;
        var message = command?["messageMetadata"];
        var propagation = command?["propagationMetadata"]?["items"];
        var hasSnapshot = command is not null && (message is not null || propagation is not null);
        if (!hasSnapshot)
        {
            message = BuildDispatchMetadata(dispatch, instance, task, attempt, stageById)
                .GetValueOrDefault(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey);
            propagation = JsonSerializer.SerializeToNode(LoadPropagationMetadata(instance).Items, ArtifactJsonOptions);
        }
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
            FormatJson(BuildDispatchMetadata(dispatch, instance, task, attempt, stageById)),
            dispatch.TaskExecutionAttemptId.ToString())
        {
            MessageMetadata = FormatJson(message),
            PropagationMetadata = FormatJson(propagation),
            MessageMetadataSource = hasSnapshot ? "Persisted" : "Reconstructed"
        };
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

    private static Dictionary<string, JsonNode> BuildDispatchMetadata(
        TaskDispatch dispatch,
        OrchestrationInstance instance,
        TaskExecution task,
        TaskExecutionAttempt attempt,
        IReadOnlyDictionary<string, string> stageById)
    {
        if (dispatch.Metadata is { Count: > 0 })
        {
            return dispatch.Metadata;
        }

        var messageMetadata = new OrchestrationMessageMetadata
        {
            SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
            OrchestrationInstanceId = instance.Id.ToString(),
            CurrentStage = stageById.GetValueOrDefault(task.StageExecutionId.ToString()),
            CurrentTasks = [task.TaskKey],
            CorrelationId = FirstNonEmpty(dispatch.CorrelationId, task.CorrelationId, instance.CorrelationId),
            TaskExecutionId = task.Id.ToString(),
            DispatchId = dispatch.Id.ToString(),
            Attempt = attempt.AttemptNumber
        };

        return RemoteCommandMetadataBuilder.Build(
            messageMetadata,
            LoadPropagationMetadata(instance));
    }

    private static OrchestrationPropagationMetadata LoadPropagationMetadata(OrchestrationInstance instance)
    {
        if (instance?.Metadata is null ||
            !instance.Metadata.TryGetValue(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, out var node) ||
            node is null)
        {
            return new OrchestrationPropagationMetadata();
        }

        return TryDeserializePropagationEnvelope(node)
            ?? TryDeserializePropagationItems(node)
            ?? new OrchestrationPropagationMetadata();
    }

    private static OrchestrationPropagationMetadata TryDeserializePropagationEnvelope(JsonNode node)
    {
        try
        {
            var metadata = node.Deserialize<OrchestrationPropagationMetadata>(ArtifactJsonOptions);
            return metadata?.Items is null ? null : metadata.Clone();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static OrchestrationPropagationMetadata TryDeserializePropagationItems(JsonNode node)
    {
        try
        {
            var items = node.Deserialize<Dictionary<string, JsonNode>>(ArtifactJsonOptions);
            if (items is null)
            {
                return null;
            }

            var metadata = new OrchestrationPropagationMetadata();
            foreach (var item in items)
            {
                metadata.Items[item.Key] = item.Value?.DeepClone();
            }

            return metadata;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static Id ParseId(string value) => new(Ulid.Parse(value));
}
