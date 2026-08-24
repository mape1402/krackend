using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

public sealed class Release
{
    public Id Id { get; set; }
    public Id ArtifactId { get; set; }
    public string OrchestrationDefinitionId { get; set; }
    public string RequestedBy { get; set; }
    public string Strategy { get; set; }
    public ReleaseStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

