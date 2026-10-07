using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

/// <summary>
/// Translates runtime operational state changes for the Pigeon adapter.
/// </summary>
internal sealed class PigeonRuntimeDegradationHandler : IRuntimeDegradationHandler
{
    private readonly ILogger<PigeonRuntimeDegradationHandler> _logger;

    public PigeonRuntimeDegradationHandler(ILogger<PigeonRuntimeDegradationHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValueTask OnRuntimeStateChangedAsync(
        RuntimeOperationalStateChangedContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Current.State == RuntimeOperationalState.Closed)
        {
            _logger.LogWarning(
                "Pigeon ingress is closed by runtime admission. Messages already delivered to Krackend will not be completed until admission reopens.");
            return ValueTask.CompletedTask;
        }

        if (context.Previous?.State == RuntimeOperationalState.Closed)
        {
            _logger.LogInformation(
                "Pigeon ingress can resume because runtime admission changed to '{State}'.",
                context.Current.State);
        }

        return ValueTask.CompletedTask;
    }
}
