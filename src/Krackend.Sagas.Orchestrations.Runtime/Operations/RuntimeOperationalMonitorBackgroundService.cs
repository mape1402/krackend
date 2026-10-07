using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Periodically evaluates runtime dependencies and updates admission state.
/// </summary>
internal sealed class RuntimeOperationalMonitorBackgroundService : BackgroundService
{
    private readonly IRuntimeOperationalStateController _stateController;
    private readonly IOptionsMonitor<RuntimeOperationalOptions> _options;
    private readonly ILogger<RuntimeOperationalMonitorBackgroundService> _logger;

    public RuntimeOperationalMonitorBackgroundService(
        IRuntimeOperationalStateController stateController,
        IOptionsMonitor<RuntimeOperationalOptions> options,
        ILogger<RuntimeOperationalMonitorBackgroundService> logger)
    {
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Max(1, options.ScanIntervalSeconds));

            if (options.Enabled)
            {
                await EvaluateAsync(stoppingToken);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _stateController.EvaluateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while evaluating runtime operational state.");
        }
    }
}
