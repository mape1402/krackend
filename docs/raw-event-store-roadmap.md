# Raw Event Store Roadmap

## Goal

Add an untyped event store API for centralized event logs that receive events from many bounded contexts without requiring CLR event classes for every event name and schema version.

The existing typed event sourcing API remains unchanged. Bounded contexts can continue using typed events, reducers, snapshots, and state rehydration. The raw API is additive and targets centralized storage, auditing, forwarding, and integration scenarios.

## Design Principles

- Keep typed event sourcing as the primary write-model API for bounded contexts.
- Do not require centralized event stores to reference every bounded-context event assembly.
- Store raw JSON payloads using the same envelope/table shape as typed events.
- Preserve optimistic concurrency through `ExpectedVersion`.
- Preserve existing envelope metadata: stream, version, global position, correlation, causation, tenant, user, source, and metadata.
- Do not deserialize raw payloads into CLR event types in the central store path.
- Keep reducer/state/snapshot behavior out of the raw API.
- Allow future validation through JSON Schema or another schema catalog without making it mandatory.

## Proposed API

Add `RawEventData`:

```csharp
public sealed record RawEventData(
    string EventType,
    SemanticVersion EventSchemaVersion,
    string Payload,
    string? Metadata = null);
```

Add `IRawEventStore`:

```csharp
public interface IRawEventStore
{
    Task<IReadOnlyCollection<EventEnvelope>> AppendRawAsync(
        string streamName,
        string streamId,
        ExpectedVersion expectedVersion,
        IReadOnlyCollection<RawEventData> events,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EventEnvelope>> ReadStreamAsync(
        string streamName,
        string streamId,
        long fromVersion,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<long> GetCurrentVersionAsync(
        string streamName,
        string streamId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EventEnvelope>> ReadFromAsync(
        string streamName,
        long afterGlobalPosition,
        int maxCount,
        CancellationToken cancellationToken = default);
}
```

## Implementation Steps

1. Done: add raw contracts to `Krackend.EventSourcing.Abstractions`.
2. Done: add raw envelope creation support in the core runtime.
3. Done: implement `IRawEventStore` in `InMemoryEventStore`.
4. Done: implement `IRawEventStore` in `EntityFrameworkEventStore<TDbContext>`.
5. Done: register `IRawEventStore` in core and EF dependency injection.
6. Done: add tests for raw append/read, metadata preservation, expected versions, and coexistence with typed events.
7. Done: update documentation with centralized event store guidance and examples.

## Future Work

- Optional JSON Schema validation per `EventType + EventSchemaVersion`.
- Optional raw event ingestion endpoint helpers.
- Optional event forwarding/subscription helpers over `IEventLogReader`/`IRawEventStore`.
- Optional package for central event store operational tooling.
