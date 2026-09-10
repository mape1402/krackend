namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text.Json.Nodes;
using global::ButterMorph.Abstractions;

/// <summary>
/// Maps ButterMorph diagnostics into orchestration execution metadata.
/// </summary>
public interface IButterMorphDiagnosticMetadataMapper
{
    /// <summary>
    /// Maps ButterMorph diagnostics into serializable metadata.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by ButterMorph.</param>
    /// <returns>Serializable orchestration diagnostics.</returns>
    IReadOnlyDictionary<string, JsonNode> Map(IReadOnlyCollection<DiagnosticEntry> diagnostics);

    /// <summary>
    /// Maps an exception into serializable metadata.
    /// </summary>
    /// <param name="exception">Exception raised by ButterMorph execution.</param>
    /// <returns>Serializable orchestration diagnostics.</returns>
    IReadOnlyDictionary<string, JsonNode> Map(Exception exception);
}
