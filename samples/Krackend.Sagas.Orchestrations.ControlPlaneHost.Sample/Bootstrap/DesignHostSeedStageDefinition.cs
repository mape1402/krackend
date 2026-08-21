using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Bootstrap;

internal sealed record DesignHostSeedStageDefinition(
    Id Id,
    string Key,
    string Name,
    int Order,
    string Description,
    IReadOnlyList<DesignHostSeedTaskDefinition> Tasks);
