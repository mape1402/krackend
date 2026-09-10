namespace Spider.Pipelines.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

/// <summary>
/// Adds Krackend orchestration behavior to Spider pipelines.
/// </summary>
public static class OrchestrationPipelineBuilderExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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

        var options = CreateMessagingOptions(topic, version);

        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                context.Services.GetRequiredService<IOrchestrationOperationClient>().Begin(typeof(TRequest));
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    await client.ReportSuccessAsync(
                        typeof(TRequest),
                        null,
                        transform(context.Request),
                        options,
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }
            });
            postProcess.OnFailure(async (context, _) =>
            {
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    await client.ReportFailureAsync(
                        typeof(TRequest),
                        context.Exception,
                        options,
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }
            });
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

        var options = CreateMessagingOptions(topic, version);

        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                context.Services.GetRequiredService<IOrchestrationOperationClient>().Begin(typeof(TRequest));
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    await client.ReportSuccessAsync(
                        typeof(TRequest),
                        typeof(TResponse),
                        transform(context.Request, context.Response, transformer),
                        options,
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }
            });
            postProcess.OnFailure(async (context, _) =>
            {
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    await client.ReportFailureAsync(
                        typeof(TRequest),
                        context.Exception,
                        options,
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }
            });
        });

        return builder;
    }

    private static OrchestrationOperationOptions CreateMessagingOptions(string topic, string version)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return new OrchestrationOperationOptions();
        }

        var settings = new MessagingReplyAddressSettings
        {
            Topic = topic,
            Version = NormalizeVersion(version)
        };

        return new OrchestrationOperationOptions
        {
            TriggerAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = JsonSerializer.Serialize(settings, SerializerOptions)
            }
        };
    }

    private static string NormalizeVersion(string version)
        => string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
}
