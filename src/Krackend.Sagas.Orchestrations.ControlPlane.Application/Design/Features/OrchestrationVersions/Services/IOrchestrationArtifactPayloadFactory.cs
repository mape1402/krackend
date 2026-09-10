using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Builds immutable orchestration artifact payloads from design definitions.
/// </summary>
public interface IOrchestrationArtifactPayloadFactory
{
    /// <summary>
    /// Creates the artifact payload JSON for an orchestration version.
    /// </summary>
    /// <param name="definition">Orchestration definition.</param>
    /// <param name="version">Orchestration version snapshot.</param>
    /// <returns>Serialized artifact payload.</returns>
    string CreatePayloadJson(
        OrchestrationDefinition definition,
        OrchestrationVersion version);
}
