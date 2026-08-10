namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Represents the trigger intake status values.
/// </summary>
public enum TriggerIntakeStatus
{
    /// <summary>
    /// Represents received.
    /// </summary>
    Received,
    /// <summary>
    /// Represents buffered.
    /// </summary>
    Buffered,
    /// <summary>
    /// Represents persisted primary.
    /// </summary>
    PersistedPrimary,
    /// <summary>
    /// Represents persisted secondary.
    /// </summary>
    PersistedSecondary,
    /// <summary>
    /// Represents promoted to runtime.
    /// </summary>
    PromotedToRuntime,
    /// <summary>
    /// Represents rejected.
    /// </summary>
    Rejected,
    /// <summary>
    /// Represents expired.
    /// </summary>
    Expired,
    /// <summary>
    /// Represents failed.
    /// </summary>
    Failed
}
