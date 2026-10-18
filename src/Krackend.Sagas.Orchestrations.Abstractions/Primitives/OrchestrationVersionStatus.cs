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
    /// Represents a draft that is waiting for its deployment review to be decided and applied.
    /// </summary>
    PendingDeployment,
    /// <summary>
    /// Represents a version deployed to the distribution catalog.
    /// </summary>
    Deployed,
    /// <summary>
    /// Represents a deployed version that is waiting for its deprecation review to be decided and applied.
    /// </summary>
    PendingDeprecation,
    /// <summary>
    /// Represents deprecated.
    /// </summary>
    Deprecated,
    /// <summary>
    /// Represents a deprecated version that is waiting for its archive review to be decided and applied.
    /// </summary>
    PendingArchive,
    /// <summary>
    /// Represents archived.
    /// </summary>
    Archived,
    /// <summary>
    /// Represents a discarded draft that was never deployed.
    /// </summary>
    Abandoned,
    /// <summary>
    /// Legacy persisted state retained only to read data created before the review workflow.
    /// </summary>
    [Obsolete("Use PendingDeployment and an OrchestrationVersionReview instead.")]
    InReview,
    /// <summary>
    /// Legacy persisted state retained only to read data created before the review workflow.
    /// </summary>
    [Obsolete("Approval belongs to OrchestrationVersionReview instead.")]
    Approved
}
