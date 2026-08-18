namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

/// <summary>
/// Runtime task dispatcher that schedules task dispatch through durable work instead of publishing inline.
/// </summary>
public sealed class DurableRuntimeTaskDispatcher : IRuntimeTaskDispatcher
{
    private readonly IRuntimeDurableWorkScheduler _durableWorkScheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableRuntimeTaskDispatcher"/> class.
    /// </summary>
    public DurableRuntimeTaskDispatcher(IRuntimeDurableWorkScheduler durableWorkScheduler)
    {
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
    }

    /// <inheritdoc />
    public bool CanDispatch(string taskKind)
        => string.Equals(taskKind, "Messaging", StringComparison.OrdinalIgnoreCase)
            || string.Equals(taskKind, "0", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<RuntimeTaskDispatchResult> Dispatch(
        RuntimeTaskDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var envelope = request.ToDispatchEnvelope();
        await _durableWorkScheduler.ScheduleDispatchTask(envelope, cancellationToken);

        return new RuntimeTaskDispatchResult
        {
            Succeeded = true,
            Status = "Scheduled",
            ExternalReference = envelope.DispatchId
        };
    }
}
