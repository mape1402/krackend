namespace Krackend.Sagas.Orchestrations.Web;

public sealed class RuntimeArtifactPullResult
{
    public bool Succeeded { get; set; }
    public int Pulled { get; set; }
    public int Activated { get; set; }
    public int Failed { get; set; }
    public IReadOnlyCollection<string> Messages { get; set; } = Array.Empty<string>();
}
