using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Pigeon.Messaging.Consuming.Dispatching;

namespace Krackend.Sagas.Orchestrations.Messaging.Pigeon;

/// <summary>
/// Reads orchestrator metadata from the Pigeon envelope and exposes it through the scoped metadata context.
/// </summary>
public sealed class OrchestratorPigeonConsumeMetadataInterceptor : IConsumeInterceptor
{
    private readonly IOrchestratorMetadataWriter _metadataWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorPigeonConsumeMetadataInterceptor"/> class.
    /// </summary>
    /// <param name="metadataWriter">Write-side metadata context.</param>
    public OrchestratorPigeonConsumeMetadataInterceptor(IOrchestratorMetadataWriter metadataWriter)
    {
        _metadataWriter = metadataWriter ?? throw new ArgumentNullException(nameof(metadataWriter));
    }

    /// <summary>
    /// Hydrates the current scoped metadata from Pigeon's raw metadata dictionary.
    /// </summary>
    /// <param name="context">Pigeon consume context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completed value task.</returns>
    public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
    {
        _metadataWriter.Clear();

        if (context.RawMetadata is not null
            && context.RawMetadata.ContainsKey(OrchestratorMetadataConstants.MetadataKey))
        {
            var metadata = context.GetMetadata<OrchestratorMessageMetadata>(
                OrchestratorMetadataConstants.MetadataKey);
            _metadataWriter.Set(metadata);
        }

        return ValueTask.CompletedTask;
    }
}
