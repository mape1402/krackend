# Krackend Event Sourcing Roadmap

## Goal

Make `Krackend.EventSourcing` a stable, modular, and extensible event sourcing library for .NET services.

The stable release should keep the core small and explicit, separate storage adapters from contracts, support event and state schema versioning, keep snapshots safe, fail loudly when critical pieces are missing, and provide analyzers that catch common mistakes before runtime.

## Design Principles

- The core runtime owns event sourcing behavior, not concrete storage.
- Events are identified by `EventSchema(name, version)`.
- State is identified by `StateSchema(name, version)`, separate from event schemas.
- Persisted events are resolved by `EventType + EventSchemaVersion`.
- Snapshot payloads contain state only.
- Missing reducers must fail.
- Manual event and state registration must remain available for dynamic scenarios.
- There is no reflection-based event application.
- There are no mandatory aggregate base classes.
- Production APIs must not load complete streams without limits.
- EF Core, ADO, Mongo, or other stores are interchangeable adapters.
- Spider, Pelican, templates, and other ecosystem libraries must stay outside the core runtime. Dedicated extension packages can be created later only when their integration contracts are stable.
- Projections belong to read-model infrastructure, not the write-model core.

## Package Layout

```txt
Krackend.EventSourcing.Abstractions
Krackend.EventSourcing
Krackend.EventSourcing.EntityFrameworkCore
Krackend.EventSourcing.Analyzers
Krackend.EventSourcing.Testing
```

`Krackend.EventSourcing.Projections` remains in the solution as internal read-model infrastructure, but it is not part of the NuGet package set for the stable event sourcing release.

## Implemented

1. Removed unbounded `LoadAsync` from `IEventStore`.
2. Added paged reads through `ReadStreamAsync(streamName, streamId, fromVersion, maxCount)`.
3. Added expected version append modes: `Any`, `NoStream`, and `Exact(version)`.
4. Added event schema versioning through `[EventSchema]` and `SemanticVersion`.
5. Added state schema versioning through `[StateSchema]`.
6. Added state schema metadata to snapshots: `StateType` and `StateSchemaVersion`.
7. Added `IInitialStateFactory<TState>` and DI registration APIs.
8. Added automatic discovery for deciders, reducers, stream resolvers, event schemas, state schemas, and initial state factories.
9. Added `IStateSchemaRegistry` and duplicate state schema detection.
10. Split abstractions into `Krackend.EventSourcing.Abstractions`.
11. Split EF Core storage into `Krackend.EventSourcing.EntityFrameworkCore`.
12. Added typed diagnostic exceptions.
13. Added basic Roslyn analyzers.
14. Added `Krackend.EventSourcing.Testing`.
15. Updated SQLite and Pelican samples.
16. Removed placeholder Spider and Pelican extension projects until their integration contracts are stable.

## Remaining Work

### Integration Packages

Move the sample-level Pelican hook implementation into a dedicated extension package once the template contracts are stable:

- committed event mapping
- request/entity event creation
- post-save hooks
- stream resolution helpers
- template-friendly DI extensions

Move Spider-specific behavior into a dedicated extension package only when the integration surface is clear.

### Snapshots

Add an optional snapshot worker package or extension:

```csharp
services.AddKrackendSnapshotWorker<CustomerState>(options =>
{
    options.Interval = TimeSpan.FromMinutes(5);
    options.BatchSize = 100;
});
```

Snapshot creation should stay outside the request path.

### State Migration

Define explicit state snapshot migration contracts:

```csharp
public interface IStateSnapshotMigrator
{
    string StateType { get; }
    SemanticVersion FromSchemaVersion { get; }
    SemanticVersion ToSchemaVersion { get; }
    string Migrate(string payload);
}
```

Migrations should run only when a complete migration path exists.

### Analyzers

Current analyzer rules:

- duplicate event schemas
- reducer events missing `[EventSchema]`
- duplicate state schemas
- reducer states missing `[StateSchema]`
- initial state factories using states missing `[StateSchema]`

Future rules can include:

- command types without stream resolution
- event-sourced application services without initial state factories
- suspicious manual schema/version mismatches
- deprecated or dangerous API usage

Reducer coverage by event version should be added only when there is a reliable convention or explicit configuration that defines which state owns which event versions.

### Testing Package

Expand `Krackend.EventSourcing.Testing` with:

- in-memory stores focused on assertions
- snapshot fixtures
- event stream assertions
- decider scenario DSL
- reducer scenario DSL

### Release Preparation

Before tagging:

- run full build and tests
- inspect generated `.nupkg` files
- verify package README content
- verify public XML documentation
- run the Pelican sample against SQL Server
- decide the first public version tag

## Stable Release Criteria

- Core has no EF Core dependency.
- EF Core lives in a storage adapter package.
- Optional integrations live outside the core runtime.
- Event and state schemas are explicit and versioned.
- Snapshots store state payloads and state schema metadata.
- Missing reducers fail.
- Missing event/state schemas fail.
- Duplicate event/state schemas fail.
- Manual registration remains supported.
- The hot path does not require snapshot generation.
- Roslyn analyzers catch common schema mistakes.
- Testing helpers exist.
- Samples demonstrate real usage without personal configuration.
- Documentation is in English.
