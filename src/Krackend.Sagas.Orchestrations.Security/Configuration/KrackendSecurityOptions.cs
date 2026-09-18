namespace Krackend.Sagas.Orchestrations.Security.Configuration;

/// <summary>
/// Configures Krackend orchestration authorization behavior.
/// </summary>
public sealed class KrackendSecurityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether authenticated users must be known subjects before product access is granted.
    /// </summary>
    public bool RequireKnownSubject { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether bootstrap administrators are synchronized into persistent assignments.
    /// </summary>
    public bool AllowBootstrapAdminSync { get; set; } = true;

    /// <summary>
    /// Gets the subject resolver options.
    /// </summary>
    public KrackendSubjectResolverOptions Subject { get; } = new();

    /// <summary>
    /// Gets the configured bootstrap administrators.
    /// </summary>
    public List<KrackendBootstrapSubject> BootstrapAdmins { get; } = [];
}
