using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryOrchestrationInstanceRepository : IOrchestrationInstanceRepository, IOrchestrationIdleInstanceRepository, IOrchestrationDispatchCancellationIntentRepository, IRuntimeInstanceCatalogRepository, IRuntimeInstanceStatusCountsRepository
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

        public Task<RuntimeInstanceCatalogPage> GetPage(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var page = Math.Max(1, query.PageNumber);
            var size = Math.Clamp(query.PageSize, 1, 100);
            var skip = (int)Math.Min((long)(page - 1) * size, int.MaxValue);
            var filtered = ApplyCatalogFilters(query).ToArray();
            var ordered = (query.Sort, query.Descending) switch
            {
                (RuntimeInstanceCatalogSort.Started, false) => filtered.OrderBy(x => x.StartedOnUtc),
                (RuntimeInstanceCatalogSort.Started, true) => filtered.OrderByDescending(x => x.StartedOnUtc),
                (RuntimeInstanceCatalogSort.Orchestration, false) => filtered.OrderBy(x => x.OrchestrationDefinitionKey),
                (RuntimeInstanceCatalogSort.Orchestration, true) => filtered.OrderByDescending(x => x.OrchestrationDefinitionKey),
                (RuntimeInstanceCatalogSort.CorrelationId, false) => filtered.OrderBy(x => x.CorrelationId),
                (RuntimeInstanceCatalogSort.CorrelationId, true) => filtered.OrderByDescending(x => x.CorrelationId),
                (RuntimeInstanceCatalogSort.SagaId, false) => filtered.OrderBy(x => x.SagaId),
                (RuntimeInstanceCatalogSort.SagaId, true) => filtered.OrderByDescending(x => x.SagaId),
                (RuntimeInstanceCatalogSort.Status, false) => filtered.OrderBy(x => x.Status.ToString(), StringComparer.Ordinal),
                (RuntimeInstanceCatalogSort.Status, true) => filtered.OrderByDescending(x => x.Status.ToString(), StringComparer.Ordinal),
                (_, false) => filtered.OrderBy(x => x.LastUpdatedOnUtc),
                _ => filtered.OrderByDescending(x => x.LastUpdatedOnUtc)
            };
            return Task.FromResult(new RuntimeInstanceCatalogPage(ordered.ThenBy(x => x.Id).Skip(skip).Take(size).ToArray(), filtered.LongLength, page, size));
        }

        public Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(
            RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyDictionary<OrchestrationInstanceStatus, long> counts = ApplyCatalogFilters(query)
                .GroupBy(x => x.Status).ToDictionary(group => group.Key, group => group.LongCount());
            return Task.FromResult(counts);
        }

        private IEnumerable<OrchestrationInstance> ApplyCatalogFilters(RuntimeInstanceCatalogQuery query)
        {
            IEnumerable<OrchestrationInstance> source = _store.Instances.Values;
            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var term = query.SearchText.Trim();
                if (Ulid.TryParse(term, out var parsedId))
                {
                    var instanceId = new Id(parsedId);
                    source = source.Where(x => x.Id == instanceId || x.OrchestrationDefinitionKey.Contains(term)
                        || x.CorrelationId.Contains(term) || x.SagaId.Contains(term) || x.ExecutionKey.Contains(term));
                }
                else
                    source = source.Where(x => x.OrchestrationDefinitionKey.Contains(term) || x.CorrelationId.Contains(term)
                        || x.SagaId.Contains(term) || x.ExecutionKey.Contains(term));
            }
            if (query.Status is { } status) source = source.Where(x => x.Status == status);
            if (query.StartedFromUtc is { } startedFrom) source = source.Where(x => x.StartedOnUtc >= startedFrom);
            if (query.StartedBeforeUtc is { } startedBefore) source = source.Where(x => x.StartedOnUtc < startedBefore);
            if (query.ActiveOnly)
                source = source.Where(x => x.Status is OrchestrationInstanceStatus.Created
                    or OrchestrationInstanceStatus.Running
                    or OrchestrationInstanceStatus.Idle
                    or OrchestrationInstanceStatus.Compensating);
            return source;
        }

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetDispatchCancellationPending(int take, int skip, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(_store.Instances.Values
                .Where(x => x.DispatchCancellationPending)
                .OrderBy(x => x.LastUpdatedOnUtc)
                .ThenBy(x => x.Id)
                .Skip(Math.Max(0, skip))
                .Take(Math.Max(1, take))
                .ToArray());

        public Task<IReadOnlyCollection<OrchestrationInstance>> GetIdleExpiringOnOrBefore(
            DateTime utcNow,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<OrchestrationInstance>>(_store.Instances.Values
                .Where(x => x.Status == OrchestrationInstanceStatus.Idle &&
                            x.IdleExpiresOnUtc is not null &&
                            x.IdleExpiresOnUtc <= utcNow)
                .OrderBy(x => x.IdleExpiresOnUtc)
                .ThenBy(x => x.Id)
                .Take(Math.Max(1, take))
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
                instances.Count(x => x.Status == OrchestrationInstanceStatus.Idle),
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
                or OrchestrationInstanceStatus.Aborted;
    }
}
