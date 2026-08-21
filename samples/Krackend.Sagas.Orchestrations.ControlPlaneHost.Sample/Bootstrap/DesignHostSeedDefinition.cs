using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Bootstrap;

internal sealed record DesignHostSeedDefinition(
    SemanticVersion Version,
    Id VersionId,
    Id TriggerId,
    Id RegistryProviderId,
    IReadOnlyList<DesignHostSeedStageDefinition> Stages);
