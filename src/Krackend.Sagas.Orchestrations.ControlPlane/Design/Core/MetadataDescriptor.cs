namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Describes one transversal metadata group available to orchestration mappings and execution conditions.
/// </summary>
public sealed class MetadataDescriptor
{
    /// <summary>
    /// Gets or sets descriptor identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets the logical metadata key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the exact metadata key expected on incoming messages.
    /// </summary>
    public required string SourceKey { get; set; }

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets optional description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the metadata JSON schema.
    /// </summary>
    public required string SchemaJson { get; set; }

    /// <summary>
    /// Gets or sets deterministic content hash for the schema snapshot.
    /// </summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets last update timestamp in UTC.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }
}
