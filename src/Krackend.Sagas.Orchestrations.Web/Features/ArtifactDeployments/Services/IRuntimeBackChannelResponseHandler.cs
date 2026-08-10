using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Handles messages received through an orchestration back channel.
/// </summary>
public interface IRuntimeBackChannelResponseHandler
{
    /// <summary>
    /// Handles a broker-neutral back-channel response.
    /// </summary>
    /// <param name="context">Consumed response context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Handle(MessageConsumeContext context, CancellationToken cancellationToken = default);
}
