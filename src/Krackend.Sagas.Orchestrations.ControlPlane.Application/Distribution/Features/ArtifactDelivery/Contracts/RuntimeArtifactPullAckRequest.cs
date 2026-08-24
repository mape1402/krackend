namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class RuntimeArtifactPullAckRequest
{
    public string RuntimeArtifactId { get; set; }
}
