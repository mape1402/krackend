using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

public sealed class ReleaseEntity
{
    public Id Id { get; set; }
    public Id ArtifactId { get; set; }
    public string OrchestrationDefinitionId { get; set; }
    public string RequestedBy { get; set; }
    public string Strategy { get; set; }
    public ReleaseStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ArtifactEntity Artifact { get; set; }
    public ICollection<ReleasePlanTargetEntity> Targets { get; set; } = new List<ReleasePlanTargetEntity>();
}

