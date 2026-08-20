using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IRuntimeIngressConfigurationKeyBuilder
    {
        string BuildTriggerKey(TriggerBindingArtifact trigger);

        string BuildBackchannelKey(IngressTransport transport);
    }
}
