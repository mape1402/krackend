namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Indicates that the runtime rejected an operation because its current operational state cannot safely accept it.
/// </summary>
public sealed class RuntimeAdmissionRejectedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeAdmissionRejectedException"/> class.
    /// </summary>
    public RuntimeAdmissionRejectedException(RuntimeAdmissionOperation operation, RuntimeOperationalSnapshot snapshot)
        : base($"Runtime admission rejected operation '{operation}' while operational state is '{snapshot?.State}'. {snapshot?.Reason}")
    {
        Operation = operation;
        Snapshot = snapshot;
    }

    /// <summary>
    /// Gets the rejected operation.
    /// </summary>
    public RuntimeAdmissionOperation Operation { get; }

    /// <summary>
    /// Gets the runtime snapshot that caused the rejection.
    /// </summary>
    public RuntimeOperationalSnapshot Snapshot { get; }
}
