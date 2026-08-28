namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents the configuration lifecycle state of a Design node registered by Runtime.
/// </summary>
public enum RuntimeDesignNodeStatus
{
    /// <summary>
    /// The node exists but its required configuration is not complete yet.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// The node is configured and can be used by Runtime distribution flows.
    /// </summary>
    Enabled = 2,

    /// <summary>
    /// The node is configured but cannot be used until it is resumed.
    /// </summary>
    Suspend = 3
}
