# Krackend Event Sourcing

`Krackend.EventSourcing` provides the core write-model runtime for persisting events, rehydrating state with reducers, creating state snapshots, and building event envelopes with technical metadata.

The library is split into packages so the core runtime does not take hard dependencies on storage adapters, projections, or ecosystem integrations.

## Packages

- `Krackend.EventSourcing.Abstractions`: public contracts, attributes, envelopes, stores, snapshots, streams, registries, and exceptions.
- `Krackend.EventSourcing`: core runtime, default serializers, schema registries, reducers, rehydration, snapshots, metadata collection, stream routing, and base DI.
- `Krackend.EventSourcing.EntityFrameworkCore`: EF Core storage adapter for event stores, snapshots, and snapshot candidates integrated into the application `DbContext`.
- `Krackend.EventSourcing.Projections`: optional projection runtime.
- `Krackend.EventSourcing.SpiderExtensions`: optional Spider integration package.
- `Krackend.EventSourcing.PelicanExtensions`: optional Pelican/template integration package.
- `Krackend.EventSourcing.Analyzers`: Roslyn diagnostics for common schema and reducer mistakes.
- `Krackend.EventSourcing.Testing`: test helpers for streams, reducers, deciders, and initial states.

## Events

Every persisted event should declare a stable schema:

```csharp
[EventSchema("CustomerBalanceMoved", "1.1.0")]
public sealed record CustomerBalanceMoved(string CustomerId, decimal Amount, decimal Balance);
```

`EventSchema.Name` and `EventSchema.Version` are part of the persisted contract. Two CLR types may represent different versions of the same business event, but they cannot share the same `name + version`.

Attribute-based registration is the recommended path:

```csharp
registry.Register<CustomerBalanceMoved>();
```

Manual registration is still supported for dynamic scenarios:

```csharp
registry.Register<CustomerBalanceMoved>("CustomerBalanceMoved", "1.1.0");
```

## State

State has its own schema:

```csharp
[StateSchema("CustomerState", "1.0.0")]
public sealed record CustomerState(string CustomerId, string Name, decimal Balance);
```

The state schema is not the event schema. An event can stay stable while state changes, and state can stay stable while event contracts evolve.

States marked with `[StateSchema]` are registered automatically when the assembly is scanned:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.ScanAssemblyContaining<CustomerState>();
});
```

You can also register state schemas manually with `StateSchemaRegistry` for dynamic scenarios.

For application services, register the initial state once in DI:

```csharp
services.AddEventSourcedInitialState(() => CustomerState.Empty);
```

If the initial state needs dependencies or dynamic rules, register a factory:

```csharp
services.AddEventSourcedInitialStateFactory<CustomerState, CustomerInitialStateFactory>();
```

Concrete implementations of `IInitialStateFactory<TState>` are also discovered automatically during assembly scanning:

```csharp
public sealed class CustomerInitialStateFactory : IInitialStateFactory<CustomerState>
{
    public ValueTask<CustomerState> CreateAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(CustomerState.Empty);
    }
}
```

You can also register a delegate with access to the container:

```csharp
services.AddEventSourcedInitialStateFactory<CustomerState>((provider, cancellationToken) =>
{
    var tenant = provider.GetRequiredService<ICurrentTenant>();

    return ValueTask.FromResult(CustomerState.ForTenant(tenant.Id));
});
```

After that, the normal execution path does not require passing `initialState` per call:

```csharp
await customerService.ExecuteAsync(new RenameCustomer("customer-001", "New Name"));
```

Overloads that accept `initialState` remain available for tests and advanced scenarios.

## Reducers

Every event version used during rehydration must have an exact reducer:

```csharp
reducers.Register<CustomerState, CustomerBalanceMovedV1>((state, @event) =>
    state with { Balance = @event.Balance });

reducers.Register<CustomerState, CustomerBalanceMovedV2>((state, @event) =>
    state with { Balance = @event.NewBalance });
```

If a reducer is missing, the library throws `EventReducerNotRegisteredException`. Events are not silently ignored during rehydration.

## Reads

The public API does not expose unbounded stream reads.

Use:

```csharp
ReadStreamAsync(streamName, streamId, fromVersion, maxCount)
```

Rehydration calculates `fromVersion`:

- without snapshot: `1`
- with snapshot: `snapshot.StreamVersion + 1`

The recommended overload resolves the initial state through `IInitialStateFactory<TState>`:

```csharp
var state = await rehydrator.RehydrateAsync<CustomerState>("customers", "customer-001");
```

## Append

Append operations should use expected versions:

```csharp
ExpectedVersion.Any
ExpectedVersion.NoStream
ExpectedVersion.Exact(version)
```

`Any` exists for flows that intentionally skip optimistic version checks, such as committed events created by hooks. For strict event-sourced write flows, prefer `Exact(version)`.

## Snapshots

A snapshot is a snapshot of state:

```txt
events -> reducers -> TState -> snapshot payload
```

It is not a snapshot of a request, envelope, EF entity, or projection.

Snapshot tables store technical metadata:

- `StreamName`
- `StreamId`
- `StreamVersion`
- `StateType`
- `StateSchemaVersion`
- `Payload`
- `CreatedAt`

`Payload` contains only the serialized state.

Snapshot generation should run outside the request path: background worker, scheduled job, or maintenance process. The hot path can read snapshots, but should not depend on creating them.

The processor can also resolve the initial state through a factory:

```csharp
await snapshotProcessor.ProcessPendingAsync(maxCount: 100);
```

## EF Core

The EF Core adapter integrates event store tables into the application `DbContext`:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });
});

services.AddKrackendEntityFrameworkEventStore<AppDbContext>();
```

The default table name is `Events`.

## Errors

Public exceptions live under `Krackend.EventSourcing.Diagnostics`:

- `EventSchemaMissingException`
- `DuplicateEventSchemaException`
- `EventTypeNotRegisteredException`
- `EventReducerNotRegisteredException`
- `StateSchemaMissingException`
- `DuplicateStateSchemaException`
- `StateTypeNotRegisteredException`
- `SnapshotStateSchemaMismatchException`
- `EventPayloadDeserializationException`
- `SnapshotDeserializationException`
- `SnapshotSerializerMissingException`
- `EventStoreConcurrencyException`

## Analyzers

The analyzers currently report:

- `KES0001`: two CLR types declare the same `EventSchema(name, version)`.
- `KES0002`: a reducer handles an event without `[EventSchema]`.
- `KES0003`: two CLR types declare the same `StateSchema(name, version)`.
- `KES0004`: a reducer handles state without `[StateSchema]`.
- `KES0005`: an initial state factory creates state without `[StateSchema]`.

These diagnostics complement runtime errors. Manual registration is still supported, so not every configuration can be validated statically.

## Testing

`Krackend.EventSourcing.Testing` includes helpers for tests:

```csharp
var initialState = new TestInitialStateFactory<CustomerState>(CustomerState.Empty);

var envelopes = EventStreamBuilder
    .ForStream("customers", "customer-001")
    .Register<CustomerCreated>()
    .Add(new CustomerCreated("customer-001", "Sample Customer", "customer@example.test"))
    .Build();
```

It also includes `ReducerTest` and `DeciderTest` for testing reducers and deciders without starting storage.
