using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IArtifactBuilder<in TEvent>
{
    Artifact Build(TEvent integrationEvent);
}
