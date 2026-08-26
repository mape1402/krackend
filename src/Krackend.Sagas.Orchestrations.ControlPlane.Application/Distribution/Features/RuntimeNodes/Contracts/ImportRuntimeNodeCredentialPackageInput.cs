namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Represents a runtime node credential import request.
/// </summary>
public sealed class ImportRuntimeNodeCredentialPackageInput
{
    /// <summary>
    /// Gets or sets the runtime node id.
    /// </summary>
    public string RuntimeNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON or Base64 package to import.
    /// </summary>
    public string Package { get; set; } = string.Empty;
}
