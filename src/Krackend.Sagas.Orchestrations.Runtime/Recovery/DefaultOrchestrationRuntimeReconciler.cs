using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Recovery;

/// <summary>
/// Default durable runtime reconciler.
/// </summary>
internal sealed class DefaultOrchestrationRuntimeReconciler : IOrchestrationRuntimeReconciler
{
    private readonly IRuntimeAdmissionController _admissionController;
    private readonly IOrchestrationInstanceRepository _instanceRepository;
    private readonly IOrchestrationTimeoutProcessor _timeoutProcessor;
    private readonly ISagaEngine _sagaEngine;
    private readonly RuntimeReconciliationOptions _options;

    public DefaultOrchestrationRuntimeReconciler(
        IRuntimeAdmissionController admissionController,
        IOrchestrationInstanceRepository instanceRepository,
        IOrchestrationTimeoutProcessor timeoutProcessor,
        ISagaEngine sagaEngine,
        IOptions<RuntimeReconciliationOptions> options)
    {
        _admissionController = admissionController ?? throw new ArgumentNullException(nameof(admissionController));
        _instanceRepository = instanceRepository ?? throw new ArgumentNullException(nameof(instanceRepository));
        _timeoutProcessor = timeoutProcessor ?? throw new ArgumentNullException(nameof(timeoutProcessor));
        _sagaEngine = sagaEngine ?? throw new ArgumentNullException(nameof(sagaEngine));
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

        var timeoutsProcessed = await _timeoutProcessor.ProcessDueTimeoutsAsync(utcNow, cancellationToken);
        var instances = await _instanceRepository.GetRecoverable(Math.Max(1, _options.BatchSize), cancellationToken);
        var advanced = 0;

        foreach (var instance in instances)
        {
            if (IsTerminal(instance.Status))
            {
                continue;
            }

            await _sagaEngine.OrchestrateAsync(new ForwardIntent
            {
                ArtifactId = instance.RuntimeOrchestrationArtifactId.ToString(),
                IngressTransport = IngressTransport.Messaging,
                MessageMetadata = BuildMetadata(instance),
                Payload = instance.SnapshotPayload?.DeepClone()
            }, cancellationToken);
            advanced++;
        }

        return new RuntimeReconciliationResult(instances.Count, advanced, timeoutsProcessed, false);
    }

    private static OrchestrationMessageMetadata BuildMetadata(OrchestrationInstance instance)
        => new()
        {
            SagaId = string.IsNullOrWhiteSpace(instance.SagaId) ? instance.Id.ToString() : instance.SagaId,
            OrchestrationInstanceId = instance.Id.ToString(),
            CurrentStage = instance.CurrentStageKey,
            CurrentTasks = string.IsNullOrWhiteSpace(instance.CurrentTaskKey)
                ? Array.Empty<string>()
                : [instance.CurrentTaskKey],
            CorrelationId = instance.CorrelationId
        };

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Failed
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Aborted
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Compensated;
}
