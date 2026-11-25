using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

internal sealed class OrchestrationTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OrchestrationTimeoutOptions> _options;
    private readonly ILogger<OrchestrationTimeoutBackgroundService> _logger;
    private readonly RuntimeForegroundActivity _foregroundActivity;
    private long _deferredSinceTimestamp;

    public OrchestrationTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OrchestrationTimeoutOptions> options,
        ILogger<OrchestrationTimeoutBackgroundService> logger)
        : this(scopeFactory, options, logger, null)
    {
    }

    public OrchestrationTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OrchestrationTimeoutOptions> options,
        ILogger<OrchestrationTimeoutBackgroundService> logger,
        RuntimeForegroundActivity foregroundActivity)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _foregroundActivity = foregroundActivity ?? new RuntimeForegroundActivity();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Max(1, options.ScanIntervalSeconds));

            if (options.Enabled)
            {
                var recoveryBypass = TryBeginRecovery(options, out var shouldRun);
                if (shouldRun)
                {
                    using (recoveryBypass)
                        await ProcessTimeoutsAsync(stoppingToken);
                }
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private IDisposable TryBeginRecovery(
        OrchestrationTimeoutOptions options,
        out bool shouldRun)
    {
        var quietPeriod = TimeSpan.FromMilliseconds(Math.Clamp(
            options.RecoveryQuietPeriodMilliseconds,
            0,
            60_000));
        if (!_foregroundActivity.ShouldYieldRecovery(quietPeriod))
        {
            Interlocked.Exchange(ref _deferredSinceTimestamp, 0);
            shouldRun = true;
            return null;
        }

        var now = Stopwatch.GetTimestamp();
        var deferredSince = Interlocked.CompareExchange(ref _deferredSinceTimestamp, now, 0);
        if (deferredSince == 0)
            deferredSince = now;

        var maxDeferral = TimeSpan.FromSeconds(Math.Clamp(
            options.RecoveryMaxDeferralSeconds,
            1,
            3_600));
        if (Stopwatch.GetElapsedTime(deferredSince) < maxDeferral)
        {
            shouldRun = false;
            return null;
        }

        Interlocked.Exchange(ref _deferredSinceTimestamp, now);
        shouldRun = true;
        return _foregroundActivity.EnterRecoveryBypass();
    }

    private async Task ProcessTimeoutsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var admission = scope.ServiceProvider.GetService<IRuntimeAdmissionController>();
            if (admission is not null &&
                !admission.CanAccept(RuntimeAdmissionOperation.TimeoutProcessing))
            {
                return;
            }

            // The core runtime registers a no-op scheduler, but retaining the fallback keeps this hosted service
            // compatible with minimal hosts that only register the legacy timeout processor.
            var scheduler = scope.ServiceProvider.GetService<ITaskResponseTimeoutScheduler>()
                ?? new NoopTaskResponseTimeoutScheduler();
            var idleProcessor = scope.ServiceProvider.GetRequiredService<IOrchestrationIdleProcessor>();
            await idleProcessor.ProcessExpiredAsync(DateTime.UtcNow, cancellationToken: cancellationToken);
            if (scheduler.UsesDurableScheduling)
            {
                await scheduler.RepairAsync(take: 100, cancellationToken: cancellationToken);
                return;
            }

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
