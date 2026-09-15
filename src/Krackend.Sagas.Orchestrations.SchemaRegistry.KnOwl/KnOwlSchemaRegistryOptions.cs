namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl;

/// <summary>
/// Configures the KnOwl Control Plane schema registry adapter.
/// </summary>
public sealed class KnOwlSchemaRegistryOptions
{
    /// <summary>
    /// Gets or sets the provider key used by Krackend schema bindings.
    /// </summary>
    public string ProviderKey { get; set; } = "knowl";

    /// <summary>
    /// Gets or sets whether the KnOwl adapter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the KnOwl Control Plane endpoint base URI.
    /// </summary>
    public Uri BaseUri { get; set; }

    /// <summary>
    /// Gets or sets the outbound HTTP timeout used by the adapter.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether a binding without an exact contract version may resolve the latest deployed version.
    /// </summary>
    public bool AllowLatestVersionResolution { get; set; }

    /// <summary>
    /// Gets or sets the schema format stored in Krackend snapshots.
    /// </summary>
    public string SchemaFormat { get; set; } = "ButterMorph";
}
