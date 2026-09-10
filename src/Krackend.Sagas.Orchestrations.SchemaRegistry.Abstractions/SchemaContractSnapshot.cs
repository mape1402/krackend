namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Represents an immutable schema document resolved from a registry for a published artifact.
/// </summary>
public sealed record SchemaContractSnapshot
{
    /// <summary>
    /// Gets the contract reference used to resolve the snapshot.
    /// </summary>
    public SchemaContractReference Reference { get; init; } = new();

    /// <summary>
    /// Gets the schema document format, such as ButterMorph or JsonSchema.
    /// </summary>
    public string SchemaFormat { get; init; } = "ButterMorph";

    /// <summary>
    /// Gets the schema JSON document.
    /// </summary>
    public string SchemaJson { get; init; } = "{}";

    /// <summary>
    /// Gets the schema content hash.
    /// </summary>
    public string ContentHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider-specific source artifact id, when available.
    /// </summary>
    public string SourceArtifactId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider that resolved the snapshot.
    /// </summary>
    public string ResolvedBy { get; init; } = string.Empty;

    /// <summary>
    /// Gets when the snapshot was resolved.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
