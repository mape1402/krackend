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
    public UpcastedEventPayload Upcast(
        string eventType,
        string currentSchemaVersion,
        string targetSchemaVersion,
        string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var currentVersion = Version.Parse(currentSchemaVersion);
        var targetVersion = Version.Parse(targetSchemaVersion);

        if (currentVersion > targetVersion)
            throw new InvalidOperationException("Current schema version cannot be greater than target schema version.");

        var version = currentSchemaVersion;
        var currentPayload = payload;

        while (Version.Parse(version) < targetVersion)
        {
            var upcaster = _upcasters.SingleOrDefault(candidate =>
                candidate.EventType == eventType &&
                candidate.FromSchemaVersion == version &&
                Version.Parse(candidate.ToSchemaVersion) > Version.Parse(candidate.FromSchemaVersion) &&
                Version.Parse(candidate.ToSchemaVersion) <= targetVersion);

            if (upcaster is null)
                throw new InvalidOperationException($"No upcaster found for event '{eventType}' from schema version '{version}' toward '{targetSchemaVersion}'.");

            currentPayload = upcaster.Upcast(currentPayload);
            version = upcaster.ToSchemaVersion;
        }

        return new UpcastedEventPayload(eventType, version, currentPayload);
    }
}
