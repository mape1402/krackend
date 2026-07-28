namespace Krackend.EventSourcing.Upcasting;

using Krackend.EventSourcing.Contracts;

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
        SemanticVersion currentSchemaVersion,
        SemanticVersion targetSchemaVersion,
        string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (currentSchemaVersion == default)
            currentSchemaVersion = SemanticVersion.Default;

        if (targetSchemaVersion == default)
            targetSchemaVersion = SemanticVersion.Default;

        if (currentSchemaVersion > targetSchemaVersion)
            throw new InvalidOperationException("Current schema version cannot be greater than target schema version.");

        var version = currentSchemaVersion;
        var currentPayload = payload;

        while (version < targetSchemaVersion)
        {
            var upcaster = _upcasters.SingleOrDefault(candidate =>
                candidate.EventType == eventType &&
                candidate.FromSchemaVersion == version &&
                candidate.ToSchemaVersion > candidate.FromSchemaVersion &&
                candidate.ToSchemaVersion <= targetSchemaVersion);

            if (upcaster is null)
                throw new InvalidOperationException($"No upcaster found for event '{eventType}' from schema version '{version}' toward '{targetSchemaVersion}'.");

            currentPayload = upcaster.Upcast(currentPayload);
            version = upcaster.ToSchemaVersion;
        }

        return new UpcastedEventPayload(eventType, version, currentPayload);
    }
}
