namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents a persisted ButterMorph DSL validation configuration.
/// </summary>
public sealed class DslValidationConfigurationJsonModel : ValidationConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets the ButterMorph DSL document.
    /// </summary>
    public string Dsl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the schema snapshot hash used when this validation was authored.
    /// </summary>
    public string SchemaHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last semantic validation diagnostics captured by Design.
    /// </summary>
    public string SemanticDiagnosticsJson { get; set; } = "{}";
}
