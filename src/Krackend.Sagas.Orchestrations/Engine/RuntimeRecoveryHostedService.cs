using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Periodically scans and resumes pending runtime work.
/// </summary>
public sealed class RuntimeRecoveryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RuntimeRecoveryOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeRecoveryHostedService"/> class.
    /// </summary>
    public RuntimeRecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RuntimeRecoveryOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
            return;

        if (_options.CurrentValue.RunOnStartup)
            await Recover(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(GetScanInterval(), stoppingToken);
            await Recover(stoppingToken);
        }
    }

    private async Task Recover(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IRuntimePendingWorkProcessor>();
        await processor.ProcessDueWork(DateTime.UtcNow, cancellationToken);
    }

    private TimeSpan GetScanInterval()
    {
        var options = _options.CurrentValue;
        return options.ScanInterval < options.MinimumScanInterval
            ? options.MinimumScanInterval
            : options.ScanInterval;
    }
}
