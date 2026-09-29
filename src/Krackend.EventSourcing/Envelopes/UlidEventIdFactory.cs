namespace Krackend.EventSourcing.Envelopes;

/// <summary>
/// Creates ULID identifiers for persisted events.
/// </summary>
public sealed class UlidEventIdFactory : IEventIdFactory
{
    /// <inheritdoc />
    public string Create(EventIdFactoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Ulid.NewUlid().ToString();
    }
}
