using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Marks Mule action executions as terminal when a failure must not be retried.
/// </summary>
public interface IMuleTerminalFailureMarker
{
    /// <summary>
    /// Updates the action execution context so Mule treats the current failure as terminal.
    /// </summary>
    /// <typeparam name="TPayload">Payload type handled by the Mule action.</typeparam>
    /// <param name="context">Mule action execution context.</param>
    void MarkTerminal<TPayload>(MuleActionContext<TPayload> context);
}
