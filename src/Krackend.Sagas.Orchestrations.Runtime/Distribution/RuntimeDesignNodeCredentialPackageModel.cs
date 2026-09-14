namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents a generated design node credential package from Runtime.
/// </summary>
public sealed class RuntimeDesignNodeCredentialPackageModel
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
