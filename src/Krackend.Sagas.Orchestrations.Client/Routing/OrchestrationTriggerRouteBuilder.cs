namespace Krackend.Sagas.Orchestrations.Client.Routing;

using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;

/// <summary>
/// Builds trigger routing rules for request-only orchestration pipelines.
/// </summary>
/// <typeparam name="TRequest">Request type handled by the pipeline.</typeparam>
public sealed class OrchestrationTriggerRouteBuilder<TRequest>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly List<OrchestrationTriggerRoute<TRequest>> _routes = new();
    private OrchestrationTriggerRoute<TRequest> _otherwise;
    private bool _requireMatch;

    /// <summary>
    /// Adds a messaging route that publishes the request payload when the predicate matches.
    /// </summary>
    /// <param name="predicate">Predicate used to select the route.</param>
    /// <param name="topic">Messaging topic or queue name.</param>
    /// <param name="version">Messaging contract version.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> When(
        Func<TRequest, bool> predicate,
        string topic,
        string version = "1.0.0")
        => When(predicate, static request => request, topic, version);

    /// <summary>
    /// Adds a messaging route that publishes a transformed payload when the predicate matches.
    /// </summary>
    /// <param name="predicate">Predicate used to select the route.</param>
    /// <param name="transform">Payload transform executed when the route matches.</param>
    /// <param name="topic">Messaging topic or queue name.</param>
    /// <param name="version">Messaging contract version.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> When(
        Func<TRequest, bool> predicate,
        Func<TRequest, object> transform,
        string topic,
        string version = "1.0.0")
        => When(predicate, transform, CreateMessagingReplyAddress(topic, version));

    /// <summary>
    /// Adds a transport-agnostic route that publishes the request payload when the predicate matches.
    /// </summary>
    /// <param name="predicate">Predicate used to select the route.</param>
    /// <param name="address">Transport-specific trigger address.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> When(
        Func<TRequest, bool> predicate,
        OrchestrationReplyAddress address)
        => When(predicate, static request => request, address);

    /// <summary>
    /// Adds a transport-agnostic route that publishes a transformed payload when the predicate matches.
    /// </summary>
    /// <param name="predicate">Predicate used to select the route.</param>
    /// <param name="transform">Payload transform executed when the route matches.</param>
    /// <param name="address">Transport-specific trigger address.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> When(
        Func<TRequest, bool> predicate,
        Func<TRequest, object> transform,
        OrchestrationReplyAddress address)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        _routes.Add(new OrchestrationTriggerRoute<TRequest>(predicate, transform, address));
        return this;
    }

    /// <summary>
    /// Adds a fallback messaging route that publishes the request payload when no previous route matches.
    /// </summary>
    /// <param name="topic">Messaging topic or queue name.</param>
    /// <param name="version">Messaging contract version.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> Otherwise(
        string topic,
        string version = "1.0.0")
        => Otherwise(static request => request, topic, version);

    /// <summary>
    /// Adds a fallback messaging route that publishes a transformed payload when no previous route matches.
    /// </summary>
    /// <param name="transform">Payload transform executed by the fallback route.</param>
    /// <param name="topic">Messaging topic or queue name.</param>
    /// <param name="version">Messaging contract version.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> Otherwise(
        Func<TRequest, object> transform,
        string topic,
        string version = "1.0.0")
        => Otherwise(transform, CreateMessagingReplyAddress(topic, version));

    /// <summary>
    /// Adds a fallback transport-agnostic route that publishes the request payload when no previous route matches.
    /// </summary>
    /// <param name="address">Transport-specific trigger address.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> Otherwise(OrchestrationReplyAddress address)
        => Otherwise(static request => request, address);

    /// <summary>
    /// Adds a fallback transport-agnostic route that publishes a transformed payload when no previous route matches.
    /// </summary>
    /// <param name="transform">Payload transform executed by the fallback route.</param>
    /// <param name="address">Transport-specific trigger address.</param>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> Otherwise(
        Func<TRequest, object> transform,
        OrchestrationReplyAddress address)
    {
        if (transform is null)
        {
            throw new ArgumentNullException(nameof(transform));
        }

        _otherwise = new OrchestrationTriggerRoute<TRequest>(
            static _ => true,
            transform,
            address);
        return this;
    }

    /// <summary>
    /// Requires at least one route or fallback route to match.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    public OrchestrationTriggerRouteBuilder<TRequest> RequireMatch()
    {
        _requireMatch = true;
        return this;
    }

    /// <summary>
    /// Resolves the route that should publish the trigger for a request.
    /// </summary>
    /// <param name="request">Request instance.</param>
    /// <returns>The selected routing result, or an unmatched result when no route matched.</returns>
    public OrchestrationTriggerRoutingResult Resolve(TRequest request)
    {
        foreach (var route in _routes)
        {
            if (route.Predicate(request))
            {
                return CreateResult(route.Transform(request), route.Address);
            }
        }

        if (_otherwise is not null)
        {
            return CreateResult(_otherwise.Transform(request), _otherwise.Address);
        }

        if (_requireMatch)
        {
            throw new OrchestrationTriggerRouteMatchException(
                $"No orchestration trigger route matched request type '{typeof(TRequest).FullName}'.");
        }

        return OrchestrationTriggerRoutingResult.NoMatch();
    }

    private static OrchestrationTriggerRoutingResult CreateResult(object payload, OrchestrationReplyAddress address)
        => new(
            true,
            payload,
            new OrchestrationOperationOptions
            {
                TriggerAddress = address
            });

    private static OrchestrationReplyAddress CreateMessagingReplyAddress(string topic, string version)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var settings = new MessagingReplyAddressSettings
        {
            Topic = topic,
            Version = NormalizeVersion(version)
        };

        return new OrchestrationReplyAddress
        {
            Transport = OrchestrationTransportNames.Messaging,
            SettingsPayload = JsonSerializer.Serialize(settings, SerializerOptions)
        };
    }

    private static string NormalizeVersion(string version)
        => string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
}
