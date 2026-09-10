namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Validates DSL-backed artifact configuration before an orchestration version is published.
/// </summary>
public interface IOrchestrationArtifactDslValidationService
{
    /// <summary>
    /// Validates the DSL content captured by the orchestration version.
    /// </summary>
    /// <param name="version">Version snapshot to validate.</param>
    void Validate(OrchestrationVersion version);
}
