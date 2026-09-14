namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable plugin task configuration.
/// </summary>
public sealed record PluginTaskConfigurationArtifact(Id PluginId) : ITaskConfigurationArtifact
{
    /// <summary>
    /// Gets plugin task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.Plugin;
}
