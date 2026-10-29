using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;

internal sealed class ExecutionTransitionRepository : RuntimeRepositoryBase, IExecutionTransitionRepository
{
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;

    public ExecutionTransitionRepository(
        RuntimeDbContext dbContext,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeReactiveEventPublisher reactiveEventPublisher)
        : base(dbContext, unitOfWork)
    {
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
        _unitOfWork = unitOfWork;
    }

    public async Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
    {
        DbContext.ExecutionTransitions.Add(transition);
        await SaveChanges(cancellationToken);
        var eventData = await BuildReactiveEvent(transition, cancellationToken);
        if (eventData is not null)
        {
            if (_unitOfWork is RuntimeStorageUnitOfWork unitOfWork)
                await unitOfWork.PublishWhenCommitted(eventData);
            else
                await _reactiveEventPublisher.Publish(eventData, cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.ExecutionTransitions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.OccurredOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(int take = 250, CancellationToken cancellationToken = default)
        => await (from transition in DbContext.ExecutionTransitions.AsNoTracking()
                  orderby transition.OccurredOnUtc descending
                  select transition)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var bucketSize = GetBucketSize(sinceUtc, nowUtc);
        var firstBucketUtc = AlignBucket(sinceUtc, bucketSize);
        var instances = await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.StartedOnUtc <= nowUtc)
            .Where(x => x.StartedOnUtc >= firstBucketUtc
                || x.CompletedOnUtc >= firstBucketUtc
                || x.FailedOnUtc >= firstBucketUtc
                || x.StoppedOnUtc >= firstBucketUtc
                || x.CompensatedOnUtc >= firstBucketUtc
                || x.Status == OrchestrationInstanceStatus.Created
                || x.Status == OrchestrationInstanceStatus.Running
                || x.Status == OrchestrationInstanceStatus.Idle
                || x.Status == OrchestrationInstanceStatus.Compensating)
            .ToArrayAsync(cancellationToken);

        return BuildTraffic(instances, firstBucketUtc, nowUtc, bucketSize);
    }

    private async Task<RuntimeReactiveEvent> BuildReactiveEvent(ExecutionTransition transition, CancellationToken cancellationToken)
    {
        var instance = DbContext.OrchestrationInstances.Local.FirstOrDefault(x => x.Id == transition.OrchestrationInstanceId)
            ?? await DbContext.OrchestrationInstances.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == transition.OrchestrationInstanceId, cancellationToken);

        if (instance is null)
        {
            return null;
        }

        // A reactive event is emitted in the same unit of work that changed the
        // saga. The normal path therefore already has its execution graph in
        // EF's local tracker. Do not turn each SignalR delta into three extra
        // database lookups: stage, task and artifact data are optional display
        // enrichment and an existing browser row retains them when omitted.
        var stageKey = GetStageKey(transition.StageExecutionId);
        var taskKey = GetTaskKey(transition.TaskExecutionId);
        var orchestrationVersion = GetOrchestrationVersion(instance.RuntimeOrchestrationArtifactId);
        return new RuntimeReactiveEvent
        {
            Id = transition.Id,
            Revision = instance.StatisticsRevision > 0
                ? instance.StatisticsRevision.ToString(CultureInfo.InvariantCulture)
                : transition.Id.ToString(),
            EventName = ResolveEventName(transition.TransitionType),
            TransitionType = transition.TransitionType,
            OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
            OrchestrationVersion = orchestrationVersion,
            OrchestrationInstanceId = transition.OrchestrationInstanceId,
            CorrelationId = instance.CorrelationId,
            SagaId = instance.SagaId,
            ExecutionKey = instance.ExecutionKey,
            StageExecutionId = transition.StageExecutionId,
            StageKey = stageKey,
            TaskExecutionId = transition.TaskExecutionId,
            TaskKey = taskKey,
            TaskExecutionAttemptId = transition.TaskExecutionAttemptId,
            FromStatus = transition.FromStatus,
            ToStatus = transition.ToStatus,
            InstanceStatus = instance.Status.ToString(),
            StartedOnUtc = instance.StartedOnUtc,
            LastUpdatedOnUtc = instance.LastUpdatedOnUtc,
            OccurredOnUtc = transition.OccurredOnUtc,
            Message = transition.Message,
            Payload = transition.Payload,
            ProducedBy = transition.ProducedBy
        };
    }

    private string GetStageKey(Id? stageExecutionId)
    {
        if (stageExecutionId is null)
        {
            return null;
        }

        var stage = DbContext.StageExecutions.Local.FirstOrDefault(x => x.Id == stageExecutionId.Value);

        return stage?.StageKey;
    }

    private string GetTaskKey(Id? taskExecutionId)
    {
        if (taskExecutionId is null)
        {
            return null;
        }

        var task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == taskExecutionId.Value);

        return task?.TaskKey;
    }

    private string GetOrchestrationVersion(Id runtimeOrchestrationArtifactId)
    {
        var artifact = DbContext.RuntimeOrchestrationArtifacts.Local.FirstOrDefault(x => x.Id == runtimeOrchestrationArtifactId);

        return artifact?.Version.ToString();
    }

    private static IReadOnlyCollection<RuntimeTrafficPoint> BuildTraffic(
        IReadOnlyCollection<OrchestrationInstance> instances,
        DateTime firstBucketUtc,
        DateTime nowUtc,
        TimeSpan bucketSize)
    {
        var buckets = new List<RuntimeTrafficPoint>();
        for (var bucketStart = firstBucketUtc; bucketStart <= nowUtc; bucketStart = bucketStart.Add(bucketSize))
        {
            var bucketEnd = bucketStart.Add(bucketSize);
            buckets.Add(new RuntimeTrafficPoint(
                bucketStart,
                instances.Count(instance => IsActiveInBucket(instance, bucketStart, bucketEnd)),
                instances.Count(instance => instance.StartedOnUtc >= bucketStart && instance.StartedOnUtc < bucketEnd),
                instances.Count(instance => instance.CompletedOnUtc >= bucketStart && instance.CompletedOnUtc < bucketEnd),
                instances.Count(instance => instance.FailedOnUtc >= bucketStart && instance.FailedOnUtc < bucketEnd)));
        }

        return buckets;
    }

    private static bool IsActiveInBucket(OrchestrationInstance instance, DateTime bucketStartUtc, DateTime bucketEndUtc)
    {
        var terminalOnUtc = GetTerminalOnUtc(instance);
        return instance.StartedOnUtc < bucketEndUtc
            && (terminalOnUtc is null || terminalOnUtc.Value >= bucketStartUtc);
    }

    private static DateTime? GetTerminalOnUtc(OrchestrationInstance instance)
    {
        var terminalTimes = new[]
        {
            instance.CompletedOnUtc,
            instance.FailedOnUtc,
            instance.StoppedOnUtc,
            instance.CompensatedOnUtc
        };

        return terminalTimes
            .Where(value => value is not null)
            .OrderBy(value => value)
            .FirstOrDefault();
    }

    private static TimeSpan GetBucketSize(DateTime sinceUtc, DateTime nowUtc)
        => nowUtc - sinceUtc > TimeSpan.FromHours(2)
            ? TimeSpan.FromHours(1)
            : TimeSpan.FromMinutes(1);

    private static DateTime AlignBucket(DateTime value, TimeSpan bucketSize)
        => bucketSize >= TimeSpan.FromHours(1)
            ? new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc)
            : new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);

    private static string ResolveEventName(string transitionType)
        => transitionType switch
        {
            "InstancePromoted" or "InstanceStarted" => RuntimeReactiveEventNames.OrchestrationStarted,
            "InstanceWaitingResponse" => RuntimeReactiveEventNames.OrchestrationWaiting,
            "InstanceCompensating" => RuntimeReactiveEventNames.OrchestrationCompensating,
            "InstanceCompleted" => RuntimeReactiveEventNames.OrchestrationCompleted,
            "InstanceCompensated" => RuntimeReactiveEventNames.OrchestrationCompensated,
            "InstanceFailed" => RuntimeReactiveEventNames.OrchestrationFailed,
            "StageStarted" => RuntimeReactiveEventNames.StageStarted,
            "StageCompleted" => RuntimeReactiveEventNames.StageCompleted,
            "StageFailed" => RuntimeReactiveEventNames.StageFailed,
            "StageSkipped" => RuntimeReactiveEventNames.StageSkipped,
            "TaskStarted" => RuntimeReactiveEventNames.TaskStarted,
            "TaskInputTransformed" => RuntimeReactiveEventNames.TaskInputTransformed,
            "TaskCompleted" or "TaskCallbackCompleted" => RuntimeReactiveEventNames.TaskCompleted,
            "TaskFailed" or "TaskCallbackFailed" => RuntimeReactiveEventNames.TaskFailed,
            "TaskSkipped" => RuntimeReactiveEventNames.TaskSkipped,
            "TaskWaitingResponse" => RuntimeReactiveEventNames.TaskWaiting,
            "TaskRetryScheduled" => RuntimeReactiveEventNames.TaskRetryScheduled,
            "TaskRetryStarted" => RuntimeReactiveEventNames.TaskRetryStarted,
            "TaskTimedOut" => RuntimeReactiveEventNames.TaskTimedOut,
            "DispatchPublished" or "TaskDispatched" => RuntimeReactiveEventNames.DispatchPublished,
            "DispatchFailed" => RuntimeReactiveEventNames.DispatchFailed,
            "CompensationScheduled" => RuntimeReactiveEventNames.CompensationScheduled,
            "CompensationStarted" => RuntimeReactiveEventNames.CompensationStarted,
            "CompensationDispatched" => RuntimeReactiveEventNames.CompensationDispatched,
            "CompensationCompleted" => RuntimeReactiveEventNames.CompensationCompleted,
            "CompensationFailed" => RuntimeReactiveEventNames.CompensationFailed,
            "ParallelGroupStarted" => RuntimeReactiveEventNames.ParallelGroupStarted,
            "ParallelGroupCompleted" => RuntimeReactiveEventNames.ParallelGroupCompleted,
            "ParallelGroupFailed" => RuntimeReactiveEventNames.ParallelGroupFailed,
            _ => RuntimeReactiveEventNames.TransitionRecorded
        };
}
