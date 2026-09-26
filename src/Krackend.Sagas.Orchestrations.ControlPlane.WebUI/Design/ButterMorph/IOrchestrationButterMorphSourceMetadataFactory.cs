using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

/// <summary>
/// Builds ButterMorph source metadata for orchestration designer contexts.
/// </summary>
public interface IOrchestrationButterMorphSourceMetadataFactory
{
    /// <summary>
    /// Creates source metadata for the provided schema context.
    /// </summary>
    /// <param name="schemaContext">Schema context used by the designer.</param>
    /// <returns>Source metadata keyed by source alias.</returns>
    IReadOnlyDictionary<string, ButterMorphDesignerSourceMetadata> Create(OrchestrationSchemaContext schemaContext);
}
