namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a DSL-based transformation configuration.
/// </summary>
public class DslTransformationConfiguration : ITransformationConfiguration
{
    /// <summary>
    /// Gets the engine used to execute the transformation.
    /// </summary>
    public EngineType Engine => EngineType.DSL;

    /// <summary>
    /// Gets or sets the ButterMorph DSL document.
    /// </summary>
    public string Dsl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source schema context hash used when the transformation was authored.
    /// </summary>
    public string SourceContextHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target schema snapshot hash used when the transformation was authored.
    /// </summary>
    public string TargetSchemaHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last semantic validation diagnostics captured by Design.
    /// </summary>
    public string SemanticDiagnosticsJson { get; set; } = "{}";
}
