namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Default admission controller backed by the runtime operational state provider.
/// </summary>
internal sealed class DefaultRuntimeAdmissionController : IRuntimeAdmissionController
{
    private readonly IRuntimeOperationalStateProvider _stateProvider;

    public DefaultRuntimeAdmissionController(IRuntimeOperationalStateProvider stateProvider)
    {
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
    }

    public bool CanAccept(RuntimeAdmissionOperation operation)
    {
        var state = _stateProvider.Current.State;
        return state switch
        {
            RuntimeOperationalState.Healthy => true,
            RuntimeOperationalState.Degraded => true,
            RuntimeOperationalState.Recovering => operation is RuntimeAdmissionOperation.Reconciliation
                or RuntimeAdmissionOperation.OrchestrationExecution
                or RuntimeAdmissionOperation.Dispatch
                or RuntimeAdmissionOperation.TimeoutProcessing
                or RuntimeAdmissionOperation.Recovery
                or RuntimeAdmissionOperation.IngressStandup,
            RuntimeOperationalState.Closed => false,
            _ => false
        };
    }

    public ValueTask EnsureAcceptedAsync(
        RuntimeAdmissionOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (CanAccept(operation))
        {
            return ValueTask.CompletedTask;
        }

        throw new RuntimeAdmissionRejectedException(operation, _stateProvider.Current);
    }
}
