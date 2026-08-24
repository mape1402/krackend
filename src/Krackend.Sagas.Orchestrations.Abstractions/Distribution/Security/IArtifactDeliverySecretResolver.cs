namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Resolves a signing secret from a secret reference stored in configuration or metadata.
/// </summary>
public interface IArtifactDeliverySecretResolver
{
    /// <summary>
    /// Resolves the secret value for the given reference.
    /// </summary>
    /// <param name="secretReference">Reference to the secret value.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The resolved signing secret.</returns>
    Task<string> ResolveSecret(string secretReference, CancellationToken cancellationToken = default);
}
