namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Represents interaction data for orchestration metadata descriptors.
/// </summary>
public sealed class MetadataDescriptorModel
{
    /// <summary>
    /// Gets or sets identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets metadata key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets incoming message metadata key.
    /// </summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets JSON schema.
    /// </summary>
    public string SchemaJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets schema content hash.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

}
