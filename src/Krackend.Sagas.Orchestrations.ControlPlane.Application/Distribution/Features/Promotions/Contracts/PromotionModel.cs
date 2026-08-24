namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ReleaseModel
{
    public string Id { get; set; }
    public string ArtifactId { get; set; }
    public string OrchestrationDefinitionId { get; set; }
    public string RequestedBy { get; set; }
    public string Strategy { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public IReadOnlyCollection<ReleasePlanTargetModel> Targets { get; set; } = Array.Empty<ReleasePlanTargetModel>();
}

public sealed class ReleasePlanTargetModel
{
    public string Id { get; set; }
    public string RuntimeNodeId { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Notes { get; set; }
}

public sealed record CreateReleaseInput(
    string ArtifactId,
    string RequestedBy,
    string Strategy,
    IReadOnlyCollection<string> RuntimeNodeIds,
    string Notes);

