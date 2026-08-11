using Krackend.Sagas.Orchestrations.Client.Abstractions;

namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Default broker-agnostic orchestration client coordinator.
/// </summary>
public sealed class OrchestrationClientExecutionCoordinator : IOrchestrationClientExecutionCoordinator
{
    private readonly IOrchestrationClientMetadataReader _metadataReader;
    private readonly IOrchestrationClientMetadataWriter _metadataWriter;
    private readonly IOrchestrationOutputPublisher _publisher;
    private readonly IOrchestrationOutputPayloadFactory _payloadFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationClientExecutionCoordinator"/> class.
    /// </summary>
    public OrchestrationClientExecutionCoordinator(
        IOrchestrationClientMetadataReader metadataReader,
        IOrchestrationClientMetadataWriter metadataWriter,
        IOrchestrationOutputPublisher publisher,
        IOrchestrationOutputPayloadFactory payloadFactory)
    {
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
        _metadataWriter = metadataWriter ?? throw new ArgumentNullException(nameof(metadataWriter));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _payloadFactory = payloadFactory ?? throw new ArgumentNullException(nameof(payloadFactory));
    }

    /// <inheritdoc/>
    public OrchestrationExecutionContext Begin(OrchestrationOutputDescriptor output = null)
    {
        var metadata = _metadataReader.Read();

        if (metadata is not null)
        {
            _metadataWriter.Set(metadata);

            return new OrchestrationExecutionContext
            {
                Mode = OrchestrationClientExecutionMode.Orchestrated,
                Metadata = metadata,
                Output = output
            };
        }

        _metadataWriter.Clear();

        return new OrchestrationExecutionContext
        {
            Mode = OrchestrationClientExecutionMode.Standalone,
            Output = output
        };
    }

    /// <inheritdoc/>
    public Task<OrchestrationPublishResult> PublishSuccess<TRequest, TResponse>(
        OrchestrationExecutionContext context,
        TRequest request,
        TResponse response,
        Func<TRequest, TResponse, object> transformPayload = null,
        CancellationToken cancellationToken = default)
    {
        var payload = _payloadFactory.CreateSuccessPayload(request, response, transformPayload);
        return Publish(context, payload, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OrchestrationPublishResult> PublishSuccess<TRequest>(
        OrchestrationExecutionContext context,
        TRequest request,
        Func<TRequest, object> transformPayload = null,
        CancellationToken cancellationToken = default)
    {
        var payload = _payloadFactory.CreateSuccessPayload(request, transformPayload);
        return Publish(context, payload, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OrchestrationPublishResult> PublishFailure<TRequest>(
        OrchestrationExecutionContext context,
        TRequest request,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var payload = _payloadFactory.CreateFailurePayload(request, exception);
        return Publish(context, payload, cancellationToken);
    }

    private Task<OrchestrationPublishResult> Publish(
        OrchestrationExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (!TryResolveOutput(context, out var topic, out var version))
        {
            return Task.FromResult(new OrchestrationPublishResult
            {
                Succeeded = true,
                Status = "Skipped"
            });
        }

        return _publisher.Publish(new OrchestrationPublishRequest
        {
            Topic = topic,
            Version = version,
            Payload = payload,
            Metadata = context.Metadata
        }, cancellationToken);
    }

    private static bool TryResolveOutput(
        OrchestrationExecutionContext context,
        out string topic,
        out OrchestrationSemanticVersion version)
    {
        if (context.Metadata is not null)
        {
            topic = context.Metadata.ResponseTopic;
            version = context.Metadata.ResponseVersion;
            return true;
        }

        if (context.Output is not null)
        {
            topic = context.Output.Topic;
            version = context.Output.Version;
            return true;
        }

        topic = null;
        version = default;
        return false;
    }
}
