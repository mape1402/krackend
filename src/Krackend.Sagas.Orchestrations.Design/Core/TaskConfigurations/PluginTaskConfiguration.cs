namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents configuration for plugin-based tasks.
/// </summary>
public sealed class PluginTaskConfiguration : ITaskConfiguration
{
    /// <summary>
    /// Gets plugin task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.Plugin;

    /// <summary>
    /// Gets or sets plugin id.
    /// </summary>
    public required Id PluginId { get; set; }
}
