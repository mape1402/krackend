namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class ArtifactModel
{
    public string Id { get; set; }
    public string OrchestrationDefinitionId { get; set; }
    public string OrchestrationVersionId { get; set; }
    public string OrchestrationDisplayName { get; set; }
    public string VersionLabel { get; set; }
    public string VersionNumber { get; set; }
    public string ArtifactType { get; set; }
    public string SchemaVersion { get; set; }
    public string Payload { get; set; }
    public string Metadata { get; set; }
    public string SourceEvent { get; set; }
    public string SourceVersion { get; set; }
    public string Checksum { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}

