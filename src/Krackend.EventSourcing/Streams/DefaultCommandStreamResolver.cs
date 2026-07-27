using Krackend.EventSourcing.Configuration;

namespace Krackend.EventSourcing.Streams;

/// <summary>
/// Resolves command streams from routing configuration and command-provided stream ids.
/// </summary>
public sealed class DefaultCommandStreamResolver<TCommand> : ICommandStreamResolver<TCommand>
{
    private readonly EventRoutingOptions _routingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCommandStreamResolver{TCommand}"/> class.
    /// </summary>
    public DefaultCommandStreamResolver(EventRoutingOptions routingOptions)
    {
        _routingOptions = routingOptions ?? throw new ArgumentNullException(nameof(routingOptions));
    }

    /// <inheritdoc />
    public EventStreamReference Resolve(TCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command is not IEventStreamCommand streamCommand)
            throw new InvalidOperationException($"Command '{typeof(TCommand).Name}' must implement '{nameof(IEventStreamCommand)}' or provide a custom '{nameof(ICommandStreamResolver<TCommand>)}'.");

        return EventStreamReference.Create(_routingOptions.ResolveCommand(typeof(TCommand)), streamCommand.StreamId);
    }
}
