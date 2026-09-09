using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal class IngressRegistry : IIngressRegistry
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRuntimeIngressLocalState _localState;

        private readonly ConcurrentDictionary<string, IList<string>> _connectors = new();

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
            foreach (var connectorsByArtifact in _connectors.Values)
                await ShutDownConfigurationsAsync(connectorsByArtifact.AsReadOnly(), cancellationToken);
        }

        public async Task ShutDownOneAsync(string artifactId, CancellationToken cancellationToken = default)
        {
            if(_connectors.TryGetValue(artifactId, out var connectorsByArtifact))
                await ShutDownConfigurationsAsync(connectorsByArtifact.AsReadOnly(), cancellationToken);
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
                if (_connectors.TryGetValue(configuration.ArtifactId, out var existingConnectorIds)
                    && existingConnectorIds.Contains(configuration.Id))
                {
                    continue;
                }

                var connector = serviceProvider.GetKeyedService<IIngressConector>(configuration.IngressTransport);

                if (connector == null)
                {
                    throw new IngressStandupConfigurationException($"Cannot stand up ingress configuration '{configuration.Id}' for artifact '{configuration.ArtifactId}' because transport '{configuration.IngressTransport}' has no registered connector.");
                }

                await connector.ConnectAsync(configuration, cancellationToken);

                if (!_connectors.TryGetValue(configuration.ArtifactId, out var connectorIds))
                    connectorIds = new List<string>();

                connectorIds.Add(configuration.Id);
                _connectors.AddOrUpdate(configuration.ArtifactId, connectorIds, (_, _) => connectorIds);
            }
        }

        private Task ShutDownConfigurationsAsync(IReadOnlyCollection<string> connectorIds, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask; //TODO: Implement shutdown!!!
        }
    }
}
