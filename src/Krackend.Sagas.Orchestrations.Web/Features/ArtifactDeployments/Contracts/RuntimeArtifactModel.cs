namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Read model for an active runtime artifact.
/// </summary>
public sealed class RuntimeArtifactModel
{
    public string Id { get; set; }
    public string EnvironmentKey { get; set; }
    public string OrchestrationDefinitionKey { get; set; }
    public string ArtifactType { get; set; }
    public string SourceOrchestrationVersionId { get; set; }
    public string Version { get; set; }
    public string Checksum { get; set; }
    public bool IsActive { get; set; }
    public DateTime DeployedOnUtc { get; set; }
    public DateTime? ActivatedOnUtc { get; set; }
    public DateTime? RetiredOnUtc { get; set; }
}
