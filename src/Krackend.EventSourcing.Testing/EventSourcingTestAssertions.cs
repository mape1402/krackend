using System.Text.Json;
using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Default implementation of <see cref="IEventSourcingTestAssertions"/>.
/// </summary>
public sealed class EventSourcingTestAssertions : IEventSourcingTestAssertions
{
    private readonly IEventSourcingTestEventStore _eventStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSourcingTestAssertions"/> class.
    /// </summary>
    public EventSourcingTestAssertions(IEventSourcingTestEventStore eventStore)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <inheritdoc />
    public async Task ShouldHaveStreamAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        var stream = EventStreamReference.Create(streamName, streamId);
        var events = await _eventStore.ReadAsync(stream, cancellationToken);

        if (events.Count == 0)
            throw new EventSourcingTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' to exist.");
    }

    /// <inheritdoc />
    public async Task ShouldHaveEventAsync<TEvent>(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        var stream = EventStreamReference.Create(streamName, streamId);
        var events = await _eventStore.ReadAsync(stream, cancellationToken);

        if (!events.Any(x => x.EventClrType == typeof(TEvent)))
            throw new EventSourcingTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' to contain event '{typeof(TEvent).Name}'.");
    }

    /// <inheritdoc />
    public async Task ShouldHaveEventsInOrderAsync(
        string streamName,
        string streamId,
        IReadOnlyCollection<Type> eventTypes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);

        var stream = EventStreamReference.Create(streamName, streamId);
        var actual = (await _eventStore.ReadAsync(stream, cancellationToken))
            .Select(x => x.EventClrType)
            .ToArray();

        if (!actual.SequenceEqual(eventTypes))
        {
            var expectedNames = string.Join(", ", eventTypes.Select(x => x.Name));
            var actualNames = string.Join(", ", actual.Select(x => x.Name));
            throw new EventSourcingTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' event order [{expectedNames}] but found [{actualNames}].");
        }
    }

    /// <inheritdoc />
    public async Task ShouldHaveVersionAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var stream = EventStreamReference.Create(streamName, streamId);
        var actualVersion = (await _eventStore.ReadAsync(stream, cancellationToken))
            .Select(x => x.StreamVersion)
            .DefaultIfEmpty(0)
            .Max();

        if (actualVersion != expectedVersion)
            throw new EventSourcingTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' version '{expectedVersion}' but found '{actualVersion}'.");
    }

    /// <inheritdoc />
    public async Task ShouldHaveMetadataAsync(
        string key,
        object? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var events = await _eventStore.ReadAllAsync(cancellationToken);

        if (!events.Any(x => x.Metadata.TryGetValue(key, out var actual) && ValuesEqual(actual, value)))
            throw new EventSourcingTestingAssertionException($"Expected metadata '{key}' with value '{value}' to exist.");
    }

    /// <inheritdoc />
    public async Task ShouldHaveSerializedPayloadAsync(
        string streamName,
        string streamId,
        string expectedJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedJson);

        var stream = EventStreamReference.Create(streamName, streamId);
        var expected = NormalizeJson(expectedJson);
        var found = (await _eventStore.ReadAsync(stream, cancellationToken))
            .Any(x => NormalizeJson(x.SerializedPayload) == expected);

        if (!found)
            throw new EventSourcingTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' to contain serialized payload '{expected}'.");
    }

    private static string NormalizeJson(string source)
        => JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(source));

    private static bool ValuesEqual(object? actual, object? expected)
        => actual switch
        {
            null => expected is null,
            JsonElement jsonElement => JsonElementEquals(jsonElement, expected),
            _ => actual.Equals(expected)
        };

    private static bool JsonElementEquals(JsonElement actual, object? expected)
        => expected is JsonElement expectedJson
            ? JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(expectedJson)
            : actual.ToString() == expected?.ToString();
}
