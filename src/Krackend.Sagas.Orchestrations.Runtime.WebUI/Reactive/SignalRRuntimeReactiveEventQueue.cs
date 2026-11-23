using System.Threading.Channels;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Buffers runtime reactive events before they are sent to connected SignalR clients.
/// </summary>
public sealed class SignalRRuntimeReactiveEventQueue
{
    private const int Capacity = 4096;
    private readonly Channel<RuntimeReactiveEventMessage> _channel = Channel.CreateBounded<RuntimeReactiveEventMessage>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    private int _resynchronizationRequired;
    private readonly Channel<RuntimeReactiveEvent> _fanoutChannel = Channel.CreateBounded<RuntimeReactiveEvent>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    private int _fanoutResynchronizationRequired;

    internal ChannelReader<RuntimeReactiveEvent> FanoutReader => _fanoutChannel.Reader;

    internal void TryEnqueueFanout(RuntimeReactiveEvent eventData)
    {
        if (!_fanoutChannel.Writer.TryWrite(eventData.CreateNotificationSnapshot()))
            RequireFanoutResynchronization();
    }

    internal void RequireFanoutResynchronization()
        => Interlocked.Exchange(ref _fanoutResynchronizationRequired, 1);

    internal bool TryTakeFanoutResynchronizationRequired()
        => Interlocked.Exchange(ref _fanoutResynchronizationRequired, 0) == 1;

    /// <summary>
    /// Gets the event reader consumed by the SignalR dispatcher.
    /// </summary>
    public ChannelReader<RuntimeReactiveEventMessage> Reader => _channel.Reader;

    /// <summary>
    /// Attempts to enqueue a runtime reactive event.
    /// </summary>
    public bool TryEnqueue(RuntimeReactiveEventMessage message)
    {
        if (message is null)
        {
            return false;
        }

        if (_channel.Writer.TryWrite(message))
        {
            return true;
        }

        // Realtime diagnostics must never grow an unbounded backlog or block
        // orchestration execution. A single reconciliation is requested once
        // the dispatcher catches up, preserving correctness without flooding
        // clients with expensive reloads.
        Interlocked.Exchange(ref _resynchronizationRequired, 1);
        return false;
    }

    /// <summary>
    /// Returns whether one lossy queue overflow requires clients to reconcile.
    /// </summary>
    public bool TryTakeResynchronizationRequired()
        => Interlocked.Exchange(ref _resynchronizationRequired, 0) == 1;
}
