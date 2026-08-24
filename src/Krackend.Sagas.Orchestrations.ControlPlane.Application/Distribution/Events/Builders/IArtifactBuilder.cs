using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IArtifactBuilder<in TEvent>
{
    Artifact Build(TEvent integrationEvent);
}
