using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryTaskDispatchRepository : ITaskDispatchRepository, ITaskDispatchCancellationRepository, ITaskDispatchPublicationReceiptRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryTaskDispatchRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            _store.Dispatches[dispatch.Id] = dispatch.CreateSnapshot();
            return Task.CompletedTask;
        }

        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var current = _store.Dispatches[dispatch.Id];
                if (current.CallbackReceiptId != dispatch.CallbackReceiptId)
                    throw new RuntimeStateConflictException("A callback was accepted while this dispatch was being changed.");
                var next = dispatch.CreateSnapshot();
                next.SentOnUtc = current.SentOnUtc ?? next.SentOnUtc;
                next.CallbackAcceptedOnUtc = current.CallbackAcceptedOnUtc;
                next.CallbackPending = current.CallbackPending;
                next.PendingCallbackPayload = current.PendingCallbackPayload;
                if (_store.Dispatches.TryUpdate(dispatch.Id, next, current)) break;
            }
            return Task.CompletedTask;
        }

        public Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default)
        {
            var dispatch = _store.Dispatches[dispatchId].CreateSnapshot();
            dispatch.SentOnUtc = sentOnUtc;
            if (!IsFinished(dispatch.DispatchStatus))
            {
                dispatch.DispatchStatus = status;
            }

            return Update(dispatch, cancellationToken);
        }

        public Task RecordPublicationReceipt(Id dispatchId, DateTime publishedOnUtc, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (true)
            {
                var current = _store.Dispatches[dispatchId];
                if (current.SentOnUtc.HasValue) break;
                var next = current.CreateSnapshot();
                next.SentOnUtc = publishedOnUtc;
                if (_store.Dispatches.TryUpdate(dispatchId, next, current)) break;
            }
            return Task.CompletedTask;
        }

        public Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default)
        {
            var dispatch = _store.Dispatches[dispatchId].CreateSnapshot();
            dispatch.DispatchStatus = "Failed";
            dispatch.FailedOnUtc = DateTime.UtcNow;
            dispatch.FailureReason = failureReason;
            return Update(dispatch, cancellationToken);
        }

        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.TryGetValue(dispatchId, out var dispatch)
                ? dispatch.CreateSnapshot()
                : throw new KeyNotFoundException($"Task dispatch '{dispatchId}' was not found."));

        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.TryGetValue(dispatchId, out var dispatch) ? dispatch.CreateSnapshot() : null);

        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.Values.FirstOrDefault(x => x.CommandId == commandId)?.CreateSnapshot()
                ?? throw new KeyNotFoundException($"Task dispatch with command id '{commandId}' was not found."));

        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Dispatches.Values.FirstOrDefault(x => x.TaskExecutionAttemptId == taskExecutionAttemptId)?.CreateSnapshot()
                ?? throw new KeyNotFoundException($"Task dispatch for attempt '{taskExecutionAttemptId}' was not found."));

        public Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(_store.Dispatches.Values
                .Where(x => x.ScheduledOnUtc <= dueBeforeUtc)
                .Select(x => x.CreateSnapshot())
                .ToArray());

        public Task<IReadOnlyCollection<TaskDispatch>> GetQueuePending(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(_store.Dispatches.Values
                .Where(x => string.Equals(x.DispatchStatus, "QueuePending", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DispatchStatus, "WaitingResponse", StringComparison.OrdinalIgnoreCase) &&
                    x.SentOnUtc is null)
                .OrderBy(x => x.ScheduledOnUtc)
                .Take(Math.Max(1, take))
                .Select(x => x.CreateSnapshot())
                .ToArray());

        public Task<IReadOnlyCollection<TaskDispatch>> GetPhysicalCancellationPending(
            int take,
            int skip,
            DateTime attemptedBeforeUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskDispatch>>(_store.Dispatches.Values
                .Where(x => x.PhysicalCancellationPending &&
                    (x.PhysicalCancellationLastAttemptOnUtc is null || x.PhysicalCancellationLastAttemptOnUtc <= attemptedBeforeUtc))
                .OrderBy(x => x.PhysicalCancellationLastAttemptOnUtc)
                .ThenBy(x => x.Id)
                .Skip(Math.Max(0, skip))
                .Take(Math.Max(1, take))
                .Select(x => x.CreateSnapshot())
                .ToArray());

        private static bool IsFinished(string status)
            => string.Equals(status, "Acknowledged", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "TimedOut", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }
}
