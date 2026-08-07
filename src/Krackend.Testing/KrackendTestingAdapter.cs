namespace Krackend.Testing;

/// <summary>
/// Default implementation of <see cref="IKrackendTestingAdapter"/>.
/// </summary>
public sealed class KrackendTestingAdapter : IKrackendTestingAdapter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendTestingAdapter"/> class.
    /// </summary>
    public KrackendTestingAdapter(IKrackendTestEventStore eventStore)
    {
        EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <inheritdoc />
    public IKrackendTestEventStore EventStore { get; }
}
