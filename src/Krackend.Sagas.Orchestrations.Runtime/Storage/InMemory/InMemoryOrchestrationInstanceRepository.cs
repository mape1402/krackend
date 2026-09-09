using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryOrchestrationInstanceRepository : IOrchestrationInstanceRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryOrchestrationInstanceRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            _store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            _store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Instances.TryGetValue(instanceId, out var instance)
                ? instance
                : throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found."));

        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default)
        {
            var instance = _store.Instances[instanceId];
            if (!string.IsNullOrWhiteSpace(instance.ActiveLeaseId) && instance.ActiveLeaseExpiresOnUtc > nowUtc)
            {
                return Task.FromResult<OrchestrationInstanceLease>(null);
            }

            instance.ActiveLeaseId = leaseId;
            instance.ActiveLeaseExpiresOnUtc = expiresOnUtc;
            return Task.FromResult(new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc));
        }

        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
        {
            if (_store.Instances.TryGetValue(instanceId, out var instance) && instance.ActiveLeaseId == leaseId)
            {
                instance.ActiveLeaseId = null;
                instance.ActiveLeaseExpiresOnUtc = null;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(_store.Instances.Values
                .OrderByDescending(x => x.LastUpdatedOnUtc)
                .Take(take)
                .ToArray());

        public Task<RuntimeInstanceSummary> GetSummary(DateTime recentSinceUtc, CancellationToken cancellationToken = default)
        {
            var instances = _store.Instances.Values.ToArray();
            return Task.FromResult(new RuntimeInstanceSummary(
                instances.Count(x => x.Status is OrchestrationInstanceStatus.Created or OrchestrationInstanceStatus.Running),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Waiting),
                instances.Count(x => x.CompletedOnUtc >= recentSinceUtc),
                instances.Count(x => x.FailedOnUtc >= recentSinceUtc),
                recentSinceUtc));
        }
    }
}
