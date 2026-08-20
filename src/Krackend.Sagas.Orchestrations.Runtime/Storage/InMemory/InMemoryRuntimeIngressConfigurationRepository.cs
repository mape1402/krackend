using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryRuntimeIngressConfigurationRepository : IRuntimeIngressConfigurationRepository
    {
        private readonly InMemoryRuntimeStore _store;

        public InMemoryRuntimeIngressConfigurationRepository(InMemoryRuntimeStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task UpsertForArtifactAsync(
            Id runtimeOrchestrationArtifactId,
            IReadOnlyCollection<RuntimeIngressConfiguration> configurations,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            foreach (var current in _store.IngressConfigurations.Values.Where(x =>
                         x.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId &&
                         x.IsActive))
            {
                current.IsActive = false;
                current.UpdatedOnUtc = now;
                current.DeactivatedOnUtc = now;
            }

            foreach (var configuration in configurations)
            {
                var current = _store.IngressConfigurations.Values.FirstOrDefault(x =>
                    x.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId &&
                    x.ConfigurationKey == configuration.ConfigurationKey);

                if (current is null)
                {
                    _store.IngressConfigurations[configuration.Id] = configuration;
                    continue;
                }

                current.IngressKind = configuration.IngressKind;
                current.IngressTransport = configuration.IngressTransport;
                current.SettingsPayload = configuration.SettingsPayload;
                current.IsActive = true;
                current.UpdatedOnUtc = configuration.UpdatedOnUtc;
                current.DeactivatedOnUtc = null;
            }

            return Task.CompletedTask;
        }

        public Task DeactivateForArtifactsAsync(
            IReadOnlyCollection<Id> runtimeOrchestrationArtifactIds,
            CancellationToken cancellationToken = default)
        {
            if (runtimeOrchestrationArtifactIds is null || runtimeOrchestrationArtifactIds.Count == 0)
            {
                return Task.CompletedTask;
            }

            var now = DateTime.UtcNow;
            foreach (var configuration in _store.IngressConfigurations.Values.Where(x =>
                         runtimeOrchestrationArtifactIds.Contains(x.RuntimeOrchestrationArtifactId) &&
                         x.IsActive))
            {
                configuration.IsActive = false;
                configuration.UpdatedOnUtc = now;
                configuration.DeactivatedOnUtc = now;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<RuntimeIngressConfiguration>> ReadActiveAsync(
            int skip,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeIngressConfiguration>>(
                _store.IngressConfigurations.Values
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.RuntimeOrchestrationArtifactId.ToString())
                    .ThenBy(x => x.ConfigurationKey)
                    .Skip(skip)
                    .Take(take)
                    .ToArray());

        public Task<IReadOnlyCollection<RuntimeIngressConfiguration>> GetActiveByArtifactIdAsync(
            Id runtimeOrchestrationArtifactId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<RuntimeIngressConfiguration>>(
                _store.IngressConfigurations.Values
                    .Where(x => x.RuntimeOrchestrationArtifactId == runtimeOrchestrationArtifactId && x.IsActive)
                    .OrderBy(x => x.ConfigurationKey)
                    .ToArray());
    }
}
