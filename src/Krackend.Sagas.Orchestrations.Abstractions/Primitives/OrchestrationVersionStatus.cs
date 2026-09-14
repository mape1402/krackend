namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the orchestration version status values.
/// </summary>
public enum OrchestrationVersionStatus
{
    /// <summary>
    /// Represents draft.
    /// </summary>
    Draft,
    /// <summary>
    /// Represents in review.
    /// </summary>
    InReview,
    /// <summary>
    /// Represents approved.
    /// </summary>
    Approved,
    /// <summary>
    /// Represents deployed.
    /// </summary>
    Deployed,
    /// <summary>
    /// Represents deprecated.
    /// </summary>
    Deprecated,
    /// <summary>
    /// Represents archived.
    /// </summary>
    Archived
}
