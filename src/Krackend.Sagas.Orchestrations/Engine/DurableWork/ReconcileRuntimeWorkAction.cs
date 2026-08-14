namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using Mule;

/// <summary>
/// Mule action that reconciles stalled runtime work outside the scheduler loop.
/// </summary>
[MuleAction("krackend.runtime.reconcile")]
public sealed class ReconcileRuntimeWorkAction : IMuleAction<RuntimeReconcileRequest>
{
    private readonly IRuntimePendingWorkProcessor _pendingWorkProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReconcileRuntimeWorkAction"/> class.
    /// </summary>
    public ReconcileRuntimeWorkAction(IRuntimePendingWorkProcessor pendingWorkProcessor)
    {
        _pendingWorkProcessor = pendingWorkProcessor ?? throw new ArgumentNullException(nameof(pendingWorkProcessor));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(MuleActionContext<RuntimeReconcileRequest> context, CancellationToken cancellationToken)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var dueOnUtc = context.Payload.DueOnUtc == default
            ? DateTime.UtcNow
            : context.Payload.DueOnUtc;

        await _pendingWorkProcessor.ProcessDueWork(dueOnUtc, cancellationToken);
    }
}
