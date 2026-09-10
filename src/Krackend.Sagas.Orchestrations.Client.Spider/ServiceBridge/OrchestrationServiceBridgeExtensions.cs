namespace Spider.Pipelines.Core;

using Pelican.Mediator;

/// <summary>
/// Adds Krackend orchestration behavior directly to Spider service bridges.
/// </summary>
public static class OrchestrationServiceBridgeExtensions
{
    /// <summary>
    /// Responds to the orchestration backchannel when metadata is present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> UseOrchestration<TRequest>(
        this IServiceBridge<IMediator> bridge)
        => bridge.Attach<TRequest>(static pipeline => pipeline.UseOrchestration());

    /// <summary>
    /// Publishes a trigger event when metadata is not present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> UseOrchestration<TRequest>(
        this IServiceBridge<IMediator> bridge,
        string topic,
        string version = "1.0.0")
        => bridge.Attach<TRequest>(pipeline => pipeline.UseOrchestration(topic, version));

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed request payload.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> UseOrchestration<TRequest>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, object> transform)
        => bridge.Attach<TRequest>(pipeline => pipeline.UseOrchestration(transform));

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest> UseOrchestration<TRequest>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, object> transform,
        string topic,
        string version = "1.0.0")
        => bridge.Attach<TRequest>(pipeline => pipeline.UseOrchestration(transform, topic, version));

    /// <summary>
    /// Responds to the orchestration backchannel when metadata is present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge)
        => bridge.Attach<TRequest, TResponse>(static pipeline => pipeline.UseOrchestration());

    /// <summary>
    /// Publishes a trigger event when metadata is not present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        string topic,
        string version = "1.0.0")
        => bridge.Attach<TRequest, TResponse>(pipeline => pipeline.UseOrchestration(topic, version));

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed response payload.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        Func<TResponse, object> transform)
        => bridge.Attach<TRequest, TResponse>(pipeline => pipeline.UseOrchestration(transform));

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        Func<TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => bridge.Attach<TRequest, TResponse>(pipeline => pipeline.UseOrchestration(transform, topic, version));

    /// <summary>
    /// Responds to the orchestration backchannel using a transformed request and response payload.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, TResponse, object> transform)
        => bridge.Attach<TRequest, TResponse>(pipeline => pipeline.UseOrchestration(transform));

    /// <summary>
    /// Publishes a transformed trigger event when metadata is not present.
    /// </summary>
    public static IServiceBridge<IMediator, TRequest, TResponse> UseOrchestration<TRequest, TResponse>(
        this IServiceBridge<IMediator> bridge,
        Func<TRequest, TResponse, object> transform,
        string topic,
        string version = "1.0.0")
        => bridge.Attach<TRequest, TResponse>(pipeline => pipeline.UseOrchestration(transform, topic, version));
}
