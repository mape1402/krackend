namespace Spider.Pipelines.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Client.Routing;
using Microsoft.Extensions.DependencyInjection;
using SquirrelBox;
using Spider.Pipelines.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        => AttachRequestOrchestration(builder, static request => request, null, "1.0.0");

    /// <summary>
    /// Publishes a trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        string topic,
        string version = "1.0.0")
        => AttachRequestOrchestration(builder, static request => request, topic, version);

    /// <summary>
    /// Publishes a trigger event.
    /// </summary>
    public static IPipelineBuilder<TRequest> EmitEvent<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        string topic,
        string version = "1.0.0")
        => AttachRequestEvent(builder, static request => request, topic, version);

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed request payload.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform)
        => AttachRequestOrchestration(builder, transform, null, "1.0.0");

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform,
        string topic,
        string version = "1.0.0")
        => AttachRequestOrchestration(builder, transform, topic, version);

    /// <summary>
    /// Publishes a transformed trigger event.
    /// </summary>
    public static IPipelineBuilder<TRequest> EmitEvent<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform,
        string topic,
        string version = "1.0.0")
        => AttachRequestEvent(builder, transform, topic, version);

    private static IPipelineBuilder<TRequest> AttachRequestEvent<TRequest>(
        IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform,
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

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                await context.Services.GetRequiredService<IOrchestrationOperationClient>().EmitEventAsync(
                    typeof(TRequest),
                    null,
                    transform(context.Request),
                    options,
                    context.CancellationToken);
            });
        });

        return builder;
    }

    private static IPipelineBuilder<TRequest> AttachRequestOrchestration<TRequest>(
        IPipelineBuilder<TRequest> builder,
        Func<TRequest, object> transform,
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
                RestoreDeferredMessageMetadata(context.Services);
                context.Services.GetRequiredService<IOrchestrationOperationClient>().Begin(typeof(TRequest));
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
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
                RestoreDeferredMessageMetadata(context.Services);
                var hasBackchannel = HasReplyAddress(context.Services
                    .GetService<IOrchestrationMessageMetadataAccessor>()
                    ?.Get());
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

                if (hasBackchannel)
                {
                    context.AsSettable().Success();
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Publishes a trigger event selected by routing when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest> UseOrchestration<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Action<OrchestrationTriggerRouteBuilder<TRequest>> routing)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (routing is null)
        {
            throw new ArgumentNullException(nameof(routing));
        }

        var routeBuilder = new OrchestrationTriggerRouteBuilder<TRequest>();
        routing(routeBuilder);
        return AttachRequestRouting(builder, routeBuilder);
    }

    private static IPipelineBuilder<TRequest> AttachRequestEventRouting<TRequest>(
        IPipelineBuilder<TRequest> builder,
        OrchestrationTriggerRouteBuilder<TRequest> routing)
    {
        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                var result = routing.Resolve(context.Request);
                await context.Services.GetRequiredService<IOrchestrationOperationClient>().EmitEventAsync(
                    typeof(TRequest),
                    null,
                    result.Payload,
                    result.Options,
                    context.CancellationToken);
            });
        });

        return builder;
    }

    /// <summary>
    /// Publishes a trigger event selected by routing.
    /// </summary>
    public static IPipelineBuilder<TRequest> EmitEvent<TRequest>(
        this IPipelineBuilder<TRequest> builder,
        Action<OrchestrationTriggerRouteBuilder<TRequest>> routing)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (routing is null)
        {
            throw new ArgumentNullException(nameof(routing));
        }

        var routeBuilder = new OrchestrationTriggerRouteBuilder<TRequest>();
        routing(routeBuilder);
        return AttachRequestEventRouting(builder, routeBuilder);
    }

    private static IPipelineBuilder<TRequest> AttachRequestRouting<TRequest>(
        IPipelineBuilder<TRequest> builder,
        OrchestrationTriggerRouteBuilder<TRequest> routing)
    {
        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                context.Services.GetRequiredService<IOrchestrationOperationClient>().Begin(typeof(TRequest));
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    var hasBackchannel = HasReplyAddress(context.Services
                        .GetService<IOrchestrationMessageMetadataAccessor>()
                        ?.Get());
                    var result = hasBackchannel
                        ? new OrchestrationTriggerRoutingResult(false, context.Request, new OrchestrationOperationOptions())
                        : routing.Resolve(context.Request);

                    await client.ReportSuccessAsync(
                        typeof(TRequest),
                        null,
                        result.Payload,
                        result.Options,
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }
            });
            postProcess.OnFailure(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                var hasBackchannel = HasReplyAddress(context.Services
                    .GetService<IOrchestrationMessageMetadataAccessor>()
                    ?.Get());
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    await client.ReportFailureAsync(
                        typeof(TRequest),
                        context.Exception,
                        new OrchestrationOperationOptions(),
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }

                if (hasBackchannel)
                {
                    context.AsSettable().Success();
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
        => AttachResponseOrchestration<TRequest, TResponse, Func<TResponse, object>>(
            builder,
            static (_, response, transformer) => transformer(response),
            static response => response,
            null,
            "1.0.0");

    /// <summary>
    /// Publishes a trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        string topic,
        string version = "1.0.0")
        => AttachResponseOrchestration<TRequest, TResponse, Func<TResponse, object>>(
            builder,
            static (_, response, transformer) => transformer(response),
            static response => response,
            topic,
            version);

    /// <summary>
    /// Publishes a trigger event.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> EmitEvent<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        string topic,
        string version = "1.0.0")
        => AttachResponseEvent<TRequest, TResponse, Func<TResponse, object>>(
            builder,
            static (_, response, transformer) => transformer(response),
            static response => response,
            topic,
            version);

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed response payload.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TResponse, object> transform)
        => AttachResponseOrchestration(
            builder,
            static (_, response, transformer) => transformer(response),
            transform,
            null,
            "1.0.0");

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => AttachResponseOrchestration(
            builder,
            static (_, response, transformer) => transformer(response),
            transform,
            topic,
            version);

    /// <summary>
    /// Publishes a transformed trigger event.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> EmitEvent<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => AttachResponseEvent(
            builder,
            static (_, response, transformer) => transformer(response),
            transform,
            topic,
            version);

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed request and response payload.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TRequest, TResponse, object> transform)
        => AttachResponseOrchestration<TRequest, TResponse, Func<TRequest, TResponse, object>>(
            builder,
            static (request, response, transformer) => transformer(request, response),
            transform,
            null,
            "1.0.0");

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TRequest, TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => AttachResponseOrchestration<TRequest, TResponse, Func<TRequest, TResponse, object>>(
            builder,
            static (request, response, transformer) => transformer(request, response),
            transform,
            topic,
            version);

    /// <summary>
    /// Publishes a transformed trigger event.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> EmitEvent<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Func<TRequest, TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => AttachResponseEvent<TRequest, TResponse, Func<TRequest, TResponse, object>>(
            builder,
            static (request, response, transformer) => transformer(request, response),
            transform,
            topic,
            version);

    private static IPipelineBuilder<TRequest, TResponse> AttachResponseEvent<TRequest, TResponse, TTransform>(
        IPipelineBuilder<TRequest, TResponse> builder,
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

        if (transformer is null)
        {
            throw new ArgumentNullException(nameof(transformer));
        }

        var options = CreateMessagingOptions(topic, version);

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                await context.Services.GetRequiredService<IOrchestrationOperationClient>().EmitEventAsync(
                    typeof(TRequest),
                    typeof(TResponse),
                    transform(context.Request, context.Response, transformer),
                    options,
                    context.CancellationToken);
            });
        });

        return builder;
    }

    private static IPipelineBuilder<TRequest, TResponse> AttachResponseOrchestration<TRequest, TResponse, TTransform>(
        IPipelineBuilder<TRequest, TResponse> builder,
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

        if (transformer is null)
        {
            throw new ArgumentNullException(nameof(transformer));
        }

        var options = CreateMessagingOptions(topic, version);

        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                context.Services.GetRequiredService<IOrchestrationOperationClient>().Begin(typeof(TRequest));
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
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
                RestoreDeferredMessageMetadata(context.Services);
                var hasBackchannel = HasReplyAddress(context.Services
                    .GetService<IOrchestrationMessageMetadataAccessor>()
                    ?.Get());
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

                if (hasBackchannel)
                {
                    context.AsSettable().Success();
                }
            });
        });

        return builder;
    }

    /// <summary>
    /// Publishes a trigger event selected by routing when metadata is not present.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Action<OrchestrationTriggerRouteBuilder<TRequest, TResponse>> routing)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (routing is null)
        {
            throw new ArgumentNullException(nameof(routing));
        }

        var routeBuilder = new OrchestrationTriggerRouteBuilder<TRequest, TResponse>();
        routing(routeBuilder);
        return AttachResponseRouting(builder, routeBuilder);
    }

    private static IPipelineBuilder<TRequest, TResponse> AttachResponseEventRouting<TRequest, TResponse>(
        IPipelineBuilder<TRequest, TResponse> builder,
        OrchestrationTriggerRouteBuilder<TRequest, TResponse> routing)
    {
        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                var result = routing.Resolve(context.Request, context.Response);
                await context.Services.GetRequiredService<IOrchestrationOperationClient>().EmitEventAsync(
                    typeof(TRequest),
                    typeof(TResponse),
                    result.Payload,
                    result.Options,
                    context.CancellationToken);
            });
        });

        return builder;
    }

    /// <summary>
    /// Publishes a trigger event selected by routing.
    /// </summary>
    public static IPipelineBuilder<TRequest, TResponse> EmitEvent<TRequest, TResponse>(
        this IPipelineBuilder<TRequest, TResponse> builder,
        Action<OrchestrationTriggerRouteBuilder<TRequest, TResponse>> routing)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (routing is null)
        {
            throw new ArgumentNullException(nameof(routing));
        }

        var routeBuilder = new OrchestrationTriggerRouteBuilder<TRequest, TResponse>();
        routing(routeBuilder);
        return AttachResponseEventRouting(builder, routeBuilder);
    }

    private static IPipelineBuilder<TRequest, TResponse> AttachResponseRouting<TRequest, TResponse>(
        IPipelineBuilder<TRequest, TResponse> builder,
        OrchestrationTriggerRouteBuilder<TRequest, TResponse> routing)
    {
        builder.OnPreProcess(preProcess =>
            preProcess.OnPreProcess((context, arguments) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                context.Services.GetRequiredService<IOrchestrationOperationClient>().Begin(typeof(TRequest));
                return Task.CompletedTask;
            }));

        builder.OnPostProcess(postProcess =>
        {
            postProcess.OnSuccess(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    var hasBackchannel = HasReplyAddress(context.Services
                        .GetService<IOrchestrationMessageMetadataAccessor>()
                        ?.Get());
                    var result = hasBackchannel
                        ? new OrchestrationTriggerRoutingResult(false, context.Response, new OrchestrationOperationOptions())
                        : routing.Resolve(context.Request, context.Response);

                    await client.ReportSuccessAsync(
                        typeof(TRequest),
                        typeof(TResponse),
                        result.Payload,
                        result.Options,
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }
            });
            postProcess.OnFailure(async (context, _) =>
            {
                RestoreDeferredMessageMetadata(context.Services);
                var hasBackchannel = HasReplyAddress(context.Services
                    .GetService<IOrchestrationMessageMetadataAccessor>()
                    ?.Get());
                var client = context.Services.GetRequiredService<IOrchestrationOperationClient>();
                try
                {
                    await client.ReportFailureAsync(
                        typeof(TRequest),
                        context.Exception,
                        new OrchestrationOperationOptions(),
                        context.CancellationToken);
                }
                finally
                {
                    client.Close();
                }

                if (hasBackchannel)
                {
                    context.AsSettable().Success();
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

    private static void RestoreDeferredMessageMetadata(IServiceProvider services)
    {
        RestoreDeferredPropagationMetadata(services);

        var accessor = services.GetService<IOrchestrationMessageMetadataAccessor>();
        if (HasReplyAddress(accessor?.Get()))
        {
            return;
        }

        var inbox = services.GetService<IInboxContextAccessor>()?.Current;
        if (inbox?.Entry?.Metadata is null
            || !inbox.Entry.Metadata.TryGetValue(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey, out var payload)
            || string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        var metadata = JsonSerializer.Deserialize<OrchestrationMessageMetadata>(payload, SerializerOptions);
        if (!HasReplyAddress(metadata))
        {
            return;
        }

        services.GetRequiredService<IOrchestrationMessageMetadataSetter>().Set(metadata);
    }

    private static void RestoreDeferredPropagationMetadata(IServiceProvider services)
    {
        var accessor = services.GetService<IOrchestrationPropagationMetadataAccessor>();
        if (accessor?.Get() is { HasItems: true })
        {
            return;
        }

        var inbox = services.GetService<IInboxContextAccessor>()?.Current;
        if (inbox?.Entry?.Metadata is null || inbox.Entry.Metadata.Count == 0)
        {
            return;
        }

        var metadata = new OrchestrationPropagationMetadata();
        if (inbox.Entry.Metadata.TryGetValue(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey, out var envelope) &&
            !string.IsNullOrWhiteSpace(envelope))
        {
            try
            {
                var restored = JsonSerializer.Deserialize<OrchestrationPropagationMetadata>(envelope, SerializerOptions);
                if (restored?.Items is not null)
                {
                    foreach (var item in restored.Items)
                    {
                        metadata.Items[item.Key] = item.Value?.DeepClone();
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (var item in inbox.Entry.Metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key) ||
                IsReservedPropagationMetadataKey(item.Key) ||
                metadata.Items.ContainsKey(item.Key))
            {
                continue;
            }

            metadata.Items[item.Key] = TryParseJson(item.Value) ?? JsonValue.Create(item.Value);
        }

        if (metadata.HasItems)
        {
            services.GetRequiredService<IOrchestrationPropagationMetadataSetter>().Set(metadata);
        }
    }

    private static bool IsReservedPropagationMetadataKey(string key)
        => !string.Equals(key, OrchestrationMetadataConstants.TriggerMetadataKey, StringComparison.Ordinal) &&
            key.StartsWith("Krackend.Sagas.Orchestrations.", StringComparison.Ordinal);

    private static JsonNode TryParseJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasReplyAddress(OrchestrationMessageMetadata metadata)
        => metadata?.ReplyAddress is not null
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.Transport)
           && !string.IsNullOrWhiteSpace(metadata.ReplyAddress.SettingsPayload);
}
