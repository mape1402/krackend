namespace Krackend.EventSourcing.Testing;

/// <summary>
/// Provides event sourcing assertions over the configured in-memory test store.
/// </summary>
public interface IEventSourcingTestAssertions
{
    /// <summary>
    /// Asserts that the specified stream exists.
    /// </summary>
    Task ShouldHaveStreamAsync(string streamName, string streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asserts that the specified stream contains an event of the requested CLR type.
    /// </summary>
    Task ShouldHaveEventAsync<TEvent>(string streamName, string streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asserts that the specified stream contains exactly the requested event types in order.
    /// </summary>
    Task ShouldHaveEventsInOrderAsync(
        string streamName,
        string streamId,
        IReadOnlyCollection<Type> eventTypes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asserts that the specified stream has the expected version.
    /// </summary>
    Task ShouldHaveVersionAsync(
        string streamName,
        string streamId,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asserts that any stored event has metadata matching the specified key and value.
    /// </summary>
    Task ShouldHaveMetadataAsync(string key, object? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asserts that the specified stream contains a serialized payload matching the provided JSON.
    /// </summary>
    Task ShouldHaveSerializedPayloadAsync(
        string streamName,
        string streamId,
        string expectedJson,
        CancellationToken cancellationToken = default);
}
