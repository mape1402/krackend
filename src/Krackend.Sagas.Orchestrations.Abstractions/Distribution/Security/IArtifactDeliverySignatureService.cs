namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Creates and validates HMAC signatures for artifact delivery requests.
/// </summary>
public interface IArtifactDeliverySignatureService
{
    /// <summary>
    /// Creates signature values for a request.
    /// </summary>
    ArtifactDeliverySignatureValues Sign(
        string method,
        string pathAndQuery,
        string body,
        string keyId,
        string secret,
        DateTimeOffset timestamp,
        string nonce);

    /// <summary>
    /// Validates signature values for a request.
    /// </summary>
    ArtifactDeliverySignatureValidationResult Validate(
        string method,
        string pathAndQuery,
        string body,
        ArtifactDeliverySignatureValues values,
        string expectedKeyId,
        string secret,
        DateTimeOffset now,
        TimeSpan allowedClockSkew);

    /// <summary>
    /// Computes the body hash value used by the signature.
    /// </summary>
    string ComputeBodyHash(string body);
}
