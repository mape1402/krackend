namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl;

/// <summary>
/// Configures the KnOwl Control Plane schema registry adapter.
/// </summary>
public sealed class KnOwlSchemaRegistryOptions
{
    /// <summary>
    /// Gets the default deployed catalog path exposed by KnOwl Control Plane.
    /// </summary>
    public const string DefaultCatalogPath = "contracts/artifacts";

    /// <summary>
    /// Gets the default exact event contract path exposed by KnOwl Control Plane.
    /// </summary>
    public const string DefaultEventContractPath = "contracts/events/{contractKey}/versions/{versionNumber}";

    /// <summary>
    /// Gets the default latest event contract path exposed by KnOwl Control Plane.
    /// </summary>
    public const string DefaultLatestEventContractPath = "contracts/events/{contractKey}/latest";

    /// <summary>
    /// Gets the default exact command contract path exposed by KnOwl Control Plane.
    /// </summary>
    public const string DefaultCommandContractPath = "contracts/commands/{contractKey}/versions/{versionNumber}";

    /// <summary>
    /// Gets the default latest command contract path exposed by KnOwl Control Plane.
    /// </summary>
    public const string DefaultLatestCommandContractPath = "contracts/commands/{contractKey}/latest";

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
    /// Gets or sets the relative path used to read the deployed contract artifact catalog.
    /// </summary>
    public string CatalogPath { get; set; } = DefaultCatalogPath;

    /// <summary>
    /// Gets or sets the relative path template used to read an exact deployed event contract.
    /// </summary>
    public string EventContractPath { get; set; } = DefaultEventContractPath;

    /// <summary>
    /// Gets or sets the relative path template used to read the latest deployed event contract.
    /// </summary>
    public string LatestEventContractPath { get; set; } = DefaultLatestEventContractPath;

    /// <summary>
    /// Gets or sets the relative path template used to read an exact deployed command contract.
    /// </summary>
    public string CommandContractPath { get; set; } = DefaultCommandContractPath;

    /// <summary>
    /// Gets or sets the relative path template used to read the latest deployed command contract.
    /// </summary>
    public string LatestCommandContractPath { get; set; } = DefaultLatestCommandContractPath;

    /// <summary>
    /// Gets or sets whether a binding without an exact contract version may resolve the latest deployed version.
    /// </summary>
    public bool AllowLatestVersionResolution { get; set; }

    /// <summary>
    /// Gets or sets the schema format stored in Krackend snapshots.
    /// </summary>
    public string SchemaFormat { get; set; } = "ButterMorph";
}
