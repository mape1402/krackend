using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

public sealed class ReleaseTarget
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
}

