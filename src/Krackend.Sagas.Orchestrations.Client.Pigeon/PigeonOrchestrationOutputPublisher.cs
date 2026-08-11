using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestrations.Client.Pigeon;

/// <summary>
/// Publishes orchestration client output through Pigeon.
/// </summary>
public sealed class PigeonOrchestrationOutputPublisher : IOrchestrationOutputPublisher
{
    private readonly IProducer _producer;
    private readonly IOrchestratorMetadataWriter _metadataWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PigeonOrchestrationOutputPublisher"/> class.
    /// </summary>
    public PigeonOrchestrationOutputPublisher(
        IProducer producer,
        IOrchestratorMetadataWriter metadataWriter)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _metadataWriter = metadataWriter ?? throw new ArgumentNullException(nameof(metadataWriter));
    }

    /// <inheritdoc/>
    public async Task<OrchestrationPublishResult> Publish(OrchestrationPublishRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var runtimeMetadata = PigeonOrchestrationMetadataMapper.ToRuntimeMetadata(request.Metadata);
            if (runtimeMetadata is not null)
                _metadataWriter.Set(runtimeMetadata);

            await _producer.PublishAsync(
                request.Payload,
                request.Topic,
                request.Version.ToPigeonSemanticVersion(),
                cancellationToken);

            return new OrchestrationPublishResult
            {
                Succeeded = true,
                Status = "Dispatched",
                ExternalReference = request.Topic
            };
        }
        catch (Exception ex)
        {
            return new OrchestrationPublishResult
            {
                Succeeded = false,
                Status = "Failed",
                FailureReason = ex.Message
            };
        }
        finally
        {
            _metadataWriter.Clear();
        }
    }
}
