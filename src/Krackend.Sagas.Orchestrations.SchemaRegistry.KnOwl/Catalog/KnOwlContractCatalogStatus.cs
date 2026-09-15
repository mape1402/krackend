namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// Describes the outcome of a KnOwl Control Plane contract catalog call.
/// </summary>
public enum KnOwlContractCatalogStatus
{
    /// <summary>
    /// The status has not been assigned.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// One or more deployed contract artifacts were found.
    /// </summary>
    Found = 1,

    /// <summary>
    /// The requested deployed contract artifact was not found.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The returned contract artifact is not usable.
    /// </summary>
    Invalid = 3,

    /// <summary>
    /// The KnOwl Control Plane catalog could not be reached.
    /// </summary>
    Unavailable = 4
}
