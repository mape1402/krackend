namespace Krackend.Testing;

/// <summary>
/// Exposes Krackend testing services to external test host integrations.
/// </summary>
public interface IKrackendTestingAdapter
{
    /// <summary>
    /// Gets the in-memory test event store.
    /// </summary>
    IKrackendTestEventStore EventStore { get; }
}
