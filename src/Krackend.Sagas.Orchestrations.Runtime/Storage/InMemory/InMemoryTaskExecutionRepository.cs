using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryTaskExecutionRepository : ITaskExecutionRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryTaskExecutionRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            _store.Tasks[taskExecution.Id] = taskExecution;
            return Task.CompletedTask;
        }

        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
        {
            _store.Tasks[taskExecution.Id] = taskExecution;
            return Task.CompletedTask;
        }

        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Tasks.TryGetValue(taskExecutionId, out var task)
                ? task
                : throw new KeyNotFoundException($"Task execution '{taskExecutionId}' was not found."));

        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Tasks.Values.FirstOrDefault(x => x.CorrelationId == correlationId)
                ?? throw new KeyNotFoundException($"Task execution with correlation id '{correlationId}' was not found."));

        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Tasks.Values.FirstOrDefault(x =>
                    x.StageExecutionId == stageExecutionId &&
                    x.TaskKey == taskKey)
                ?? throw new KeyNotFoundException($"Task execution '{taskKey}' was not found for stage '{stageExecutionId}'."));

        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(_store.Tasks.Values
                .Where(x => x.OrchestrationInstanceId == instanceId)
                .ToArray());

        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(_store.Tasks.Values
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse && x.WaitingSinceUtc <= dueBeforeUtc)
                .ToArray());
    }
}
