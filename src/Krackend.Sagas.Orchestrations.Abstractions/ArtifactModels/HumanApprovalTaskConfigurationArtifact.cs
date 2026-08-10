namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable human-approval task configuration.
/// </summary>
public sealed record HumanApprovalTaskConfigurationArtifact : ITaskConfigurationArtifact
{
    /// <summary>
    /// Gets human-approval task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.HumanApproval;
}
