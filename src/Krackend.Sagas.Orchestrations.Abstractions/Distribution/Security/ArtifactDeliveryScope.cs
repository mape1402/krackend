namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Defines scopes used by Design and Runtime artifact distribution calls.
/// </summary>
public enum ArtifactDeliveryScope
{
    /// <summary>
    /// Allows a Design node to push artifacts into a Runtime node.
    /// </summary>
    ArtifactPush = 1,

    /// <summary>
    /// Allows a Runtime node to read release targets exposed by a Design node.
    /// </summary>
    ReleaseRead = 2,

    /// <summary>
    /// Allows a Runtime node to read artifact packages exposed by a Design node.
    /// </summary>
    ArtifactRead = 3,

    /// <summary>
    /// Allows a Runtime node to acknowledge pulled artifacts back to a Design node.
    /// </summary>
    ArtifactAcknowledge = 4,

    /// <summary>
    /// Allows either side to validate a configured connection.
    /// </summary>
    ConnectionValidate = 5
}
