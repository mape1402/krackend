namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Describes how a materialized artifact changes runtime consumers.
/// </summary>
internal enum RuntimeArtifactLifecycle
{
    /// <summary>
    /// Registers trigger and back-channel consumers.
    /// </summary>
    Deploy,

    /// <summary>
    /// Removes trigger consumers while preserving the back channel for running instances.
    /// </summary>
    Deprecated,

    /// <summary>
    /// Removes trigger and back-channel consumers.
    /// </summary>
    Archived
}
