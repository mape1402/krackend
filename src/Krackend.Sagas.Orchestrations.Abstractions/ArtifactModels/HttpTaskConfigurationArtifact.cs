namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable HTTP task configuration.
/// </summary>
public sealed record HttpTaskConfigurationArtifact(
    SchemaBindingArtifact SchemaBinding,
    string BaseUrlVariableRef,
    string RelativePath,
    string Method,
    JsonNode HeadersTemplate,
    JsonNode QueryTemplate,
    IReadOnlyList<int> ExpectedStatusCodes,
    bool AllowSyncResponse) : ITaskConfigurationArtifact
{
    /// <summary>
    /// Gets HTTP task kind.
    /// </summary>
    public TaskKind Kind => TaskKind.Http;
}
