using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Krackend.Sagas.Orchestrations.Runtime.Recovery;

/// <summary>
/// Runs automated durable reconciliation after startup and during runtime operation.
/// </summary>
internal sealed class RuntimeReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRuntimeOperationalStateController _stateController;
    private readonly IOptionsMonitor<RuntimeReconciliationOptions> _options;
    private readonly ILogger<RuntimeReconciliationBackgroundService> _logger;
    private readonly RuntimeForegroundActivity _foregroundActivity;
    private long _deferredSinceTimestamp;

    public RuntimeReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IRuntimeOperationalStateController stateController,
        IOptionsMonitor<RuntimeReconciliationOptions> options,
        ILogger<RuntimeReconciliationBackgroundService> logger)
        : this(scopeFactory, stateController, options, logger, null)
    {
    }

    public RuntimeReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IRuntimeOperationalStateController stateController,
        IOptionsMonitor<RuntimeReconciliationOptions> options,
        ILogger<RuntimeReconciliationBackgroundService> logger,
        RuntimeForegroundActivity foregroundActivity)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
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
                        await ReconcileIfAvailableAsync(stoppingToken);
                }
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private IDisposable TryBeginRecovery(
        RuntimeReconciliationOptions options,
        out bool shouldRun)
    {
        var quietPeriod = TimeSpan.FromMilliseconds(Math.Clamp(
            options.ForegroundQuietPeriodMilliseconds,
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
            options.MaxDeferralSeconds,
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

    private async Task ReconcileIfAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var evaluated = await _stateController.EvaluateAsync(cancellationToken);
            if (evaluated.State == RuntimeOperationalState.Closed)
            {
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var reconciler = scope.ServiceProvider.GetRequiredService<IOrchestrationRuntimeReconciler>();
            await reconciler.ReconcileAsync(DateTime.UtcNow, cancellationToken);
            await ScheduleReadyArtifactsAsync(scope.ServiceProvider, cancellationToken);
            await _stateController.EvaluateAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while reconciling orchestration runtime state.");
        }
    }

    private static async Task ScheduleReadyArtifactsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var artifactRepository = serviceProvider.GetRequiredService<IRuntimeArtifactRepository>();
        var standupScheduler = serviceProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();
        var artifacts = await artifactRepository.GetReady(cancellationToken);
        foreach (var artifact in artifacts)
        {
            await standupScheduler.ScheduleStandupAsync(new RuntimeIngressStandupRequest
            {
                ArtifactId = artifact.Id.ToString(),
                IngressGeneration = artifact.IngressGeneration,
                Reason = "reconciliation",
                RequestedOnUtc = DateTime.UtcNow
            }, cancellationToken);
        }
    }
}
