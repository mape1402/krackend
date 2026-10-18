namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Describes whether a runtime orchestration version can start new SAGA instances.
/// </summary>
public enum RuntimeOrchestrationLifecycleStatus
{
    /// <summary>
    /// The version accepts triggers and callbacks.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The version no longer starts new instances, but continues accepting
    /// callbacks for already-running instances.
    /// </summary>
    Deprecated = 2,

    /// <summary>
    /// The version is retained only as deployment history and has no ingress.
    /// </summary>
    Archived = 3
}
