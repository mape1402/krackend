
namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents configuration for HTTP tasks executed by the orchestration runtime.
/// </summary>
public sealed class HttpTaskConfiguration : ITaskConfiguration
{
    /// <summary>
    /// Gets http task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.Http;

    /// <summary>
    /// Gets or sets the schema binding configuration for the associated data model.
    /// </summary>
    /// <remarks>This property allows the user to define how the data model interacts with the underlying
    /// schema, which can affect data validation and serialization behavior.</remarks>
    public SchemaBinding SchemaBinding { get; set; }

    /// <summary>
    /// Gets or sets whether schema validation is enabled for this task.
    /// </summary>
    public bool HasSchemaValidation { get; set; }

    /// <summary>
    /// Gets or sets base url variable ref.
    /// </summary>
    public required string BaseUrlVariableRef { get; set; }

    /// <summary>
    /// Gets or sets relative path.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// Gets or sets method.
    /// </summary>
    public required string Method { get; set; }

    /// <summary>
    /// Gets or sets headers template.
    /// </summary>
    public JsonNode HeadersTemplate { get; set; }

    /// <summary>
    /// Gets or sets query template.
    /// </summary>
    public JsonNode QueryTemplate { get; set; }

    /// <summary>
    /// Gets or sets expected status codes.
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; } = new();

    /// <summary>
    /// Gets or sets allow sync response.
    /// </summary>
    public bool AllowSyncResponse { get; set; }
}
