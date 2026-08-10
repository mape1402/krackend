using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;

namespace Krackend.Sagas.Orchestrations.Messaging.Pigeon;

/// <summary>
/// Implements runtime consumer registration using Pigeon's dynamic consuming configurator.
/// </summary>
public sealed class PigeonMessageConsumerRegistry : IMessageConsumerRegistry
{
    private readonly IConsumingConfigurator _configurator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PigeonMessageConsumerRegistry"/> class.
    /// </summary>
    /// <param name="configurator">Pigeon consumer configurator.</param>
    public PigeonMessageConsumerRegistry(IConsumingConfigurator configurator)
    {
        _configurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
    }

    /// <inheritdoc/>
    public Task Register(MessageConsumerRegistration registration, CancellationToken cancellationToken = default)
    {
        if (registration is null)
            throw new ArgumentNullException(nameof(registration));

        _configurator.AddConsumer<JsonNode>(registration.Topic, ParseVersion(registration.Version), (context, message) => Handle(registration, context, message));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task Remove(string topic, string version, CancellationToken cancellationToken = default)
    {
        _configurator.RemoveConsumer(topic, ParseVersion(version));
        return Task.CompletedTask;
    }

    private static MessageConsumeContext CreateConsumeContext(ConsumeContext context, JsonNode message)
    {
        var metadataAccessor = context.Services.GetRequiredService<IOrchestratorMetadataAccessor>();
        return new MessageConsumeContext
        {
            Topic = context.Topic,
            Version = context.MessageVersion.ToString(),
            From = context.From,
            CreatedOnUtc = context.CreatedOnUtc,
            Message = message,
            Metadata = metadataAccessor.Current
        };
    }

    private static async Task Handle(MessageConsumerRegistration registration, ConsumeContext context, JsonNode message)
    {
        var consumeContext = CreateConsumeContext(context, message);
        await registration.Handler(consumeContext, context.CancellationToken);
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
