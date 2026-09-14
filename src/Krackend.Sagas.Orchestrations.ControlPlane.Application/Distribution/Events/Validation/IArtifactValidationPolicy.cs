using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IArtifactValidationPolicy
{
    bool CanValidate(string artifactType);

    void Validate(Artifact artifact);
}
