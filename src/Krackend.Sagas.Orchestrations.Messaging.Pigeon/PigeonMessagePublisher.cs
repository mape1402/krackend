using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

namespace Krackend.Sagas.Orchestrations.Messaging.Pigeon;

/// <summary>
/// Publishes runtime messages through Pigeon's producer while keeping the engine broker-neutral.
/// </summary>
public sealed class PigeonMessagePublisher : IMessagePublisher
{
    private readonly IProducer _producer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PigeonMessagePublisher"/> class.
    /// </summary>
    /// <param name="producer">Pigeon producer.</param>
    public PigeonMessagePublisher(IProducer producer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    /// <inheritdoc/>
    public async Task<MessagePublishResult> Publish(MessagePublishRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            await _producer.PublishAsync(request.Message ?? new JsonObject(), request.Topic, ParseVersion(request.Version), cancellationToken);

            return new MessagePublishResult
            {
                Succeeded = true,
                Status = "Dispatched",
                ExternalReference = request.Topic
            };
        }
        catch (Exception ex)
        {
            return new MessagePublishResult
            {
                Succeeded = false,
                Status = "Failed",
                FailureReason = ex.Message
            };
        }
    }

    /// <summary>
    /// Converts a runtime version string to Pigeon's semantic version value.
    /// </summary>
    /// <param name="version">Semantic version text.</param>
    /// <returns>Pigeon semantic version.</returns>
    private static SemanticVersion ParseVersion(string version)
        => string.IsNullOrWhiteSpace(version)
            ? SemanticVersion.Default
            : SemanticVersion.Parse(version);
}
