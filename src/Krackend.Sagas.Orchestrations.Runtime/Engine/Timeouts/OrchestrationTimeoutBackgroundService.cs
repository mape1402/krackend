using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class OrchestrationTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OrchestrationTimeoutOptions> _options;
    private readonly ILogger<OrchestrationTimeoutBackgroundService> _logger;

    public OrchestrationTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OrchestrationTimeoutOptions> options,
        ILogger<OrchestrationTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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
                await ProcessTimeoutsAsync(stoppingToken);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ProcessTimeoutsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IOrchestrationTimeoutProcessor>();
            await processor.ProcessDueTimeoutsAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while processing orchestration timeouts.");
        }
    }
}
