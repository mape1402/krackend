namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Runtime ingress synchronization options.
/// </summary>
public sealed class RuntimeIngressSynchronizationOptions
{
    /// <summary>
    /// Gets or sets active artifact page size.
    /// </summary>
    public int ActiveArtifactPageSize { get; set; } = 128;
}
