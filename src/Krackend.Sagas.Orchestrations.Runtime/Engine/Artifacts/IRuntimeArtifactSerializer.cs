using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    public interface IRuntimeArtifactSerializer
    {
        OrchestrationArtifact Deserialize(string payload);
    }
}
