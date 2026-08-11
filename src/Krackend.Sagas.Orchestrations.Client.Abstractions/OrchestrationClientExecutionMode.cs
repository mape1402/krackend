namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Identifies how a service execution is participating in orchestration output.
/// </summary>
public enum OrchestrationClientExecutionMode
{
    /// <summary>
    /// The execution has not been classified yet.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The execution received orchestrator metadata and should answer that runtime context.
    /// </summary>
    Orchestrated = 1,

    /// <summary>
    /// The execution did not receive orchestrator metadata and can publish only when configured per call.
    /// </summary>
    Standalone = 2
}
