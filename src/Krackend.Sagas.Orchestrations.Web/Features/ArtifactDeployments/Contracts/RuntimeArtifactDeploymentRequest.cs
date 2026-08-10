namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Request used to materialize an orchestration artifact in a runtime environment.
/// </summary>
public sealed class RuntimeArtifactDeploymentRequest
{
    public string ArtifactId { get; set; }
    public string ArtifactType { get; set; }
    public string SchemaVersion { get; set; }
    public string EnvironmentKey { get; set; }
    public string OrchestrationDefinitionId { get; set; }
    public string OrchestrationVersionId { get; set; }
    public string OrchestrationDefinitionKey { get; set; }
    public string Version { get; set; }
    public string Checksum { get; set; }
    public string PayloadJson { get; set; }
    public string CorrelationId { get; set; }
    public string PromotedBy { get; set; }
    public DateTime PromotedOnUtc { get; set; }
}
