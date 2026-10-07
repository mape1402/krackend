namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Decides whether a runtime operation can be admitted under the current operational state.
/// </summary>
public interface IRuntimeAdmissionController
{
    /// <summary>
    /// Checks whether an operation can be admitted.
    /// </summary>
    bool CanAccept(RuntimeAdmissionOperation operation);

    /// <summary>
    /// Ensures that an operation can be admitted.
    /// </summary>
    /// <exception cref="RuntimeAdmissionRejectedException">Thrown when the runtime is closed for the requested operation.</exception>
    ValueTask EnsureAcceptedAsync(
        RuntimeAdmissionOperation operation,
        CancellationToken cancellationToken = default);
}
