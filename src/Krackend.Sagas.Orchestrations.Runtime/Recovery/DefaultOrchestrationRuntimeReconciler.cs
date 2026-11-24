using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Recovery;

/// <summary>
/// Default durable runtime reconciler.
/// </summary>
internal sealed class DefaultOrchestrationRuntimeReconciler : IOrchestrationRuntimeReconciler
{
    private readonly IRuntimeAdmissionController _admissionController;
    private readonly IOrchestrationTimeoutProcessor _timeoutProcessor;
    private readonly ITaskResponseTimeoutScheduler _timeoutScheduler;
    private readonly IOrchestrationDispatchCancellationRecoveryCoordinator _dispatchCancellationRecoveryCoordinator;
    private readonly IOrchestrationDispatchRecoveryCoordinator _dispatchRecoveryCoordinator;
    private readonly IOrchestrationCompensationRecoveryCoordinator _compensationRecoveryCoordinator;
    private readonly RuntimeReconciliationOptions _options;
    private readonly IOrchestrationCallbackRecovery _callbackRecovery;
    private readonly IOrchestrationProgressRecoveryCandidateRepository _progressCandidates;
    private readonly IServiceScopeFactory _scopeFactory;

    public DefaultOrchestrationRuntimeReconciler(
        IRuntimeAdmissionController admissionController,
        IOrchestrationInstanceRepository instanceRepository,
        IOrchestrationTimeoutProcessor timeoutProcessor,
        Krackend.Sagas.Orchestrations.Runtime.Engine.ISagaEngine sagaEngine,
        IOptions<RuntimeReconciliationOptions> options,
        IOrchestrationDispatchRecoveryCoordinator dispatchRecoveryCoordinator = null,
        IOrchestrationCompensationRecoveryCoordinator compensationRecoveryCoordinator = null,
        IOrchestrationDispatchCancellationRecoveryCoordinator dispatchCancellationRecoveryCoordinator = null,
        ITaskResponseTimeoutScheduler timeoutScheduler = null,
        IOrchestrationCallbackRecovery callbackRecovery = null,
        IServiceScopeFactory scopeFactory = null)
    {
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        ArgumentNullException.ThrowIfNull(instanceRepository);
        _progressCandidates = instanceRepository as IOrchestrationProgressRecoveryCandidateRepository;
        _timeoutProcessor = timeoutProcessor ?? throw new ArgumentNullException(nameof(timeoutProcessor));
        _timeoutScheduler = timeoutScheduler ?? new NoopTaskResponseTimeoutScheduler();
        _callbackRecovery = callbackRecovery;
        _dispatchRecoveryCoordinator = dispatchRecoveryCoordinator;
        _compensationRecoveryCoordinator = compensationRecoveryCoordinator;
        _dispatchCancellationRecoveryCoordinator = dispatchCancellationRecoveryCoordinator;
        ArgumentNullException.ThrowIfNull(sagaEngine);
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _scopeFactory = scopeFactory;
    }

    public async Task<RuntimeReconciliationResult> ReconcileAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (!_admissionController.CanAccept(RuntimeAdmissionOperation.Reconciliation))
        {
            return new RuntimeReconciliationResult(0, 0, 0, true, "Runtime admission is closed.");
        }

        await _admissionController.EnsureAcceptedAsync(RuntimeAdmissionOperation.Reconciliation, cancellationToken);
        var recoveredCallbacks = _callbackRecovery is null ? 0
            : await _callbackRecovery.RecoverAsync(Math.Max(1, _options.BatchSize), cancellationToken);

        var reconciledCancellations = _dispatchCancellationRecoveryCoordinator is null
            ? 0
            : await _dispatchCancellationRecoveryCoordinator.ReconcilePendingAsync(
                Math.Max(1, _options.BatchSize),
                cancellationToken);
        var recoveredDispatches = _dispatchRecoveryCoordinator is null
            ? 0
            : await _dispatchRecoveryCoordinator.RecoverPendingDispatchesAsync(
                Math.Max(1, _options.BatchSize),
                cancellationToken);
        var recoveredCompensations = _compensationRecoveryCoordinator is null
            ? 0
            : await _compensationRecoveryCoordinator.RecoverPendingCompensationsAsync(
                Math.Max(1, _options.BatchSize),
                cancellationToken);
        var timeoutsProcessed = _timeoutScheduler.UsesDurableScheduling
            ? await _timeoutScheduler.RepairAsync(Math.Max(1, _options.BatchSize), cancellationToken)
            : await _timeoutProcessor.ProcessDueTimeoutsAsync(utcNow, cancellationToken);

        var progressResults = await RecoverStalledProgressAsync(utcNow, cancellationToken);

        return new RuntimeReconciliationResult(
            progressResults.Observed,
            progressResults.Recovered,
            timeoutsProcessed,
            false,
            DeliveriesRecovered: recoveredDispatches,
            CompensationsRecovered: recoveredCompensations,
            DispatchCancellationsReconciled: reconciledCancellations) { CallbacksRecovered = recoveredCallbacks };
    }

    private async Task<(int Observed, int Recovered)> RecoverStalledProgressAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (_progressCandidates is null || _scopeFactory is null)
            return (0, 0);

        var grace = TimeSpan.FromSeconds(Math.Max(1, _options.ProgressRecoveryGraceSeconds));
        var candidates = await _progressCandidates.GetProgressRecoveryCandidates(
            utcNow - grace,
            utcNow,
            Math.Max(1, _options.BatchSize),
            cancellationToken);
        if (candidates.Count == 0)
            return (0, 0);

        // Each reconciliation owns a scope/DbContext and an instance lease, so unrelated
        // stalled sagas recover concurrently without serializing behind the scanner.
        var results = await Task.WhenAll(candidates.Select(async instanceId =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var reconciliation = scope.ServiceProvider.GetRequiredService<IOrchestrationReconciliationService>();
            return await reconciliation.ReconcileAsync(instanceId.ToString(), cancellationToken);
        }));

        return (candidates.Count, results.Count(result => result.Status == "Recovered"));
    }

}
