using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

internal sealed class PermanentFailureIngressRegistry : IIngressRegistry
{
    private readonly string _message;

    public PermanentFailureIngressRegistry(string message)
    {
        _message = message;
    }

    public Task StandUpAllAsync(CancellationToken cancellationToken = default)
    {
        throw new IngressStandupConfigurationException(_message);
    }

    public Task StandUpOneAsync(
        string artifactId,
        long ingressGeneration,
        CancellationToken cancellationToken = default)
    {
        throw new IngressStandupConfigurationException(_message);
    }

    public Task ShutDownAllAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutDownOneAsync(string artifactId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
