namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Abstractions;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Converts orchestration schema snapshots into ButterMorph structure schemas.
/// </summary>
public interface IOrchestrationButterMorphSchemaImporter
{
    /// <summary>
    /// Converts one schema binding into the schema type expected by the ButterMorph designer.
    /// </summary>
    /// <param name="binding">Schema binding with a resolved snapshot.</param>
    /// <param name="schema">Imported ButterMorph schema when conversion succeeds.</param>
    /// <param name="message">Diagnostic message when conversion fails.</param>
    /// <returns>True when the binding contains a usable schema snapshot.</returns>
    bool TryImport(SchemaBinding binding, out IStructureSchema schema, out string message);
}
