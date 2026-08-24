namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ReleaseTargetModel
{
    public string Id { get; set; }
    public string RuntimeNodeId { get; set; }
    public string ArtifactId { get; set; }
    public string ReleaseId { get; set; }
    public string RolloutGroup { get; set; }
    public string Status { get; set; }
    public string ActivationStatus { get; set; }
    public DateTime AssignedAtUtc { get; set; }
}

public sealed class ReleaseAttemptModel
{
    public string Id { get; set; }
    public string ReleaseTargetId { get; set; }
    public string Action { get; set; }
    public string InitiatedBy { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public string ExternalReference { get; set; }
}

