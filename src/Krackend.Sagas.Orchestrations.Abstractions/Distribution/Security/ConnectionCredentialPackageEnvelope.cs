namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents a credential package serialized as JSON and Base64 for copy and import flows.
/// </summary>
public sealed class ConnectionCredentialPackageEnvelope
{
    /// <summary>
    /// Gets or sets the readable JSON representation.
    /// </summary>
    public string Json { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Base64 encoded JSON representation.
    /// </summary>
    public string Base64 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parsed package.
    /// </summary>
    public ConnectionCredentialPackage Package { get; set; } = new();
}
