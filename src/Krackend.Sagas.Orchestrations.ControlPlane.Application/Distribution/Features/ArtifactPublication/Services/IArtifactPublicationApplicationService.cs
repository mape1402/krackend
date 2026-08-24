using Krackend.Sagas.Orchestrations.Contracts.Events;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Publishes orchestration version lifecycle artifacts into the distribution model.
/// </summary>
public interface IArtifactPublicationApplicationService
{
    /// <summary>
    /// Creates and promotes the deployment artifact for an orchestration version.
    /// </summary>
    /// <param name="deployment">Deployment data created by the design workflow.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task PublishDeployment(OrchestrationVersionDeployedEvent deployment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the deprecation artifact for an orchestration version.
    /// </summary>
    /// <param name="deprecation">Deprecation data created by the design workflow.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task PublishDeprecation(OrchestrationVersionDeprecatedEvent deprecation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the archive artifact for an orchestration version.
    /// </summary>
    /// <param name="archive">Archive data created by the design workflow.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task PublishArchive(OrchestrationVersionArchivedEvent archive, CancellationToken cancellationToken = default);
}
