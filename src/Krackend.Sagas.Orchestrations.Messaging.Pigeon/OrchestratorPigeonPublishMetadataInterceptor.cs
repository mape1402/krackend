using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestrations.Messaging.Pigeon;

/// <summary>
/// Adds the current orchestrator metadata object to Pigeon's publish envelope.
/// </summary>
public sealed class OrchestratorPigeonPublishMetadataInterceptor : IPublishInterceptor
{
    private readonly IOrchestratorMetadataAccessor _metadataAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorPigeonPublishMetadataInterceptor"/> class.
    /// </summary>
    /// <param name="metadataAccessor">Read-only metadata context.</param>
    public OrchestratorPigeonPublishMetadataInterceptor(IOrchestratorMetadataAccessor metadataAccessor)
    {
        _metadataAccessor = metadataAccessor ?? throw new ArgumentNullException(nameof(metadataAccessor));
    }

    /// <summary>
    /// Adds <c>Orchestrator.Metadata</c> when the current scope carries orchestration context.
    /// </summary>
    /// <param name="context">Pigeon publish context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completed value task.</returns>
    public ValueTask Intercept(PublishContext context, CancellationToken cancellationToken = default)
    {
        if (_metadataAccessor.Current is not null)
        {
            context.AddMetadata(
                OrchestratorMetadataConstants.MetadataKey,
                _metadataAccessor.Current);
        }

        return ValueTask.CompletedTask;
    }
}
