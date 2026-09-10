namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Atlas;

/// <summary>
/// Configures the Atlas schema registry adapter.
/// </summary>
public sealed class AtlasSchemaRegistryOptions
{
    /// <summary>
    /// Gets or sets the provider key used by schema bindings.
    /// </summary>
    public string ProviderKey { get; set; } = "atlas";

    /// <summary>
    /// Gets or sets whether the Atlas adapter is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Atlas endpoint base URI once Atlas exposes schema lookup endpoints.
    /// </summary>
    public Uri BaseUri { get; set; }
}
