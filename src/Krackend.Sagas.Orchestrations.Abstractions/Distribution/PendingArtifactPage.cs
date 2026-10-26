namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution;

/// <summary>A page of releases available for manual import by a runtime node.</summary>
public sealed class PendingArtifactPage
{
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public long TotalRows { get; set; }
    public IReadOnlyCollection<PendingArtifactSummary> Items { get; set; } = Array.Empty<PendingArtifactSummary>();
}

/// <summary>Information needed to identify a pending release without transferring its payload.</summary>
public sealed class PendingArtifactSummary
{
    public string ReleaseId { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string ReleaseStrategy { get; set; } = string.Empty;
    public string ReleaseTargetId { get; set; } = string.Empty;
    public string OrchestrationDefinitionKey { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public string PromotedBy { get; set; } = string.Empty;
    public DateTime PromotedOnUtc { get; set; }
}
