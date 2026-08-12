namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Result produced by a pending runtime work scan.
/// </summary>
/// <param name="ScannedOnUtc">Scan instant.</param>
/// <param name="Items">Pending work items found.</param>
public sealed record RuntimePendingWorkResult(
    DateTime ScannedOnUtc,
    IReadOnlyCollection<RuntimePendingWorkItem> Items)
{
    /// <summary>
    /// Gets total pending work count.
    /// </summary>
    public int TotalCount => Items.Count;

    /// <summary>
    /// Gets waiting task count.
    /// </summary>
    public int WaitingTaskCount => Items.Count(x => x.WorkType == RuntimePendingWorkTypes.WaitingTaskTimeout);

    /// <summary>
    /// Gets waiting attempt count.
    /// </summary>
    public int WaitingAttemptCount => Items.Count(x => x.WorkType == RuntimePendingWorkTypes.WaitingAttemptTimeout);

    /// <summary>
    /// Gets pending compensation count.
    /// </summary>
    public int PendingCompensationCount => Items.Count(x => x.WorkType == RuntimePendingWorkTypes.PendingCompensation);
}
