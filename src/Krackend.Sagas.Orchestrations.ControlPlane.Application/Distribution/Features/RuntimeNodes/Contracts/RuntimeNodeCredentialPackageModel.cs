namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Represents a generated runtime node credential package.
/// </summary>
public sealed class RuntimeNodeCredentialPackageModel
{
    /// <summary>
    /// Gets or sets the readable JSON package.
    /// </summary>
    public string Json { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Base64 encoded package.
    /// </summary>
    public string Base64 { get; set; } = string.Empty;
}
