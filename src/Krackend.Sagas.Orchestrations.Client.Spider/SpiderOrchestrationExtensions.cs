using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Client;
using Microsoft.Extensions.DependencyInjection;
using Pelican.Mediator;
using Spider.Pipelines.Core;

namespace Krackend.Sagas.Orchestrations.Client.Spider;

/// <summary>
/// Spider pipeline extensions that attach orchestration client behavior around business execution.
/// </summary>
public static class SpiderOrchestrationExtensions
{
    /// <summary>
    /// Adds orchestration behavior for a request/response execution using incoming metadata only.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> Orchestrate<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge)
        where TRequest : IRequest<TResponse>
        => OrchestrateCore<TRequest, TResponse>(bridge, output: null, transformPayload: null);

    /// <summary>
    /// Adds orchestration behavior for a request/response execution using a per-call output destination.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> Orchestrate<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        string topic,
        OrchestrationSemanticVersion? version = null)
        where TRequest : IRequest<TResponse>
        => OrchestrateCore<TRequest, TResponse>(bridge, new OrchestrationOutputDescriptor(topic, version), transformPayload: null);

    /// <summary>
    /// Adds orchestration behavior for a request/response execution using incoming metadata and a transformed payload.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> Orchestrate<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, TResponse, object> transformPayload)
        where TRequest : IRequest<TResponse>
        => OrchestrateCore<TRequest, TResponse>(bridge, output: null, transformPayload);

    /// <summary>
    /// Adds orchestration behavior for a request/response execution using a transformed payload and per-call destination.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> Orchestrate<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, TResponse, object> transformPayload,
        string topic,
        OrchestrationSemanticVersion? version = null)
        where TRequest : IRequest<TResponse>
        => OrchestrateCore(bridge, new OrchestrationOutputDescriptor(topic, version), transformPayload);

    /// <summary>
    /// Adds orchestration behavior for a request-only execution using incoming metadata only.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> Orchestrate<TRequest>(
        this IServiceBridge<IMediator> bridge)
        where TRequest : IRequest
        => OrchestrateCore<TRequest>(bridge, output: null, transformPayload: null);

    /// <summary>
    /// Adds orchestration behavior for a request-only execution using a per-call output destination.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> Orchestrate<TRequest>(
        this IServiceBridge<IMediator> bridge,
        string topic,
        OrchestrationSemanticVersion? version = null)
        where TRequest : IRequest
        => OrchestrateCore<TRequest>(bridge, new OrchestrationOutputDescriptor(topic, version), transformPayload: null);

    /// <summary>
    /// Adds orchestration behavior for a request-only execution using incoming metadata and a transformed payload.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> Orchestrate<TRequest>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, object> transformPayload)
        where TRequest : IRequest
        => OrchestrateCore<TRequest>(bridge, output: null, transformPayload);

    /// <summary>
    /// Adds orchestration behavior for a request-only execution using a transformed payload and per-call destination.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> Orchestrate<TRequest>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, object> transformPayload,
        string topic,
        OrchestrationSemanticVersion? version = null)
        where TRequest : IRequest
        => OrchestrateCore(bridge, new OrchestrationOutputDescriptor(topic, version), transformPayload);

    /// <summary>
    /// Sends a request/response Pelican request through an orchestration-configured Spider bridge.
    /// </summary>
    public static Task<TResponse> DefaultSend<TRequest, TResponse>(
        this IServiceBridge<IMediator, TRequest, TResponse> bridge,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(request);

        return bridge.ExecuteAsync(mediator => (message, token) => mediator.Send(message, token), request, cancellationToken);
    }

    /// <summary>
    /// Sends a request-only Pelican request through an orchestration-configured Spider bridge.
    /// </summary>
    public static Task DefaultSend<TRequest>(
        this IServiceBridge<IMediator, TRequest> bridge,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(request);

        return bridge.ExecuteAsync(mediator => mediator.Send, request, cancellationToken);
    }

    private static IServiceBridge<IMediator, TRequest, TResponse> OrchestrateCore<TRequest, TResponse>(
        IServiceBridge<IMediator> bridge,
        OrchestrationOutputDescriptor output,
        Func<TRequest, TResponse, object> transformPayload)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(bridge);

        return bridge.Attach<TRequest, TResponse>(builder =>
        {
            builder.OnPreProcess(config => config.OnPreProcess((context, _) =>
            {
                var coordinator = context.Services.GetRequiredService<IOrchestrationClientExecutionCoordinator>();
                coordinator.Begin(output);
                return Task.CompletedTask;
            }));

            builder.OnPostProcess(config =>
            {
                config.OnSuccess((context, _) =>
                {
                    var coordinator = context.Services.GetRequiredService<IOrchestrationClientExecutionCoordinator>();
                    var executionContext = CreateExecutionContext(context.Services, output);
                    return coordinator.PublishSuccess(executionContext, context.Request, context.Response, transformPayload, context.CancellationToken);
                });

                config.OnFailure((context, _) =>
                {
                    var coordinator = context.Services.GetRequiredService<IOrchestrationClientExecutionCoordinator>();
                    var executionContext = CreateExecutionContext(context.Services, output);
                    return coordinator.PublishFailure(executionContext, context.Request, context.Exception, context.CancellationToken);
                });
            });
        });
    }

    private static IServiceBridge<IMediator, TRequest> OrchestrateCore<TRequest>(
        IServiceBridge<IMediator> bridge,
        OrchestrationOutputDescriptor output,
        Func<TRequest, object> transformPayload)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(bridge);

        return bridge.Attach<TRequest>(builder =>
        {
            builder.OnPreProcess(config => config.OnPreProcess((context, _) =>
            {
                var coordinator = context.Services.GetRequiredService<IOrchestrationClientExecutionCoordinator>();
                coordinator.Begin(output);
                return Task.CompletedTask;
            }));

            builder.OnPostProcess(config =>
            {
                config.OnSuccess((context, _) =>
                {
                    var coordinator = context.Services.GetRequiredService<IOrchestrationClientExecutionCoordinator>();
                    var executionContext = CreateExecutionContext(context.Services, output);
                    return coordinator.PublishSuccess(executionContext, context.Request, transformPayload, context.CancellationToken);
                });

                config.OnFailure((context, _) =>
                {
                    var coordinator = context.Services.GetRequiredService<IOrchestrationClientExecutionCoordinator>();
                    var executionContext = CreateExecutionContext(context.Services, output);
                    return coordinator.PublishFailure(executionContext, context.Request, context.Exception, context.CancellationToken);
                });
            });
        });
    }

    private static OrchestrationExecutionContext CreateExecutionContext(IServiceProvider services, OrchestrationOutputDescriptor output)
    {
        var metadata = services.GetRequiredService<IOrchestrationClientMetadataAccessor>().Current;

        return new OrchestrationExecutionContext
        {
            Mode = metadata is null
                ? OrchestrationClientExecutionMode.Standalone
                : OrchestrationClientExecutionMode.Orchestrated,
            Metadata = metadata,
            Output = output
        };
    }
}
