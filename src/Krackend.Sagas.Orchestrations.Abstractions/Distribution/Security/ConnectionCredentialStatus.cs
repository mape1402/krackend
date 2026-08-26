namespace Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

/// <summary>
/// Represents the lifecycle status of a persisted node credential.
/// </summary>
public enum ConnectionCredentialStatus
{
    /// <summary>
    /// No credential has been configured.
    /// </summary>
    Missing = 0,

    /// <summary>
    /// The credential can be used to issue tokens.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The credential was disabled without destroying its audit trail.
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// The credential was revoked and can no longer issue tokens.
    /// </summary>
    Revoked = 3
}
