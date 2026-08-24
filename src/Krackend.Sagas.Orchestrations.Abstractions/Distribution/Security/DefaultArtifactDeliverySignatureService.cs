using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Default HMAC-SHA256 implementation for artifact delivery signatures.
/// </summary>
public sealed class DefaultArtifactDeliverySignatureService : IArtifactDeliverySignatureService
{
    /// <inheritdoc />
    public ArtifactDeliverySignatureValues Sign(
        string method,
        string pathAndQuery,
        string body,
        string keyId,
        string secret,
        DateTimeOffset timestamp,
        string nonce)
    {
        ValidateRequired(keyId, nameof(keyId));
        ValidateRequired(secret, nameof(secret));
        ValidateRequired(nonce, nameof(nonce));

        var bodyHash = ComputeBodyHash(body);
        var timestampValue = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = ComputeSignature(method, pathAndQuery, timestampValue, nonce, bodyHash, secret);

        return new ArtifactDeliverySignatureValues(keyId, timestampValue, nonce, bodyHash, signature);
    }

    /// <inheritdoc />
    public ArtifactDeliverySignatureValidationResult Validate(
        string method,
        string pathAndQuery,
        string body,
        ArtifactDeliverySignatureValues values,
        string expectedKeyId,
        string secret,
        DateTimeOffset now,
        TimeSpan allowedClockSkew)
    {
        if (values is null)
        {
            return ArtifactDeliverySignatureValidationResult.Failure("Signature headers are missing.");
        }

        if (!string.Equals(values.KeyId, expectedKeyId, StringComparison.Ordinal))
        {
            return ArtifactDeliverySignatureValidationResult.Failure("Signature key identifier is invalid.");
        }

        if (!long.TryParse(values.Timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTimestamp))
        {
            return ArtifactDeliverySignatureValidationResult.Failure("Signature timestamp is invalid.");
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        if (timestamp < now.Subtract(allowedClockSkew) || timestamp > now.Add(allowedClockSkew))
        {
            return ArtifactDeliverySignatureValidationResult.Failure("Signature timestamp is outside the allowed clock skew.");
        }

        var expectedBodyHash = ComputeBodyHash(body);
        if (!SecureEquals(values.BodyHash, expectedBodyHash))
        {
            return ArtifactDeliverySignatureValidationResult.Failure("Signature body hash is invalid.");
        }

        var expectedSignature = ComputeSignature(
            method,
            pathAndQuery,
            values.Timestamp,
            values.Nonce,
            values.BodyHash,
            secret);

        return SecureEquals(values.Signature, expectedSignature)
            ? ArtifactDeliverySignatureValidationResult.Success()
            : ArtifactDeliverySignatureValidationResult.Failure("Signature value is invalid.");
    }

    /// <inheritdoc />
    public string ComputeBodyHash(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }

    private static string ComputeSignature(
        string method,
        string pathAndQuery,
        string timestamp,
        string nonce,
        string bodyHash,
        string secret)
    {
        var canonical = string.Join(
            '\n',
            (method ?? string.Empty).Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(pathAndQuery) ? "/" : pathAndQuery,
            timestamp ?? string.Empty,
            nonce ?? string.Empty,
            bodyHash ?? string.Empty);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static void ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }
    }
}
