namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents a design node credential import request in Runtime.
/// </summary>
public sealed class ImportRuntimeDesignNodeCredentialPackageInput
{
    /// <summary>
    /// Gets or sets the design node id.
    /// </summary>
    public string DesignNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON or Base64 package to import.
    /// </summary>
    public string Package { get; set; } = string.Empty;
}
