using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryStageExecutionRepository : IStageExecutionRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryStageExecutionRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            _store.Stages[stageExecution.Id] = stageExecution;
            return Task.CompletedTask;
        }

        public Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
        {
            _store.Stages[stageExecution.Id] = stageExecution;
            return Task.CompletedTask;
        }

        public Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Stages.TryGetValue(stageExecutionId, out var stage)
                ? stage
                : throw new KeyNotFoundException($"Stage execution '{stageExecutionId}' was not found."));

        public Task<StageExecution> GetByInstanceAndKey(Id instanceId, string stageKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Stages.Values.FirstOrDefault(x =>
                    x.OrchestrationInstanceId == instanceId &&
                    x.StageKey == stageKey)
                ?? throw new KeyNotFoundException($"Stage execution '{stageKey}' was not found for instance '{instanceId}'."));

        public Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<StageExecution>>(_store.Stages.Values
                .Where(x => x.OrchestrationInstanceId == instanceId)
                .OrderBy(x => x.Order)
                .ToArray());
    }
}
