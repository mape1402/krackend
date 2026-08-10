namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the task kind values.
/// </summary>
public enum TaskKind
{
    /// <summary>
    /// Represents messaging.
    /// </summary>
    Messaging,
    /// <summary>
    /// Represents http.
    /// </summary>
    Http,
    /// <summary>
    /// Represents plugin.
    /// </summary>
    Plugin,
    /// <summary>
    /// Represents human approval.
    /// </summary>
    HumanApproval
}
