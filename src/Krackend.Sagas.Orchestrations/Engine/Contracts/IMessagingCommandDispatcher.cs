namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Dispatches runtime messaging task commands through the configured messaging facade.
/// </summary>
public interface IMessagingCommandDispatcher
{
    /// <summary>
    /// Dispatches a messaging task command.
    /// </summary>
    /// <param name="command">Messaging dispatch command produced by the engine.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Dispatch result returned by the messaging facade.</returns>
    Task<MessagingDispatchResult> Dispatch(MessagingDispatchCommand command, CancellationToken cancellationToken = default);
}
