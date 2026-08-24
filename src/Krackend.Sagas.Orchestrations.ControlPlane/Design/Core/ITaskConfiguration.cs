namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the polymorphic contract for task-specific configuration payloads at design time.
/// </summary>
public interface ITaskConfiguration
{
    /// <summary>
    /// Gets task kind associated with this configuration.
    /// </summary>
    public TaskKind Kind { get; }
}
