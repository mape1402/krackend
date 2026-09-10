namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

using Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed class StaticIngressConfigurationByArtifactAccessor : IGetIngressConfigurationByArtifactAccessor
{
    private readonly IReadOnlyCollection<IngressConfiguration> _configurations;

    public StaticIngressConfigurationByArtifactAccessor(IReadOnlyCollection<IngressConfiguration> configurations)
    {
        _configurations = configurations ?? throw new ArgumentNullException(nameof(configurations));
    }

    public Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(
            _configurations.Where(configuration => configuration.ArtifactId == artifactId).ToArray());
}
