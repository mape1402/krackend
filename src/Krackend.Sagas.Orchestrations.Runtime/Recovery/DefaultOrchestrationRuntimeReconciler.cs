using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
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

    public DefaultOrchestrationRuntimeReconciler(
        IRuntimeAdmissionController admissionController,
        IOrchestrationInstanceRepository instanceRepository,
        IOrchestrationTimeoutProcessor timeoutProcessor,
        Krackend.Sagas.Orchestrations.Runtime.Engine.ISagaEngine sagaEngine,
        IOptions<RuntimeReconciliationOptions> options,
        IOrchestrationDispatchRecoveryCoordinator dispatchRecoveryCoordinator = null,
        IOrchestrationCompensationRecoveryCoordinator compensationRecoveryCoordinator = null,
        IOrchestrationDispatchCancellationRecoveryCoordinator dispatchCancellationRecoveryCoordinator = null,
        ITaskResponseTimeoutScheduler timeoutScheduler = null)
    {
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        ArgumentNullException.ThrowIfNull(instanceRepository);
        _timeoutProcessor = timeoutProcessor ?? throw new ArgumentNullException(nameof(timeoutProcessor));
        _timeoutScheduler = timeoutScheduler ?? new NoopTaskResponseTimeoutScheduler();
        _dispatchRecoveryCoordinator = dispatchRecoveryCoordinator;
        _compensationRecoveryCoordinator = compensationRecoveryCoordinator;
        _dispatchCancellationRecoveryCoordinator = dispatchCancellationRecoveryCoordinator;
        ArgumentNullException.ThrowIfNull(sagaEngine);
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

        return new RuntimeReconciliationResult(
            0,
            0,
            timeoutsProcessed,
            false,
            DeliveriesRecovered: recoveredDispatches,
            CompensationsRecovered: recoveredCompensations,
            DispatchCancellationsReconciled: reconciledCancellations);
    }

}
