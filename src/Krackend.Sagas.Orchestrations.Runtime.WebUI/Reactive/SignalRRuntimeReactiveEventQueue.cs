using System.Threading.Channels;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Buffers runtime reactive events before they are sent to connected SignalR clients.
/// </summary>
public sealed class SignalRRuntimeReactiveEventQueue
{
    private readonly Channel<RuntimeReactiveEventMessage> _channel = Channel.CreateUnbounded<RuntimeReactiveEventMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    /// Gets the event reader consumed by the SignalR dispatcher.
    /// </summary>
    public ChannelReader<RuntimeReactiveEventMessage> Reader => _channel.Reader;

    /// <summary>
    /// Attempts to enqueue a runtime reactive event.
    /// </summary>
    public bool TryEnqueue(RuntimeReactiveEventMessage message)
        => message is not null && _channel.Writer.TryWrite(message);
}
