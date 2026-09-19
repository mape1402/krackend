using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlaneHost.Mongo.Sample.Bootstrap;

internal sealed record DesignHostSeedTaskDefinition(
    Id Id,
    string Key,
    string Name,
    int Order,
    string Topic,
    string TransformationDsl = null);
