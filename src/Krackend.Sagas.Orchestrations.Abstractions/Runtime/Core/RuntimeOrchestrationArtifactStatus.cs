namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Represents the installation status of a runtime orchestration artifact.
/// </summary>
public enum RuntimeOrchestrationArtifactStatus
{
    /// <summary>
    /// The artifact was accepted by the runtime, but its ingress configuration has not been projected yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The artifact was projected successfully and is ready to be applied by runtime replicas.
    /// </summary>
    Ready = 1,

    /// <summary>
    /// The artifact projection failed and must be retried or replaced.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The artifact is no longer available for new orchestration work.
    /// </summary>
    Retired = 3
}
