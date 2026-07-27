namespace Krackend.EventSourcing.Configuration;

/// <summary>
/// Configures event type to logical store routing.
/// </summary>
public sealed class EventRoutingOptions
{
    private readonly Dictionary<Type, string> _routes = [];
    private readonly Dictionary<Type, string> _commandRoutes = [];

    /// <summary>
    /// Gets configured event routes.
    /// </summary>
    public IReadOnlyDictionary<Type, string> Routes => _routes;

    /// <summary>
    /// Gets configured command routes.
    /// </summary>
    public IReadOnlyDictionary<Type, string> CommandRoutes => _commandRoutes;

    /// <summary>
    /// Gets or sets the default stream name used when no explicit route exists.
    /// </summary>
    public string DefaultStreamName { get; set; } = "domain";

    /// <summary>
    /// Routes an event CLR type to a logical stream name.
    /// </summary>
    public EventRoutingOptions Route<TEvent>(string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        _routes[typeof(TEvent)] = streamName;
        return this;
    }

    /// <summary>
    /// Routes a command CLR type to a logical stream name.
    /// </summary>
    public EventRoutingOptions RouteCommand<TCommand>(string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);

        _commandRoutes[typeof(TCommand)] = streamName;
        return this;
    }

    /// <summary>
    /// Resolves the logical stream name for an event CLR type.
    /// </summary>
    public string Resolve(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return _routes.TryGetValue(eventType, out var streamName)
            ? streamName
            : DefaultStreamName;
    }

    /// <summary>
    /// Resolves the logical stream name for a command CLR type.
    /// </summary>
    public string ResolveCommand(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        return _commandRoutes.TryGetValue(commandType, out var streamName)
            ? streamName
            : DefaultStreamName;
    }
}
