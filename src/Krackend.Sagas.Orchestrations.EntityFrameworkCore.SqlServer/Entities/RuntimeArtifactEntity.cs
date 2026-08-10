using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class RuntimeArtifactEntity
{
    public Id Id { get; set; }
    public string EnvironmentKey { get; set; }
    public string OrchestrationDefinitionKey { get; set; }
    public string ArtifactType { get; set; }
    public Id SourceOrchestrationVersionId { get; set; }
    public string Version { get; set; }
    public string ArtifactChecksum { get; set; }
    public string ArtifactPayloadJson { get; set; }
    public bool IsActive { get; set; }
    public bool LoadedToCache { get; set; }
    public DateTime DeployedOnUtc { get; set; }
    public DateTime? ActivatedOnUtc { get; set; }
    public DateTime? RetiredOnUtc { get; set; }
    public Id? SupersededByArtifactId { get; set; }
    public string Notes { get; set; }
}
