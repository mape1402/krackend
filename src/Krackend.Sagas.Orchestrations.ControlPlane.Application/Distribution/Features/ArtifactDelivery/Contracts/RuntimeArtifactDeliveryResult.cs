namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class RuntimeArtifactDeliveryResult
{
    public bool Succeeded { get; set; }
    public string ReleaseTargetId { get; set; }
    public string RuntimeNodeId { get; set; }
    public string ArtifactId { get; set; }
    public string Status { get; set; }
    public string Message { get; set; }
    public string ExternalReference { get; set; }
}
