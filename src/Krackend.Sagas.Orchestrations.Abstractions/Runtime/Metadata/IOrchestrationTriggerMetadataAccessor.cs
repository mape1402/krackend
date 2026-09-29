namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Provides Krackend-defined metadata for the trigger event produced by the current service scope.
/// </summary>
public interface IOrchestrationTriggerMetadataAccessor
{
    /// <summary>
    /// Gets trigger metadata for the current service scope.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trigger metadata. Empty metadata keeps the current behavior.</returns>
    ValueTask<OrchestrationTriggerMetadata> GetAsync(CancellationToken cancellationToken = default);
}
