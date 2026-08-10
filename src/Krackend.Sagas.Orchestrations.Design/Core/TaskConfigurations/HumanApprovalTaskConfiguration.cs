namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents configuration for human-approval tasks.
/// </summary>
public sealed class HumanApprovalTaskConfiguration : ITaskConfiguration
{
    /// <summary>
    /// Gets human approval task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.HumanApproval;
}
