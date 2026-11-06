using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal class IngressRegistry : IIngressRegistry, IStagedIngressRegistry, IRollbackIngressRegistry, IRefreshIngressRegistry, IArchivedRecoveryIngressRegistry, IArchivedRecoveryIngressCleanup
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRuntimeIngressLocalState _localState;
        private readonly IRuntimeAdmissionController _admissionController;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IngressConnectorRegistration>> _connectors = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _rollbackLocks = new();

        public IngressRegistry(
            IServiceScopeFactory scopeFactory,
            IRuntimeIngressLocalState localState,
            IRuntimeAdmissionController admissionController)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _localState = localState ?? throw new ArgumentNullException(nameof(localState));
            _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        }

        public async Task StandUpAllAsync(CancellationToken cancellationToken = default)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.IngressStandup, cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            using var reader = scope.ServiceProvider.GetRequiredService<IGetAllIngressConfigurationsAccessor>();

            IngressConfigurationReadingResult dataset;
            do
            {
                dataset = await reader.ReadAsync(cancellationToken);
                await StandUpConfigurationsAsync(scope.ServiceProvider, dataset.Configurations, cancellationToken);
            }
            while (dataset.HasMoreItems);
        }

        public Task StandUpOneAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
            => StandUpAsync(artifactId, ingressGeneration, false, false, cancellationToken);

        public Task StandUpStagedAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
            => StandUpAsync(artifactId, ingressGeneration, true, false, cancellationToken);

        public Task RefreshOneAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
            => StandUpAsync(artifactId, ingressGeneration, false, true, cancellationToken);

        public async Task<IArchivedRecoveryIngressPreparation> PrepareArchivedRecoveryAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.IngressStandup, cancellationToken);
            var gate = _rollbackLocks.GetOrAdd(artifactId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            var added = new List<IngressConnectorRegistration>();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var accessor = scope.ServiceProvider.GetRequiredService<IGetArchivedBackchannelConfigurationAccessor>();
                var configurations = (await accessor.GetArchivedBackchannelsAsync(artifactId, ingressGeneration, cancellationToken))?.ToArray() ?? [];
                if (configurations.Length == 0 || configurations.Any(x => x.IngressKind != IngressKind.Backchannel))
                    throw new IngressStandupConfigurationException("The archived artifact has no valid backchannel for recovery.");
                foreach (var configuration in configurations) configuration.IsStaged = true;
                await StandUpConfigurationsAsync(scope.ServiceProvider, configurations, cancellationToken, verifyExisting: true, added: added);
                return new RollbackPreparation(this, artifactId, added, gate);
            }
            catch
            {
                try { await RemoveRollbackConnectorsAsync(artifactId, added); }
                finally { gate.Release(); }
                throw;
            }
        }

        public async Task<IRollbackIngressPreparation> PrepareRollbackAsync(string artifactId, long ingressGeneration, IReadOnlyCollection<string> triggerKeys, CancellationToken cancellationToken = default)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.IngressStandup, cancellationToken);
            var keys = (triggerKeys ?? []).Distinct(StringComparer.Ordinal).ToArray();
            if (keys.Length == 0) throw new IngressStandupConfigurationException("Rollback requires at least one target trigger.");
            var gate = _rollbackLocks.GetOrAdd(artifactId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            var added = new List<IngressConnectorRegistration>();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var accessor = scope.ServiceProvider.GetRequiredService<IGetRollbackIngressConfigurationAccessor>();
                var configurations = (await accessor.GetRollbackConfigurationAsync(artifactId, ingressGeneration, keys, cancellationToken))?.ToArray() ?? [];
                if (configurations.Count(x => x.IngressKind == IngressKind.Trigger) != keys.Length ||
                    !configurations.Any(x => x.IngressKind == IngressKind.Backchannel))
                    throw new IngressStandupConfigurationException("Rollback target trigger or backchannel configuration is missing.");
                foreach (var configuration in configurations) configuration.IsStaged = true;
                await StandUpConfigurationsAsync(scope.ServiceProvider, configurations, cancellationToken, verifyExisting: true, added: added);
                return new RollbackPreparation(this, artifactId, added, gate);
            }
            catch
            {
                try { await RemoveRollbackConnectorsAsync(artifactId, added); }
                finally { gate.Release(); }
                throw;
            }
        }

        private async Task StandUpAsync(string artifactId, long ingressGeneration, bool staged, bool force, CancellationToken cancellationToken)
        {
            await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.IngressStandup, cancellationToken);
            var gate = _rollbackLocks.GetOrAdd(artifactId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (!force && _localState.IsApplied(artifactId, ingressGeneration)) return;
                using var scope = _scopeFactory.CreateScope();
                var configurations = (staged
                    ? await scope.ServiceProvider.GetRequiredService<IGetStagedIngressConfigurationAccessor>()
                        .GetStagedConfigurationAsync(artifactId, ingressGeneration, cancellationToken)
                    : await scope.ServiceProvider.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>()
                        .GetConfigurationAsync(artifactId, cancellationToken))?.ToArray()
                    ?? Array.Empty<IngressConfiguration>();

                if (configurations.Length == 0)
                    throw new IngressStandupConfigurationException($"Cannot stand up artifact '{artifactId}' generation '{ingressGeneration}' because no ingress configurations were found.");

                if (staged)
                    foreach (var configuration in configurations) configuration.IsStaged = true;

                await StandUpConfigurationsAsync(scope.ServiceProvider, configurations, cancellationToken);
                if (force && _connectors.TryGetValue(artifactId, out var registrations))
                {
                    var activeIds = configurations.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                    foreach (var obsolete in registrations.Values.Where(x => !activeIds.Contains(x.ConnectorId)).ToArray())
                    {
                        await ShutDownConfigurationsAsync([obsolete], cancellationToken);
                        registrations.TryRemove(obsolete.ConnectorId, out _);
                    }
                }
                _localState.MarkApplied(artifactId, ingressGeneration);
            }
            finally { gate.Release(); }
        }

        public async Task ShutDownAllAsync(CancellationToken cancellationToken = default)
        {
            foreach (var artifactConnectors in _connectors.ToArray())
            {
                if (_connectors.TryRemove(artifactConnectors.Key, out var connectorsByArtifact))
                {
                    await ShutDownConfigurationsAsync(connectorsByArtifact.Values.ToArray(), cancellationToken);
                }
            }

            _localState.Clear();
        }

        public async Task ShutDownOneAsync(string artifactId, CancellationToken cancellationToken = default)
        {
            var gate = _rollbackLocks.GetOrAdd(artifactId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (_connectors.TryRemove(artifactId, out var connectorsByArtifact))
                    await ShutDownConfigurationsAsync(connectorsByArtifact.Values.ToArray(), cancellationToken);
                _localState.Forget(artifactId);
            }
            finally { gate.Release(); }
        }

        public IReadOnlyCollection<string> GetLocalArtifactIds()
            => _connectors.Where(x => !x.Value.IsEmpty).Select(x => x.Key).ToArray();

        public async Task<bool> ShutDownIfArchivedAsync(string artifactId, CancellationToken cancellationToken = default)
        {
            if (!System.Ulid.TryParse(artifactId, out var parsed)) return false;
            var gate = _rollbackLocks.GetOrAdd(artifactId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (!_connectors.TryGetValue(artifactId, out var registrations) || registrations.IsEmpty) return false;
                using var scope = _scopeFactory.CreateScope();
                var artifacts = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>();
                var artifact = await artifacts.GetById(new Id(parsed), cancellationToken);
                if (artifact.IsActive || artifact.Status != RuntimeOrchestrationArtifactStatus.Retired ||
                    artifact.LifecycleStatus != RuntimeOrchestrationLifecycleStatus.Archived) return false;
                foreach (var registration in registrations.Values.ToArray())
                {
                    // Keep the registration if disconnect fails, allowing the next scan to retry.
                    await ShutDownConfigurationsAsync([registration], cancellationToken);
                    registrations.TryRemove(registration.ConnectorId, out _);
                }
                _localState.Forget(artifactId);
                return true;
            }
            finally { gate.Release(); }
        }

        private async Task StandUpConfigurationsAsync(
            IServiceProvider serviceProvider,
            IReadOnlyCollection<IngressConfiguration> configurations,
            CancellationToken cancellationToken = default,
            bool verifyExisting = false,
            List<IngressConnectorRegistration> added = null)
        {
            if (configurations is null)
            {
                return;
            }

            foreach (var configuration in configurations)
            {
                var connectorRegistrations = _connectors.GetOrAdd(
                    configuration.ArtifactId,
                    _ => new ConcurrentDictionary<string, IngressConnectorRegistration>());

                if (!connectorRegistrations.TryAdd(
                    configuration.Id,
                    new IngressConnectorRegistration(configuration.Id, configuration.IngressTransport)))
                {
                    if (verifyExisting)
                    {
                        var existingConnector = serviceProvider.GetKeyedService<IIngressConector>(configuration.IngressTransport);
                        if (existingConnector is not IRevalidatableIngressConector revalidatable)
                            throw new IngressStandupConfigurationException($"Ingress configuration '{configuration.Id}' cannot be revalidated for rollback.");
                        await revalidatable.RevalidateAsync(configuration, cancellationToken);
                    }
                    continue;
                }

                var connector = serviceProvider.GetKeyedService<IIngressConector>(configuration.IngressTransport);

                if (connector == null)
                {
                    connectorRegistrations.TryRemove(configuration.Id, out _);
                    throw new IngressStandupConfigurationException($"Cannot stand up ingress configuration '{configuration.Id}' for artifact '{configuration.ArtifactId}' because transport '{configuration.IngressTransport}' has no registered connector.");
                }

                try
                {
                    await connector.ConnectAsync(configuration, cancellationToken);
                    added?.Add(new IngressConnectorRegistration(configuration.Id, configuration.IngressTransport));
                }
                catch
                {
                    connectorRegistrations.TryRemove(configuration.Id, out _);
                    throw;
                }
            }
        }

        private async Task RemoveRollbackConnectorsAsync(string artifactId, IReadOnlyCollection<IngressConnectorRegistration> added)
        {
            if (!_connectors.TryGetValue(artifactId, out var registrations)) return;
            var removed = new List<IngressConnectorRegistration>();
            foreach (var item in added)
                if (registrations.TryRemove(item.ConnectorId, out var registration)) removed.Add(registration);
            await ShutDownConfigurationsAsync(removed, CancellationToken.None);
        }

        private sealed class RollbackPreparation(IngressRegistry registry, string artifactId,
            IReadOnlyCollection<IngressConnectorRegistration> added, SemaphoreSlim gate) : IRollbackIngressPreparation, IArchivedRecoveryIngressPreparation
        {
            private bool _completed;
            private bool _disposed;
            public void Complete() => _completed = true;
            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;
                try { if (!_completed) await registry.RemoveRollbackConnectorsAsync(artifactId, added); }
                finally { gate.Release(); }
            }
        }

        private async Task ShutDownConfigurationsAsync(
            IReadOnlyCollection<IngressConnectorRegistration> connectorRegistrations,
            CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            foreach (var registration in connectorRegistrations)
            {
                var connector = scope.ServiceProvider.GetKeyedService<IIngressConector>(registration.IngressTransport);
                if (connector is null)
                {
                    continue;
                }

                await connector.DisconnectAsync(registration.ConnectorId, cancellationToken);
            }
        }
    }
}
