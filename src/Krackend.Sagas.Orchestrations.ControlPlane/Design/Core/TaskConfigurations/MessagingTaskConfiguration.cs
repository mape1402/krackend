namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

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
    /// Gets or sets the schema binding for the command request payload.
    /// </summary>
    public SchemaBinding RequestSchemaBinding { get; set; }

    /// <summary>
    /// Gets or sets the schema binding for the command response payload.
    /// </summary>
    public SchemaBinding ResponseSchemaBinding { get; set; }

    /// <summary>
    /// Gets or sets whether schema validation is enabled for this task.
    /// </summary>
    public bool HasSchemaValidation { get; set; }

    /// <summary>
    /// Gets or sets the validation executed against the command request payload.
    /// </summary>
    public ValidationDefinition RequestValidation { get; set; }

    /// <summary>
    /// Gets or sets whether request payload validation is enabled for this task.
    /// </summary>
    public bool HasRequestValidation { get; set; }

    /// <summary>
    /// Gets or sets the validation executed against the command response payload.
    /// </summary>
    public ValidationDefinition ResponseValidation { get; set; }

    /// <summary>
    /// Gets or sets whether response payload validation is enabled for this task.
    /// </summary>
    public bool HasResponseValidation { get; set; }
}
