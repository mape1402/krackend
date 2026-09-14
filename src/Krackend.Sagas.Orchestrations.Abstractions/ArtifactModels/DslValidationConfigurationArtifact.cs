namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable ButterMorph DSL validation configuration.
/// </summary>
public sealed record DslValidationConfigurationArtifact : IValidationConfigurationArtifact
{
    /// <summary>
    /// Gets the DSL engine.
    /// </summary>
    public EngineType Engine => EngineType.DSL;

    /// <summary>
    /// Gets the ButterMorph DSL document used to validate the payload.
    /// </summary>
    public string Dsl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the schema snapshot hash used when the validation rule was authored.
    /// </summary>
    public string SchemaHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last semantic validation diagnostics captured by Design.
    /// </summary>
    public string SemanticDiagnosticsJson { get; init; } = "{}";
}
