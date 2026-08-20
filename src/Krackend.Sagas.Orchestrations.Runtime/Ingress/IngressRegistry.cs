using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal class IngressRegistry : IIngressRegistry
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IngressRegistry> _logger;

        private readonly ConcurrentDictionary<string, IList<string>> _connectors = new();

        public IngressRegistry(IServiceScopeFactory scopeFactory, ILogger<IngressRegistry> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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


        public async Task StandUpOneAsync(string artifactId, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>();
            var configurations = await accessor.GetConfigurationAsync(artifactId, cancellationToken);

            if (configurations == null || !configurations.Any())
            {
                _logger.LogError("Cannot stand up a new orchestration roadmap with artifact id '{id}'", artifactId);
                return;
            }

            await StandUpConfigurationsAsync(scope.ServiceProvider, configurations, cancellationToken);
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
                var connector = serviceProvider.GetKeyedService<IIngressConector>(configuration.Kind);

                if (connector == null)
                {
                    _logger.LogWarning("Doesn't have a connector registered for '{kind}' ingress.", configuration.Kind);
                    return;
                }

                await connector.ConnectAsync(configuration.SettingsPayload, cancellationToken);

                if (!_connectors.TryGetValue(configuration.ArtifactId, out var connectorIds))
                    connectorIds = new List<string>();

                connectorIds.Add(configuration.Id);
                _connectors.AddOrUpdate(configuration.ArtifactId, connectorIds, (old, @new) => connectorIds);
            }
        }

        private Task ShutDownConfigurationsAsync(IReadOnlyCollection<string> connectorIds, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask; //TODO: Implement shutdown!!!
        }
    }
}
