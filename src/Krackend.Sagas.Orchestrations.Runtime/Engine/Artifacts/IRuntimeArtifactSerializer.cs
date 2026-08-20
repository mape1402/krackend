using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal interface IRuntimeArtifactSerializer
    {
        OrchestrationArtifact Deserialize(string payload);
    }
}
