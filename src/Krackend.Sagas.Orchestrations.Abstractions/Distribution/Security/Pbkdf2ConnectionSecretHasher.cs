using System.Security.Cryptography;
using System.Text;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// PBKDF2 SHA-256 implementation for connection secret hashing.
/// </summary>
public sealed class Pbkdf2ConnectionSecretHasher : IConnectionSecretHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const string Prefix = "pbkdf2-sha256";

    /// <inheritdoc />
    public string HashSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret cannot be empty.", nameof(secret));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join('.', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <inheritdoc />
    public bool VerifySecret(string secret, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('.', 4);
        if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
