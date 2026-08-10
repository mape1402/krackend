using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

public sealed class RuntimeCapabilityEntity
{
    public Id Id { get; set; }
    public Id RuntimeNodeId { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public RuntimeNodeEntity RuntimeNode { get; set; }
}
