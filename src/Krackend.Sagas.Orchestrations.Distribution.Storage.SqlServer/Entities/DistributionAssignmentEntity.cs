using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;

public sealed class ReleaseTargetEntity
{
    public Id Id { get; set; }
    public Id RuntimeNodeId { get; set; }
    public Id ArtifactId { get; set; }
    public Id? ReleaseId { get; set; }
    public string RolloutGroup { get; set; }
    public ReleaseTargetStatus Status { get; set; }
    public ActivationStatus ActivationStatus { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? AvailableAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string FailureReason { get; set; }
    public string RuntimeVersionApplied { get; set; }
    public string CorrelationId { get; set; }
    public RuntimeNodeEntity RuntimeNode { get; set; }
    public ArtifactEntity Artifact { get; set; }
    public ReleaseEntity Release { get; set; }
    public ICollection<ReleaseAttemptEntity> Attempts { get; set; } = new List<ReleaseAttemptEntity>();
}

