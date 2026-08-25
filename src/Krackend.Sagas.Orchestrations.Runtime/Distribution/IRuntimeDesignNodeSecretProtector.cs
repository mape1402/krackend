namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Protects and unprotects shared secrets captured for runtime design nodes.
/// </summary>
public interface IRuntimeDesignNodeSecretProtector
{
    /// <summary>
    /// Protects a plain text secret before storage.
    /// </summary>
    string Protect(string secret);

    /// <summary>
    /// Unprotects a stored secret for signing or validation.
    /// </summary>
    string Unprotect(string protectedSecret);
}
