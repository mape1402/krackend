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
}
