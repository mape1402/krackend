namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable messaging task configuration.
/// </summary>
public sealed record MessagingTaskConfigurationArtifact(
    string Topic,
    SemanticVersion Version,
    SchemaBindingArtifact SchemaBinding) : ITaskConfigurationArtifact
{
    /// <summary>
    /// Gets messaging task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.Messaging;

    /// <summary>
    /// Gets the schema binding for the command request payload sent to the worker service.
    /// </summary>
    public SchemaBindingArtifact RequestSchemaBinding { get; init; } = SchemaBinding;

    /// <summary>
    /// Gets the schema binding for the command response payload returned by the worker service.
    /// </summary>
    public SchemaBindingArtifact ResponseSchemaBinding { get; init; }
}
