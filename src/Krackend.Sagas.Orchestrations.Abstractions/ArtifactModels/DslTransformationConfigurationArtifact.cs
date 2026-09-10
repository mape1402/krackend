namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable DSL transformation configuration.
/// </summary>
public sealed record DslTransformationConfigurationArtifact : ITransformationConfigurationArtifact
{
    /// <summary>
    /// Gets DSL engine.
    /// </summary>
    public EngineType Engine => EngineType.DSL;

    /// <summary>
    /// Gets the ButterMorph DSL document.
    /// </summary>
    public string Dsl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source schema context hash used when the transformation was authored.
    /// </summary>
    public string SourceContextHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target schema snapshot hash used when the transformation was authored.
    /// </summary>
    public string TargetSchemaHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last semantic validation diagnostics captured by Design.
    /// </summary>
    public string SemanticDiagnosticsJson { get; init; } = "{}";
}
