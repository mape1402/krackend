using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

public sealed class OrchestrationAllowedRuntimeNode
{
    public Id Id { get; set; }
    public string OrchestrationDefinitionId { get; set; }
    public Id RuntimeNodeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; }
}

