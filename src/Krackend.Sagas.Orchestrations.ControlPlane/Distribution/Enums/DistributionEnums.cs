namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

/// <summary>
/// Represents the operational state of a runtime node registered in the control plane.
/// </summary>
public enum RuntimeNodeStatus
{
    /// <summary>
    /// Runtime node is not fully configured.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Runtime node is configured and available for release distribution.
    /// </summary>
    Enabled = 2,

    /// <summary>
    /// Runtime node is suspended and cannot be used for release distribution.
    /// </summary>
    Suspend = 3
}

/// <summary>
/// Represents who can initiate artifact distribution for a runtime node.
/// </summary>
public enum DistributionMode
{
    /// <summary>
    /// Design initiates calls and publishes artifacts into Runtime.
    /// </summary>
    DesignPublishesToRuntime = 1,

    /// <summary>
    /// Runtime initiates calls and fetches releases from Design.
    /// </summary>
    RuntimeFetchesFromDesign = 2,

    /// <summary>
    /// Design and Runtime can both initiate artifact distribution calls.
    /// </summary>
    HybridSync = 3
}

/// <summary>
/// Represents the delivery status of one release target.
/// </summary>
public enum ReleaseTargetStatus
{
    /// <summary>
    /// Release target is waiting for distribution.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Release target is available for Runtime pull.
    /// </summary>
    AvailableForPull = 2,

    /// <summary>
    /// Release target has been scheduled for Design push.
    /// </summary>
    PushScheduled = 3,

    /// <summary>
    /// Release target is being delivered.
    /// </summary>
    InProgress = 4,

    /// <summary>
    /// Artifact was delivered to Runtime.
    /// </summary>
    Delivered = 5,

    /// <summary>
    /// Runtime acknowledged a pulled artifact.
    /// </summary>
    Acknowledged = 6,

    /// <summary>
    /// Runtime activated the artifact.
    /// </summary>
    Activated = 7,

    /// <summary>
    /// Release target delivery failed.
    /// </summary>
    Failed = 8,

    /// <summary>
    /// Release target delivery was cancelled.
    /// </summary>
    Cancelled = 9
}

/// <summary>
/// Represents release orchestration status.
/// </summary>
public enum ReleaseStatus
{
    /// <summary>
    /// Release is a draft.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Release is in progress.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Release completed.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Release failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Release was cancelled.
    /// </summary>
    Cancelled = 5
}

/// <summary>
/// Represents artifact activation status in Runtime.
/// </summary>
public enum ActivationStatus
{
    /// <summary>
    /// Artifact has not been activated.
    /// </summary>
    NotActivated = 1,

    /// <summary>
    /// Artifact is activating.
    /// </summary>
    Activating = 2,

    /// <summary>
    /// Artifact was activated.
    /// </summary>
    Activated = 3,

    /// <summary>
    /// Artifact activation failed.
    /// </summary>
    ActivationFailed = 4
}

