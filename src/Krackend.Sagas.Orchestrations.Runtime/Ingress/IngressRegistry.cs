using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal class IngressRegistry : IIngressRegistry
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRuntimeIngressLocalState _localState;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IngressConnectorRegistration>> _connectors = new();

        public IngressRegistry(
            IServiceScopeFactory scopeFactory,
            IRuntimeIngressLocalState localState)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _localState = localState ?? throw new ArgumentNullException(nameof(localState));
        }

        public async Task StandUpAllAsync(CancellationToken cancellationToken = default)
        {
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


        public async Task StandUpOneAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
        {
            if (_localState.IsApplied(artifactId, ingressGeneration))
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>();
            var configurations = (await accessor.GetConfigurationAsync(artifactId, cancellationToken))?.ToArray()
                ?? Array.Empty<IngressConfiguration>();

            if (configurations.Length == 0)
            {
                throw new IngressStandupConfigurationException($"Cannot stand up artifact '{artifactId}' generation '{ingressGeneration}' because no ingress configurations were found.");
            }

            await StandUpConfigurationsAsync(scope.ServiceProvider, configurations, cancellationToken);
            _localState.MarkApplied(artifactId, ingressGeneration);
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
            if (_connectors.TryRemove(artifactId, out var connectorsByArtifact))
            {
                await ShutDownConfigurationsAsync(connectorsByArtifact.Values.ToArray(), cancellationToken);
            }

            _localState.Forget(artifactId);
        }

        private async Task StandUpConfigurationsAsync(
            IServiceProvider serviceProvider,
            IReadOnlyCollection<IngressConfiguration> configurations,
            CancellationToken cancellationToken = default)
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
                }
                catch
                {
                    connectorRegistrations.TryRemove(configuration.Id, out _);
                    throw;
                }
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
