using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IArtifactValidationPolicy
{
    bool CanValidate(string artifactType);

    void Validate(Artifact artifact);
}
