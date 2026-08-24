namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Defines HTTP headers used to authenticate artifact delivery requests.
/// </summary>
public static class ArtifactDeliverySecurityHeaders
{
    /// <summary>
    /// Header containing the signing key identifier.
    /// </summary>
    public const string KeyId = "X-Krackend-Key-Id";

    /// <summary>
    /// Header containing the request timestamp in Unix seconds.
    /// </summary>
    public const string Timestamp = "X-Krackend-Timestamp";

    /// <summary>
    /// Header containing a unique request nonce.
    /// </summary>
    public const string Nonce = "X-Krackend-Nonce";

    /// <summary>
    /// Header containing the SHA-256 hash of the request body.
    /// </summary>
    public const string BodyHash = "X-Krackend-Body-Sha256";

    /// <summary>
    /// Header containing the HMAC-SHA256 request signature.
    /// </summary>
    public const string Signature = "X-Krackend-Signature";
}
