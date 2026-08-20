namespace Spider.Pipelines.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Client.Responses;
using Krackend.Sagas.Orchestrations.Client.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Spider.Pipelines.PostProcessing;

/// <summary>
/// Adds Krackend orchestration behavior to Spider pipelines.
/// </summary>
public static class OrchestrationPipelineBuilderExtensions
{
    /// <summary>
    /// Responds to the orchestration backchannel when metadata is present.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder)
        => builder.UseOrchestration<TRequest>(static request => request);

    /// <summary>
    /// Publishes a trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        string topic,
        string version = "1.0.0")
        => builder.UseOrchestration<TRequest>(static request => request, topic, version);

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed request payload.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform)
        => builder.UseOrchestration(transform, null, "1.0.0");

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform,
        string topic,
        string version = "1.0.0")
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        var options = new OrchestrationOperationOptions
        {
            Topic = topic,
            Version = NormalizeVersion(version)
        };

        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                _ = context.Services.GetRequiredService<IInstanceMetadataAccessor>().Get();
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess((context, arguments) =>
                PublishSuccessAsync(context.Services, context.Request, null, transform(context.Request), options, arguments, context.CancellationToken));
            postProcess.OnFailure((context, arguments) =>
                PublishFailureAsync<TRequest>(context.Services, arguments, context.Exception, context.CancellationToken));
        });

        return builder;
    }

    /// <summary>
    /// Responds to the orchestration backchannel when metadata is present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder)
        => builder.UseOrchestration<TRequest, TResponse>(static response => response);

    /// <summary>
    /// Publishes a trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        string topic,
        string version = "1.0.0")
        => builder.UseOrchestration<TRequest, TResponse>(static response => response, topic, version);

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed response payload.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TResponse, object> transform)
        => builder.UseOrchestration(static (_, response, transformer) => transformer(response), transform, null, "1.0.0");

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => builder.UseOrchestration(static (_, response, transformer) => transformer(response), transform, topic, version);

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed request and response payload.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TRequest, TResponse, object> transform)
        => builder.UseOrchestration(transform, null, "1.0.0");

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TRequest, TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => builder.UseOrchestration<TRequest, TResponse, Func<TRequest, TResponse, object>>(
            static (request, response, transformer) => transformer(request, response),
            transform,
            topic,
            version);

    private static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse, TTransform>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TRequest, TResponse, TTransform, object> transform,
        TTransform transformer,
        string topic,
        string version)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        var options = new OrchestrationOperationOptions
        {
            Topic = topic,
            Version = NormalizeVersion(version)
        };

        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                _ = context.Services.GetRequiredService<IInstanceMetadataAccessor>().Get();
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess((context, arguments) =>
                PublishSuccessAsync(
                    context.Services,
                    context.Request,
                    typeof(TResponse),
                    transform(context.Request, context.Response, transformer),
                    options,
                    arguments,
                    context.CancellationToken));
            postProcess.OnFailure((context, arguments) =>
                PublishFailureAsync<TRequest>(context.Services, arguments, context.Exception, context.CancellationToken));
        });

        return builder;
    }

    private static async Task PublishSuccessAsync<TRequest>(
        IServiceProvider services,
        TRequest request,
        Type responseType,
        object payload,
        OrchestrationOperationOptions options,
        PostProcessArguments arguments,
        CancellationToken cancellationToken)
    {
        var metadata = services.GetRequiredService<IInstanceMetadataAccessor>().Get();
        var publisher = services.GetRequiredService<IOrchestrationClientPublisher>();
        var businessPayload = OrchestrationPayloadSerializer.ToJsonNode(payload);

        if (HasBackchannel(metadata))
        {
            var factory = services.GetRequiredService<IOrchestrationClientResponseFactory>();
            var envelope = factory.Success(metadata, typeof(TRequest), responseType, businessPayload, arguments.ExecutionTime);
            await publisher.PublishAsync(envelope, metadata.BackchannelTopic, NormalizeVersion(metadata.BackchannelVersion), cancellationToken);
            return;
        }

        if (options.HasTriggerDestination)
        {
            await publisher.PublishAsync(businessPayload, options.Topic, options.Version, cancellationToken);
        }
    }

    private static async Task PublishFailureAsync<TRequest>(
        IServiceProvider services,
        PostProcessArguments arguments,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var metadata = services.GetRequiredService<IInstanceMetadataAccessor>().Get();
        if (!HasBackchannel(metadata))
        {
            return;
        }

        var factory = services.GetRequiredService<IOrchestrationClientResponseFactory>();
        var publisher = services.GetRequiredService<IOrchestrationClientPublisher>();
        var envelope = factory.Failure(metadata, typeof(TRequest), arguments.Reason, exception, arguments.ExecutionTime);
        await publisher.PublishAsync(envelope, metadata.BackchannelTopic, NormalizeVersion(metadata.BackchannelVersion), cancellationToken);
    }

    private static bool HasBackchannel(InstanceMetadata metadata)
        => !string.IsNullOrWhiteSpace(metadata?.BackchannelTopic);

    private static string NormalizeVersion(string version)
        => string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
}
