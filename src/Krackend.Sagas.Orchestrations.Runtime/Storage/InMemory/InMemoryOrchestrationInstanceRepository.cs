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
            if (_store.Instances.TryGetValue(instance.Id, out var current) && !ReferenceEquals(current, instance))
            {
                lock (current)
                {
                    instance.ActiveLeaseId = current.ActiveLeaseId;
                    instance.ActiveLeaseExpiresOnUtc = current.ActiveLeaseExpiresOnUtc;
                }
            }

            _store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Instances.TryGetValue(instanceId, out var instance)
                ? instance
                : throw new KeyNotFoundException($"Orchestration instance '{instanceId}' was not found."));

        public Task<OrchestrationInstance> TryGetByStartIdempotencyKey(
            string startIdempotencyKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(startIdempotencyKey))
            {
                return Task.FromResult<OrchestrationInstance>(null);
            }

            var normalizedKey = startIdempotencyKey.Trim();
            return Task.FromResult(_store.Instances.Values.FirstOrDefault(instance =>
                string.Equals(instance.StartIdempotencyKey, normalizedKey, StringComparison.Ordinal)));
        }

        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default)
        {
            var instance = _store.Instances[instanceId];
            lock (instance)
            {
                if (!string.IsNullOrWhiteSpace(instance.ActiveLeaseId) &&
                    !string.Equals(instance.ActiveLeaseId, leaseId, StringComparison.Ordinal) &&
                    instance.ActiveLeaseExpiresOnUtc > nowUtc)
                {
                    return Task.FromResult<OrchestrationInstanceLease>(null);
                }

                instance.ActiveLeaseId = leaseId;
                instance.ActiveLeaseExpiresOnUtc = expiresOnUtc;
                return Task.FromResult(new OrchestrationInstanceLease(instanceId, leaseId, nowUtc, expiresOnUtc));
            }
        }

        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
        {
            if (_store.Instances.TryGetValue(instanceId, out var instance))
            {
                lock (instance)
                {
                    if (instance.ActiveLeaseId == leaseId)
                    {
                        instance.ActiveLeaseId = null;
                        instance.ActiveLeaseExpiresOnUtc = null;
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(_store.Instances.Values
                .OrderByDescending(x => x.LastUpdatedOnUtc)
                .Take(take)
                .ToArray());

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecoverable(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(_store.Instances.Values
                .Where(static x => !IsTerminal(x.Status))
                .OrderBy(static x => x.LastUpdatedOnUtc)
                .Take(take)
                .ToArray());

        public Task<RuntimeInstanceSummary> GetSummary(DateTime recentSinceUtc, CancellationToken cancellationToken = default)
        {
            var instances = _store.Instances.Values.ToArray();
            return Task.FromResult(new RuntimeInstanceSummary(
                instances.Count(x => x.Status is OrchestrationInstanceStatus.Created or OrchestrationInstanceStatus.Running),
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Waiting),
                instances.Count(x => x.CompletedOnUtc >= recentSinceUtc),
                instances.Count(x => x.FailedOnUtc >= recentSinceUtc || x.Status == OrchestrationInstanceStatus.DeadLettered),
                recentSinceUtc));
        }

        private static bool IsTerminal(OrchestrationInstanceStatus status)
            => status is OrchestrationInstanceStatus.Stopped
                or OrchestrationInstanceStatus.Compensated
                or OrchestrationInstanceStatus.Completed
                or OrchestrationInstanceStatus.CompletedWithErrors
                or OrchestrationInstanceStatus.DeadLettered
                or OrchestrationInstanceStatus.Aborted
                or OrchestrationInstanceStatus.Failed;
    }
}
