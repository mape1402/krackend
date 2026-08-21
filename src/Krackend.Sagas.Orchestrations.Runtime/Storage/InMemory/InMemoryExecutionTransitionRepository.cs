using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryExecutionTransitionRepository : IExecutionTransitionRepository
    {
        private readonly InMemoryRuntimeStore _store;
        private readonly IRuntimeReactiveEventPublisher _reactiveEventPublisher;

        public InMemoryExecutionTransitionRepository(
            InMemoryRuntimeStore store,
            IRuntimeReactiveEventPublisher reactiveEventPublisher)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _reactiveEventPublisher = reactiveEventPublisher ?? throw new ArgumentNullException(nameof(reactiveEventPublisher));
        }

        public async Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            _store.Transitions.Add(transition);
            var eventData = BuildReactiveEvent(transition);
            if (eventData is not null)
            {
                await _reactiveEventPublisher.Publish(eventData, cancellationToken);
            }
        }

        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(_store.Transitions
                .Where(x => x.OrchestrationInstanceId == instanceId)
                .OrderBy(x => x.OccurredOnUtc)
                .ToArray());

        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(_store.Transitions
                .Where(x => _store.Instances.TryGetValue(x.OrchestrationInstanceId, out var instance)
                    && string.Equals(instance.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.OccurredOnUtc)
                .Take(take)
                .ToArray());

        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTime.UtcNow;
            var bucketSize = GetBucketSize(sinceUtc, nowUtc);
            var firstBucketUtc = AlignBucket(sinceUtc, bucketSize);
            var instances = _store.Instances.Values
                .Where(x => string.Equals(x.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.StartedOnUtc <= nowUtc)
                .Where(x => x.StartedOnUtc >= firstBucketUtc || HasRecentTerminalTimestamp(x, firstBucketUtc) || IsActiveStatus(x.Status))
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<RuntimeTrafficPoint>>(
                BuildTraffic(instances, firstBucketUtc, nowUtc, bucketSize));
        }

        private RuntimeReactiveEvent BuildReactiveEvent(ExecutionTransition transition)
        {
            if (!_store.Instances.TryGetValue(transition.OrchestrationInstanceId, out var instance))
            {
                return null;
            }

            var stageKey = transition.StageExecutionId is null
                ? null
                : _store.Stages.TryGetValue(transition.StageExecutionId.Value, out var stage)
                    ? stage.StageKey
                    : null;
            var taskKey = transition.TaskExecutionId is null
                ? null
                : _store.Tasks.TryGetValue(transition.TaskExecutionId.Value, out var task)
                    ? task.TaskKey
                    : null;

            return new RuntimeReactiveEvent
            {
                Id = transition.Id,
                EventName = ResolveEventName(transition.TransitionType),
                TransitionType = transition.TransitionType,
                EnvironmentKey = instance.EnvironmentKey,
                OrchestrationDefinitionKey = instance.OrchestrationDefinitionKey,
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

        private static bool HasRecentTerminalTimestamp(OrchestrationInstance instance, DateTime sinceUtc)
            => instance.CompletedOnUtc >= sinceUtc
                || instance.FailedOnUtc >= sinceUtc
                || instance.StoppedOnUtc >= sinceUtc
                || instance.CompensatedOnUtc >= sinceUtc;

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

        private static bool IsActiveStatus(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Created
                or OrchestrationInstanceStatus.Running
                or OrchestrationInstanceStatus.Waiting
                or OrchestrationInstanceStatus.Compensating;

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
}
