using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryTaskDispatchRepository : ITaskDispatchRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryTaskDispatchRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            _store.Dispatches[dispatch.Id] = dispatch;
            return Task.CompletedTask;
        }

        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            _store.Dispatches[dispatch.Id] = dispatch;
            return Task.CompletedTask;
        }

        public Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default)
        {
            var dispatch = _store.Dispatches[dispatchId];
            dispatch.DispatchStatus = status;
            dispatch.SentOnUtc = sentOnUtc;
            return Task.CompletedTask;
        }

        public Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default)
        {
            var dispatch = _store.Dispatches[dispatchId];
            dispatch.DispatchStatus = "Failed";
            dispatch.FailedOnUtc = DateTime.UtcNow;
            dispatch.FailureReason = failureReason;
            return Task.CompletedTask;
        }

        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.TryGetValue(dispatchId, out var dispatch)
                ? dispatch
                : throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found."));

        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.TryGetValue(dispatchId, out var dispatch) ? dispatch : null);

        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.Values.FirstOrDefault(x => x.CommandId == commandId)
                ?? throw new KeyNotFoundException($"Task dispatch with command id '{commandId}' was not found."));

        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.Values.FirstOrDefault(x => x.TaskExecutionAttemptId == taskExecutionAttemptId)
                ?? throw new KeyNotFoundException($"Task dispatch for attempt '{taskExecutionAttemptId}' was not found."));

        public Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(_store.Dispatches.Values
                .Where(x => x.ScheduledOnUtc <= dueBeforeUtc)
                .ToArray());
    }
}
