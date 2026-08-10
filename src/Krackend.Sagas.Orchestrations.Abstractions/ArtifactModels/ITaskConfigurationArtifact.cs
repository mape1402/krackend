namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable polymorphic task-configuration contract.
/// </summary>
public interface ITaskConfigurationArtifact
{
    /// <summary>
    /// Gets task kind associated with this configuration.
    /// </summary>
    TaskKind Kind { get; }
}
