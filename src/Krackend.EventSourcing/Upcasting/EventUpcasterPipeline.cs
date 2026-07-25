namespace Krackend.EventSourcing.Upcasting;

/// <summary>
/// Default event upcaster pipeline.
/// </summary>
public sealed class EventUpcasterPipeline : IEventUpcasterPipeline
{
    private readonly IReadOnlyCollection<IEventUpcaster> _upcasters;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventUpcasterPipeline"/> class.
    /// </summary>
    public EventUpcasterPipeline(IEnumerable<IEventUpcaster> upcasters)
    {
        _upcasters = upcasters?.ToArray() ?? throw new ArgumentNullException(nameof(upcasters));
    }

    /// <inheritdoc />
    public UpcastedEventPayload Upcast(string eventType, int currentVersion, int targetVersion, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (currentVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "Current version must be greater than zero.");

        if (targetVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetVersion), "Target version must be greater than zero.");

        if (currentVersion > targetVersion)
            throw new InvalidOperationException("Current version cannot be greater than target version.");

        var version = currentVersion;
        var currentPayload = payload;

        while (version < targetVersion)
        {
            var upcaster = _upcasters.SingleOrDefault(candidate =>
                candidate.EventType == eventType &&
                candidate.FromVersion == version &&
                candidate.ToVersion == version + 1);

            if (upcaster is null)
                throw new InvalidOperationException($"No upcaster found for event '{eventType}' from version '{version}' to '{version + 1}'.");

            currentPayload = upcaster.Upcast(currentPayload);
            version = upcaster.ToVersion;
        }

        return new UpcastedEventPayload(eventType, version, currentPayload);
    }
}
