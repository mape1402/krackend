namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed class RepositoryBackedIngressConfigurationAccessor :
    IGetAllIngressConfigurationsAccessor,
    IGetIngressConfigurationByArtifactAccessor
{
    private const int PageSize = 500;
    private readonly IRuntimeIngressConfigurationRepository _configurationRepository;
    private readonly IRuntimeArtifactRepository _artifactRepository;
    private int _skip;

    public RepositoryBackedIngressConfigurationAccessor(
        IRuntimeIngressConfigurationRepository configurationRepository,
        IRuntimeArtifactRepository artifactRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
    }

    public async Task<IngressConfigurationReadingResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var configurations = await _configurationRepository.ReadActiveAsync(_skip, PageSize, cancellationToken);
        var mapped = await MapAsync(configurations, cancellationToken);
        _skip += mapped.Length;

        return new IngressConfigurationReadingResult
        {
            HasMoreItems = mapped.Length == PageSize,
            Configurations = mapped
        };
    }

    public async Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var runtimeArtifactId = new Id(Ulid.Parse(artifactId));
        var configurations = await _configurationRepository.GetActiveByArtifactIdAsync(
            runtimeArtifactId,
            cancellationToken);

        return await MapAsync(configurations, cancellationToken);
    }

    public void Dispose()
    {
    }

    private async Task<IngressConfiguration[]> MapAsync(
        IReadOnlyCollection<RuntimeIngressConfiguration> configurations,
        CancellationToken cancellationToken)
    {
        var mapped = new List<IngressConfiguration>(configurations.Count);
        foreach (var configuration in configurations)
        {
            var artifact = await _artifactRepository.GetById(
                configuration.RuntimeOrchestrationArtifactId,
                cancellationToken);

            mapped.Add(new IngressConfiguration
            {
                Id = configuration.Id.ToString(),
                ArtifactId = configuration.RuntimeOrchestrationArtifactId.ToString(),
                OrchestrationDefinitionKey = artifact.OrchestrationDefinitionKey,
                OrchestrationVersion = artifact.Version.ToString(),
                DeployedOnUtc = artifact.DeployedOnUtc,
                IngressKind = configuration.IngressKind,
                IngressTransport = configuration.IngressTransport,
                SettingsPayload = configuration.SettingsPayload
            });
        }

        return mapped.ToArray();
    }
}
