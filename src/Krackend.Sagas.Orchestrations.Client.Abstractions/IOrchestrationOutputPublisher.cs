namespace Krackend.Sagas.Orchestrations.Client.Abstractions;

/// <summary>
/// Publishes orchestration output through an infrastructure-specific transport.
/// </summary>
public interface IOrchestrationOutputPublisher
{
    /// <summary>
    /// Publishes orchestration output.
    /// </summary>
    Task<OrchestrationPublishResult> Publish(OrchestrationPublishRequest request, CancellationToken cancellationToken = default);
}
