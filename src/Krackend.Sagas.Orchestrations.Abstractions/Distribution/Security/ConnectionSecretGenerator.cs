namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Generates long-lived connection credentials and opaque access tokens.
/// </summary>
public interface IConnectionSecretGenerator
{
    /// <summary>
    /// Generates one connection credential.
    /// </summary>
    /// <param name="prefix">Client id prefix.</param>
    /// <returns>Generated credential material.</returns>
    ConnectionCredentialMaterial GenerateCredential(string prefix);

    /// <summary>
    /// Generates an opaque access token.
    /// </summary>
    /// <returns>Generated opaque token.</returns>
    string GenerateToken();
}
