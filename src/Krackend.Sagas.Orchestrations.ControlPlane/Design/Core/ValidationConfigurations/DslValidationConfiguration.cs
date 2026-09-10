namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a ButterMorph DSL payload validation configuration.
/// </summary>
public sealed class DslValidationConfiguration : IValidationConfiguration
{
    /// <summary>
    /// Gets the engine used to execute the validation.
    /// </summary>
    public EngineType Engine => EngineType.DSL;

    /// <summary>
    /// Gets or sets the ButterMorph DSL document.
    /// </summary>
    public string Dsl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema snapshot hash used when the validation was authored.
    /// </summary>
    public string SchemaHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last semantic validation diagnostics captured by Design.
    /// </summary>
    public string SemanticDiagnosticsJson { get; set; } = "{}";
}
