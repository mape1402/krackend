using System.Security.Cryptography;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Secure random implementation for credential and token generation.
/// </summary>
public sealed class SecureConnectionSecretGenerator : IConnectionSecretGenerator
{
    /// <inheritdoc />
    public ConnectionCredentialMaterial GenerateCredential(string prefix)
    {
        var cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? "node" : prefix.Trim().ToLowerInvariant();
        return new ConnectionCredentialMaterial
        {
            ClientId = $"{cleanPrefix}-{Ulid.NewUlid()}",
            ClientSecret = GenerateToken(),
            KeyId = $"key-{Ulid.NewUlid()}"
        };
    }

    /// <inheritdoc />
    public string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
