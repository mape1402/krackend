using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryTaskExecutionAttemptRepository : ITaskExecutionAttemptRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryTaskExecutionAttemptRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            _store.Attempts[attempt.Id] = attempt;
            return Task.CompletedTask;
        }

        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        {
            _store.Attempts[attempt.Id] = attempt;
            return Task.CompletedTask;
        }

        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Attempts.TryGetValue(attemptId, out var attempt)
                ? attempt
                : throw new KeyNotFoundException($"Task execution attempt '{attemptId}' was not found."));

        public Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default)
        {
            var dispatch = _store.Dispatches.TryGetValue(dispatchId, out var foundDispatch)
                ? foundDispatch
                : throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found.");

            return GetById(dispatch.TaskExecutionAttemptId, cancellationToken);
        }

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(_store.Attempts.Values
                .Where(x => x.TaskExecutionId == taskExecutionId)
                .OrderBy(x => x.AttemptNumber)
                .ToArray());

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(_store.Attempts.Values
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse && x.WaitingSinceUtc <= dueBeforeUtc)
                .ToArray());
    }
}
