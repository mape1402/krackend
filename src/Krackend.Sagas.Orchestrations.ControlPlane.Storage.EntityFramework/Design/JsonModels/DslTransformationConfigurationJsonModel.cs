namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents DslTransformationConfigurationJsonModel.
/// </summary>
public sealed class DslTransformationConfigurationJsonModel : TransformationConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets the ButterMorph DSL document.
    /// </summary>
    public string Dsl { get; set; }

    /// <summary>
    /// Gets or sets the source schema context hash used when the transformation was authored.
    /// </summary>
    public string SourceContextHash { get; set; }

    /// <summary>
    /// Gets or sets the target schema snapshot hash used when the transformation was authored.
    /// </summary>
    public string TargetSchemaHash { get; set; }

    /// <summary>
    /// Gets or sets semantic diagnostics JSON captured by Design.
    /// </summary>
    public string SemanticDiagnosticsJson { get; set; }
}
