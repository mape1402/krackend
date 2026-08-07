using System.Text.Json;
using Krackend.EventSourcing.Streams;

namespace Krackend.Testing;

/// <summary>
/// Provides assertion helpers for <see cref="IKrackendTestEventStore"/>.
/// </summary>
public static class KrackendTestEventStoreAssertions
{
    /// <summary>
    /// Asserts that the specified stream exists.
    /// </summary>
    public static IKrackendTestEventStore ShouldHaveStream(
        this IKrackendTestEventStore eventStore,
        string streamName,
        string streamId)
    {
        var stream = EventStreamReference.Create(streamName, streamId);
        var events = ReadStream(eventStore, stream);

        if (events.Count == 0)
            throw new KrackendTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' to exist.");

        return eventStore;
    }

    /// <summary>
    /// Asserts that the specified stream contains an event of the requested CLR type.
    /// </summary>
    public static IKrackendTestEventStore ShouldHaveEvent<TEvent>(
        this IKrackendTestEventStore eventStore,
        string streamName,
        string streamId)
    {
        var stream = EventStreamReference.Create(streamName, streamId);
        var events = ReadStream(eventStore, stream);

        if (!events.Any(x => x.EventClrType == typeof(TEvent)))
            throw new KrackendTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' to contain event '{typeof(TEvent).Name}'.");

        return eventStore;
    }

    /// <summary>
    /// Asserts that the specified stream contains exactly the requested event types in order.
    /// </summary>
    public static IKrackendTestEventStore ShouldHaveEventsInOrder(
        this IKrackendTestEventStore eventStore,
        string streamName,
        string streamId,
        params Type[] eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);

        var stream = EventStreamReference.Create(streamName, streamId);
        var actual = ReadStream(eventStore, stream)
            .Select(x => x.EventClrType)
            .ToArray();

        if (!actual.SequenceEqual(eventTypes))
        {
            var expectedNames = string.Join(", ", eventTypes.Select(x => x.Name));
            var actualNames = string.Join(", ", actual.Select(x => x.Name));
            throw new KrackendTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' event order [{expectedNames}] but found [{actualNames}].");
        }

        return eventStore;
    }

    /// <summary>
    /// Asserts that the specified stream has the expected version.
    /// </summary>
    public static IKrackendTestEventStore ShouldHaveVersion(
        this IKrackendTestEventStore eventStore,
        string streamName,
        string streamId,
        long expectedVersion)
    {
        var stream = EventStreamReference.Create(streamName, streamId);
        var actualVersion = ReadStream(eventStore, stream)
            .Select(x => x.StreamVersion)
            .DefaultIfEmpty(0)
            .Max();

        if (actualVersion != expectedVersion)
            throw new KrackendTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' version '{expectedVersion}' but found '{actualVersion}'.");

        return eventStore;
    }

    /// <summary>
    /// Asserts that any stored event has metadata matching the specified key and value.
    /// </summary>
    public static IKrackendTestEventStore ShouldHaveMetadata(
        this IKrackendTestEventStore eventStore,
        string key,
        object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var events = ReadAll(eventStore);

        if (!events.Any(x => x.Metadata.TryGetValue(key, out var actual) && ValuesEqual(actual, value)))
            throw new KrackendTestingAssertionException($"Expected metadata '{key}' with value '{value}' to exist.");

        return eventStore;
    }

    /// <summary>
    /// Asserts that the specified stream contains a serialized payload matching the provided JSON.
    /// </summary>
    public static IKrackendTestEventStore ShouldHaveSerializedPayload(
        this IKrackendTestEventStore eventStore,
        string streamName,
        string streamId,
        string expectedJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedJson);

        var stream = EventStreamReference.Create(streamName, streamId);
        var expected = NormalizeJson(expectedJson);
        var found = ReadStream(eventStore, stream)
            .Any(x => NormalizeJson(x.SerializedPayload) == expected);

        if (!found)
            throw new KrackendTestingAssertionException($"Expected stream '{stream.Name}/{stream.Id}' to contain serialized payload '{expected}'.");

        return eventStore;
    }

    private static IReadOnlyCollection<TestEventEnvelope> ReadStream(
        IKrackendTestEventStore eventStore,
        EventStreamReference stream)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        return eventStore.ReadAsync(stream).GetAwaiter().GetResult();
    }

    private static IReadOnlyCollection<TestEventEnvelope> ReadAll(IKrackendTestEventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        return eventStore.ReadAllAsync().GetAwaiter().GetResult();
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
