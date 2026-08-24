namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents payload transformation behavior applied before or after task execution.
/// </summary>
public sealed class TransformationDefinition
{
    /// <summary>
    /// Gets or sets engine.
    /// </summary>
    public required EngineType Engine { get; set; }

    /// <summary>
    /// Gets or sets the configuration settings used for the transformation process.
    /// </summary>
    /// <remarks>Ensure that the configuration object is properly initialized before use. Modifying this
    /// property affects how transformations are performed by the component.</remarks>
    public ITransformationConfiguration Configuration { get; set; }
}
