namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Describes the outcome of a schema contract resolution request.
/// </summary>
public enum SchemaContractResolutionStatus
{
    /// <summary>
    /// The resolver has not assigned a status.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A schema snapshot was resolved successfully.
    /// </summary>
    Resolved = 1,

    /// <summary>
    /// No resolver or provider is configured for the requested contract.
    /// </summary>
    NotConfigured = 2,

    /// <summary>
    /// The requested contract could not be found.
    /// </summary>
    NotFound = 3,

    /// <summary>
    /// The requested contract is invalid.
    /// </summary>
    Invalid = 4,

    /// <summary>
    /// The registry could not be reached due to a transient failure.
    /// </summary>
    Unavailable = 5
}
