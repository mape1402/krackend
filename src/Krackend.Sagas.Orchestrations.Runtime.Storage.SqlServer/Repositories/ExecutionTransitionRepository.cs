using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.SqlServer.Repositories;

internal sealed class ExecutionTransitionRepository : RuntimeRepositoryBase, IExecutionTransitionRepository
{
    private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;

    public ExecutionTransitionRepository(
        RuntimeStorageDbContext dbContext,
        IRuntimeStorageUnitOfWork unitOfWork,
        IRuntimeReactiveEventPublisher reactiveEventPublisher)
        : base(dbContext, unitOfWork)
    {
        _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
    }

    public async Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
    {
        DbContext.ExecutionTransitions.Add(transition);
        await SaveChanges(cancellationToken);
        var eventData = await BuildReactiveEvent(transition, cancellationToken);
        if (eventData is not null)
        {
            await _reactiveEventPublisher.Publish(eventData, cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await DbContext.ExecutionTransitions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.OccurredOnUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
        => await (from transition in DbContext.ExecutionTransitions.AsNoTracking()
                  join instance in DbContext.OrchestrationInstances.AsNoTracking()
                      on transition.OrchestrationInstanceId equals instance.Id
                  where instance.EnvironmentKey == environmentKey
                  orderby transition.OccurredOnUtc descending
                  select transition)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var bucketSize = GetBucketSize(sinceUtc, nowUtc);
        var firstBucketUtc = AlignBucket(sinceUtc, bucketSize);
        var instances = await DbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .Where(x => x.StartedOnUtc <= nowUtc)
            .Where(x => x.StartedOnUtc >= firstBucketUtc
                || x.CompletedOnUtc >= firstBucketUtc
                || x.FailedOnUtc >= firstBucketUtc
                || x.StoppedOnUtc >= firstBucketUtc
                || x.CompensatedOnUtc >= firstBucketUtc
                || x.Status == OrchestrationInstanceStatus.Created
                || x.Status == OrchestrationInstanceStatus.Running
                || x.Status == OrchestrationInstanceStatus.Waiting
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

        var stageKey = await GetStageKey(transition.StageExecutionId, cancellationToken);
        var taskKey = await GetTaskKey(transition.TaskExecutionId, cancellationToken);
        var orchestrationVersion = await GetOrchestrationVersion(instance.RuntimeOrchestrationArtifactId, cancellationToken);
        return new RuntimeReactiveEvent
        {
            Id = transition.Id,
            EventName = ResolveEventName(transition.TransitionType),
            TransitionType = transition.TransitionType,
            EnvironmentKey = instance.EnvironmentKey,
            OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
            OrchestrationVersion = orchestrationVersion,
            OrchestrationInstanceId = transition.OrchestrationInstanceId,
            CorrelationId = instance.CorrelationId,
            ExecutionKey = instance.ExecutionKey,
            StageExecutionId = transition.StageExecutionId,
            StageKey = stageKey,
            TaskExecutionId = transition.TaskExecutionId,
            TaskKey = taskKey,
            TaskExecutionAttemptId = transition.TaskExecutionAttemptId,
            FromStatus = transition.FromStatus,
            ToStatus = transition.ToStatus,
            InstanceStatus = instance.Status.ToString(),
            OccurredOnUtc = transition.OccurredOnUtc,
            Message = transition.Message,
            Payload = transition.Payload,
            ProducedBy = transition.ProducedBy
        };
    }

    private async Task<string> GetStageKey(Id? stageExecutionId, CancellationToken cancellationToken)
    {
        if (stageExecutionId is null)
        {
            return null;
        }

        var stage = DbContext.StageExecutions.Local.FirstOrDefault(x => x.Id == stageExecutionId.Value)
            ?? await DbContext.StageExecutions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == stageExecutionId.Value, cancellationToken);

        return stage?.StageKey;
    }

    private async Task<string> GetTaskKey(Id? taskExecutionId, CancellationToken cancellationToken)
    {
        if (taskExecutionId is null)
        {
            return null;
        }

        var task = DbContext.TaskExecutions.Local.FirstOrDefault(x => x.Id == taskExecutionId.Value)
            ?? await DbContext.TaskExecutions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == taskExecutionId.Value, cancellationToken);

        return task?.TaskKey;
    }

    private async Task<string> GetOrchestrationVersion(Id runtimeOrchestrationArtifactId, CancellationToken cancellationToken)
    {
        var artifact = DbContext.RuntimeOrchestrationArtifacts.Local.FirstOrDefault(x => x.Id == runtimeOrchestrationArtifactId)
            ?? await DbContext.RuntimeOrchestrationArtifacts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == runtimeOrchestrationArtifactId, cancellationToken);

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
