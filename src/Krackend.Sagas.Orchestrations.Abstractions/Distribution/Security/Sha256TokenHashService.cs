using System.Security.Cryptography;
using System.Text;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// SHA-256 implementation for token cache key hashing.
/// </summary>
public sealed class Sha256TokenHashService : ITokenHashService
{
    /// <inheritdoc />
    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty.", nameof(token));
        }

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
