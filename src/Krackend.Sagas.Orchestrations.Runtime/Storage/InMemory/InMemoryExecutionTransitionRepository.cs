using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryExecutionTransitionRepository : IExecutionTransitionRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryExecutionTransitionRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            _store.Transitions.Add(transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(_store.Transitions
                .Where(x => x.OrchestrationInstanceId == instanceId)
                .OrderBy(x => x.OccurredOnUtc)
                .ToArray());

        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExecutionTransition>>(_store.Transitions
                .OrderByDescending(x => x.OccurredOnUtc)
                .Take(take)
                .ToArray());

        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeTrafficPoint>>([]);
    }
}
