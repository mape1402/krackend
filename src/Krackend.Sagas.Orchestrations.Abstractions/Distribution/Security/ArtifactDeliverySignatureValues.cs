namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Contains the values that make up a signed artifact delivery request.
/// </summary>
public sealed record ArtifactDeliverySignatureValues(
    string KeyId,
    string Timestamp,
    string Nonce,
    string BodyHash,
    string Signature);
