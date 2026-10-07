using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public RuntimeReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IRuntimeOperationalStateController stateController,
        IOptionsMonitor<RuntimeReconciliationOptions> options,
        ILogger<RuntimeReconciliationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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
                await ReconcileIfAvailableAsync(stoppingToken);
            }

            await Task.Delay(interval, stoppingToken);
        }
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

            await _stateController.MarkRecoveringAsync("Automated runtime reconciliation is running.", cancellationToken);
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
