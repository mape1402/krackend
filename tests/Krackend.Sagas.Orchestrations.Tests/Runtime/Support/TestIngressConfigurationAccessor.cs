namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed class TestIngressConfigurationAccessor : IGetIngressConfigurationByArtifactAccessor
{
    private readonly IRuntimeIngressConfigurationRepository _repository;

    public TestIngressConfigurationAccessor(IRuntimeIngressConfigurationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var configurations = await _repository.GetActiveByArtifactIdAsync(new Id(Ulid.Parse(artifactId)), cancellationToken);
        return configurations
            .Select(configuration => new IngressConfiguration
            {
                Id = configuration.Id.ToString(),
                ArtifactId = configuration.RuntimeOrchestrationArtifactId.ToString(),
                IngressKind = configuration.IngressKind,
                IngressTransport = configuration.IngressTransport,
                SettingsPayload = configuration.SettingsPayload
            })
            .ToArray();
    }
}
