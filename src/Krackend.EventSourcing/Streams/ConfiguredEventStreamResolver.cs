using Krackend.EventSourcing.Configuration;

namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Resolves stream names from configured event routes.
/// </summary>
public sealed class ConfiguredEventStreamResolver : IEventStreamResolver
{
    private readonly EventRoutingOptions _routingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredEventStreamResolver"/> class.
    /// </summary>
    public ConfiguredEventStreamResolver(EventRoutingOptions routingOptions)
    {
        _routingOptions = routingOptions ?? throw new ArgumentNullException(nameof(routingOptions));
    }

    /// <inheritdoc />
    public string ResolveStreamName(Type eventType)
        => _routingOptions.Resolve(eventType);

    /// <inheritdoc />
    public string ResolveStreamType(Type aggregateType)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);

        return aggregateType.Name;
    }
}
