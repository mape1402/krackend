using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryCompensationExecutionRepository : ICompensationExecutionRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryCompensationExecutionRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            _store.Compensations[compensationExecution.Id] = compensationExecution;
            return Task.CompletedTask;
        }

        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            _store.Compensations[compensationExecution.Id] = compensationExecution;
            return Task.CompletedTask;
        }

        public Task<CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Compensations.TryGetValue(compensationExecutionId, out var compensation) ? compensation : null);

        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(_store.Compensations.Values
                .Where(x => x.OrchestrationInstanceId == instanceId)
                .ToArray());

        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(_store.Compensations.Values
                .Where(x => x.Status == "Pending")
                .ToArray());
    }
}
