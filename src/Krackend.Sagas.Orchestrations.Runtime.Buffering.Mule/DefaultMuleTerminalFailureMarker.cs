using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Default terminal failure marker for Mule actions.
/// </summary>
public sealed class DefaultMuleTerminalFailureMarker : IMuleTerminalFailureMarker
{
    /// <inheritdoc />
    public void MarkTerminal<TPayload>(MuleActionContext<TPayload> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Action.Attempts = int.MaxValue - 1;
    }
}
