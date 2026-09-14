namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;

/// <summary>
/// Converts ButterMorph diagnostics into orchestration metadata payloads.
/// </summary>
public sealed class ButterMorphDiagnosticMetadataMapper : IButterMorphDiagnosticMetadataMapper
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, JsonNode> Map(IReadOnlyCollection<DiagnosticEntry> diagnostics)
    {
        var array = new JsonArray();
        foreach (var diagnostic in diagnostics ?? Array.Empty<DiagnosticEntry>())
        {
            array.Add(new JsonObject
            {
                ["code"] = diagnostic.Code,
                ["message"] = diagnostic.Message,
                ["path"] = diagnostic.Path,
                ["severity"] = diagnostic.Severity
            });
        }

        return new Dictionary<string, JsonNode>
        {
            ["diagnostics"] = array
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, JsonNode> Map(Exception exception)
        => new Dictionary<string, JsonNode>
        {
            ["exceptionType"] = JsonValue.Create(exception.GetType().FullName),
            ["message"] = JsonValue.Create(exception.Message)
        };
}
