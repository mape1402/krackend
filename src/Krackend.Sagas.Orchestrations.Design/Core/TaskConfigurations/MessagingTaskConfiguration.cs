namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents configuration for messaging tasks.
/// </summary>
public sealed class MessagingTaskConfiguration : ITaskConfiguration
{
    /// <summary>
    /// Gets messaging task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.Messaging;

    /// <summary>
    /// Gets or sets topic.
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// Gets or sets the semantic version of the software component.
    /// </summary>
    /// <remarks>The version follows the semantic versioning format (Major.Minor.Patch), which is used to
    /// indicate compatibility and track changes between releases. Setting this property allows consumers to identify
    /// the specific version of the component for purposes such as dependency management and upgrade planning.</remarks>
    public SemanticVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the schema binding configuration for the associated data model.
    /// </summary>
    /// <remarks>This property allows customization of how the data model interacts with the underlying
    /// schema. Ensure that the schema binding is correctly configured to avoid runtime errors.</remarks>
    public SchemaBinding SchemaBinding { get; set; }

    /// <summary>
    /// Gets or sets whether schema validation is enabled for this task.
    /// </summary>
    public bool HasSchemaValidation { get; set; }
}
