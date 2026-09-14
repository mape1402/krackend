namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

/// <summary>
/// Represents a leased buffer item being processed by a runtime component.
/// </summary>
public sealed class TriggerIntakeBufferLease
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerIntakeBufferLease"/> class.
    /// </summary>
    public TriggerIntakeBufferLease(TriggerIntakeBufferItem item, string leaseId, DateTime leasedOnUtc)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        LeaseId = leaseId ?? throw new ArgumentNullException(nameof(leaseId));
        LeasedOnUtc = leasedOnUtc;
    }

    /// <summary>
    /// Gets the leased item.
    /// </summary>
    public TriggerIntakeBufferItem Item { get; }

    /// <summary>
    /// Gets the lease id.
    /// </summary>
    public string LeaseId { get; }

    /// <summary>
    /// Gets the lease creation time.
    /// </summary>
    public DateTime LeasedOnUtc { get; }
}
