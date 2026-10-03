using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers
{
    internal sealed class RetryDecisionHandler : IDecisionHandler<RetryDecision>
    {
        private readonly IOrchestrationInstanceRepository _instanceRepository;
        private readonly ITaskExecutionRepository _taskRepository;
        private readonly ITaskAttemptDispatcher _taskAttemptDispatcher;

        public RetryDecisionHandler(
            IOrchestrationInstanceRepository instanceRepository,
            ITaskExecutionRepository taskRepository,
            ITaskAttemptDispatcher taskAttemptDispatcher)
        {
            _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _taskAttemptDispatcher = taskAttemptDispatcher ?? throw new ArgumentNullException(nameof(taskAttemptDispatcher));
        }

        public async Task HandleAsync(RetryDecision decision, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var instance = await _instanceRepository.GetById(decision.InstanceId, cancellationToken);
            var taskExecution = await _taskRepository.GetById(decision.TaskExecutionId, cancellationToken);
            if (!CanRetryCurrentTaskState(taskExecution, decision.Task))
            {
                return;
            }

            await _taskAttemptDispatcher.DispatchAsync(
                new TaskAttemptDispatchRequest
                {
                    Kind = TaskAttemptDispatchKind.Retry,
                    Instance = instance,
                    StageExecutionId = decision.StageExecutionId,
                    StageKey = decision.StageKey,
                    Task = decision.Task,
                    OrchestrationExecutionPolicy = decision.OrchestrationExecutionPolicy,
                    StageExecutionPolicy = decision.StageExecutionPolicy,
                    TaskExecution = taskExecution,
                    Payload = decision.Payload,
                    MetadataDescriptors = decision.MetadataDescriptors,
                    NowUtc = now,
                    ScheduledOnUtc = ResolveRetryScheduledOnUtc(taskExecution, decision.Task, now)
                },
                cancellationToken);
        }

        private static DateTimeOffset ResolveRetryScheduledOnUtc(
            TaskExecution failedTask,
            TaskArtifact taskArtifact,
            DateTime now)
        {
            var retryPolicy = ResolveRetryPolicy(failedTask, taskArtifact);
            if (retryPolicy?.Strategy is not FixedRetryStrategyArtifact fixedRetry ||
                fixedRetry.Delay.Value <= TimeSpan.Zero)
            {
                return new DateTimeOffset(now, TimeSpan.Zero);
            }

            return new DateTimeOffset(now, TimeSpan.Zero).Add(fixedRetry.Delay.Value);
        }

        private static RetryPolicyArtifact ResolveRetryPolicy(
            TaskExecution failedTask,
            TaskArtifact taskArtifact)
        {
            if (failedTask.Status == TaskExecutionStatus.TimedOut &&
                taskArtifact?.TimeoutPolicy?.TimeoutBehaviorPolicy is ReconcileTimeoutBehaviorPolicyArtifact reconcile)
            {
                return reconcile.RetryPolicy;
            }

            return taskArtifact?.RetryPolicy;
        }

        private static bool CanRetryCurrentTaskState(
            TaskExecution taskExecution,
            TaskArtifact taskArtifact)
        {
            if (taskExecution.Status is not (TaskExecutionStatus.Failed or TaskExecutionStatus.TimedOut))
            {
                return false;
            }

            var retryPolicy = ResolveRetryPolicy(taskExecution, taskArtifact);
            return retryPolicy is not null &&
                taskExecution.LastAttemptNumber <= retryPolicy.MaxRetries;
        }
    }
}
