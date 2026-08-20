using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IBackchannelTopicFormatter
    {
        string Format(OrchestrationArtifact artifact);
    }
}
