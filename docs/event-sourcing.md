# Krackend Event Sourcing

`Krackend.EventSourcing` is a write-model runtime for .NET services that persist committed events, rehydrate state with reducers, create snapshots, and store event envelopes with technical metadata.

The core package is storage-agnostic. EF Core, testing helpers, analyzers, and ecosystem integrations live in separate packages.

## Install

For the core runtime:

```bash
dotnet add package Krackend.EventSourcing
```

For EF Core storage:

```bash
dotnet add package Krackend.EventSourcing.EntityFrameworkCore
```

Recommended development packages:

```bash
dotnet add package Krackend.EventSourcing.Analyzers
dotnet add package Krackend.EventSourcing.Testing
```

Optional packages:

```bash
dotnet add package Krackend.EventSourcing.Abstractions
```

## Package Roles

- `Krackend.EventSourcing.Abstractions`: contracts, attributes, envelopes, store interfaces, snapshot interfaces, registries, expected versions, and exceptions.
- `Krackend.EventSourcing`: core runtime, serializers, schema registries, reducers, state rehydration, snapshots, metadata, stream routing, in-memory store, and DI.
- `Krackend.EventSourcing.EntityFrameworkCore`: EF Core event store, snapshot store, snapshot candidate store, model builder integration, and app `DbContext` integration.
- `Krackend.EventSourcing.Analyzers`: Roslyn diagnostics for schema and reducer mistakes.
- `Krackend.EventSourcing.Testing`: test helpers for event streams, reducers, deciders, initial states, and DI-friendly in-memory event sourcing test stores.

## Mental Model

The core library focuses on the write-model event sourcing flow:

```txt
command -> rehydrate state -> decider -> append events -> reduce current state
```

Integrations that append committed events after existing handlers save data belong outside the core package, for example in template, Spider, or Pelican extension packages.

## Minimal Setup

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.ScanAssemblyContaining<CustomerState>();

    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });
});

services.AddEventSourcedInitialStateFactory<CustomerState, CustomerInitialStateFactory>();
services.AddKrackendEntityFrameworkEventStore<AppDbContext>();
```

`ScanAssemblyContaining<T>()` discovers:

- `[EventSchema]` event types
- `[StateSchema]` state types
- `IEventDecider<TState, TCommand>`
- `IEventReducer<TState, TEvent>`
- `ICommandStreamResolver<TCommand>`
- `IInitialStateFactory<TState>`

## Defining Events

Every persisted event should declare an event schema:

```csharp
[EventSchema("CustomerCreated", "1.0.0")]
public sealed record CustomerCreated(
    string CustomerId,
    string Name,
    string Email);
```

The persisted event identity is:

```txt
EventType + EventSchemaVersion
```

Two CLR types can represent two versions of the same event:

```csharp
[EventSchema("CustomerRenamed", "1.0.0")]
public sealed record CustomerRenamedV1(string CustomerId, string Name);

[EventSchema("CustomerRenamed", "1.1.0")]
public sealed record CustomerRenamed(string CustomerId, string Name, string Reason);
```

Each version is resolved independently during rehydration.

Manual registration remains available for advanced scenarios such as generated types, dynamic loading, or custom adapters. For normal application code, prefer `[EventSchema]` plus assembly scanning.

## Defining State

State has its own schema:

```csharp
[StateSchema("CustomerState", "1.0.0")]
public sealed record CustomerState(
    string CustomerId,
    string Name,
    string Email,
    decimal Balance,
    bool IsCreated)
{
    public static CustomerState Empty { get; } =
        new(string.Empty, string.Empty, string.Empty, 0m, false);
}
```

The persisted state identity is:

```txt
StateType + StateSchemaVersion
```

Event schema version and state schema version are independent. An event contract can change without changing state, and state can change without changing an event contract.

## Initial State

The initial state is used when a stream has no snapshot and no events.

For static state:

```csharp
services.AddEventSourcedInitialState(() => CustomerState.Empty);
```

For a factory class with dependencies:

```csharp
public sealed class CustomerInitialStateFactory : IInitialStateFactory<CustomerState>
{
    private readonly ICurrentTenant _tenant;

    public CustomerInitialStateFactory(ICurrentTenant tenant)
    {
        _tenant = tenant;
    }

    public ValueTask<CustomerState> CreateAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(CustomerState.Empty with
        {
            TenantId = _tenant.Id
        });
    }
}

services.AddEventSourcedInitialStateFactory<CustomerState, CustomerInitialStateFactory>();
```

For a delegate with access to DI:

```csharp
services.AddEventSourcedInitialStateFactory<CustomerState>((provider, cancellationToken) =>
{
    var tenant = provider.GetRequiredService<ICurrentTenant>();

    return ValueTask.FromResult(CustomerState.ForTenant(tenant.Id));
});
```

Concrete implementations of `IInitialStateFactory<TState>` are also discovered automatically when their assembly is scanned.

## Commands And Streams

Resolvers answer one question:

```txt
Which event stream should this command append to?
```

An event stream has two parts:

- `StreamName`: the logical event store/stream category, such as `customers` or `orders`.
- `StreamId`: the aggregate/entity/business id, such as `customer-001`.

For most commands, combine `[EventStream]` and `IEventStreamCommand`. The attribute defines the `StreamName`; the interface defines the `StreamId`:

```csharp
[EventStream("customers")]
public sealed record RenameCustomer(string CustomerId, string Name)
    : IEventStreamCommand
{
    public string StreamId => CustomerId;
}
```

`IEventStreamCommand` exists only to support the default resolver. It keeps simple commands from needing a custom resolver class. If you do not like that marker interface on commands, do not use it; implement `ICommandStreamResolver<TCommand>` instead.

Resolution order:

```txt
ICommandStreamResolver<TCommand>
    > [EventStream("name")] + IEventStreamCommand
    > DefaultStreamName + IEventStreamCommand
    > error
```

`DefaultStreamName` is still available as a fallback stream name used by the default command resolver when no `[EventStream]` or command-specific route is configured:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.Routing.DefaultStreamName = "customers";
});
```

Prefer `[EventStream]` for application commands because it keeps the stream name close to the command contract.

For custom stream routing, implement `ICommandStreamResolver<TCommand>`. This is the better option when the stream name/id require more than reading a single command property:

```csharp
public sealed class RenameCustomerStreamResolver
    : ICommandStreamResolver<RenameCustomer>
{
    public EventStreamReference Resolve(RenameCustomer command)
    {
        return EventStreamReference.Create("customers", command.CustomerId);
    }
}
```

Resolvers are discovered automatically during assembly scanning.

Use a custom resolver when:

- the command does not implement `IEventStreamCommand`
- the stream name depends on tenant, module, command type, or configuration
- the stream id needs normalization or composition
- a command maps to a stream that is not obvious from a single property

## Deciders

A decider turns a command and current state into events:

```csharp
public sealed class RenameCustomerDecider
    : IEventDecider<CustomerState, RenameCustomer>
{
    public ValueTask<IReadOnlyCollection<object>> DecideAsync(
        CustomerState state,
        RenameCustomer command,
        CancellationToken cancellationToken = default)
    {
        if (!state.IsCreated)
            throw new InvalidOperationException("Customer must exist before it can be renamed.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Customer name is required.", nameof(command));

        return ValueTask.FromResult<IReadOnlyCollection<object>>([
            new CustomerRenamed(command.CustomerId, command.Name)
        ]);
    }
}
```

Deciders are discovered automatically during assembly scanning.

Deciders are not persisted contracts and normally should not be versioned the way events are. Version events because they are stored forever. Version state because snapshots are stored. A decider is application behavior: evolve it with the application code, and produce whichever event schema version is current for that command. If two command behaviors must coexist, use different command types, feature flags, or separate decider implementations registered intentionally by DI.

## Reducers

A reducer applies one event version to state:

```csharp
public sealed class CustomerRenamedReducer
    : IEventReducer<CustomerState, CustomerRenamed>
{
    public CustomerState Apply(CustomerState state, CustomerRenamed @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Name = @event.Name
        };
    }
}
```

Every event version used during rehydration needs its own reducer:

```csharp
public sealed class CustomerRenamedV1Reducer
    : IEventReducer<CustomerState, CustomerRenamedV1>
{
    public CustomerState Apply(CustomerState state, CustomerRenamedV1 @event)
    {
        return state with
        {
            CustomerId = @event.CustomerId,
            Name = @event.Name
        };
    }
}
```

If a reducer is missing, rehydration fails with `EventReducerNotRegisteredException`. Events are not silently skipped.

## Executing An Event-Sourced Command

Resolve `IEventSourcedApplicationService<TState, TCommand>` from DI:

```csharp
var service = provider.GetRequiredService<
    IEventSourcedApplicationService<CustomerState, RenameCustomer>>();

var result = await service.ExecuteAsync(
    new RenameCustomer("customer-001", "New Name"));
```

The service:

1. Resolves the stream from the command.
2. Resolves the initial state from `IInitialStateFactory<TState>`.
3. Loads the latest snapshot when available.
4. Reads events after the snapshot version in batches.
5. Applies reducers to get current state.
6. Calls the decider.
7. Appends the decided events using `ExpectedVersion.Exact(rehydrated.Version)`.
8. Applies the new events to return the current state.

The result contains:

```csharp
result.PreviousState
result.CurrentState
result.PreviousVersion
result.CurrentVersion
result.CommittedEvents
```

Advanced overloads allow passing `streamName`, `streamId`, or `initialState` explicitly:

```csharp
await service.ExecuteAsync("customers", "customer-001", new RenameCustomer(...));
await service.ExecuteAsync(CustomerState.Empty, new RenameCustomer(...));
```

Prefer the overload without `initialState` for application code.

## Appending Events Directly

Use `IEventStore` when you want to persist events directly:

```csharp
var eventStore = provider.GetRequiredService<IEventStore>();

var envelopes = await eventStore.AppendAsync(
    streamName: "customers",
    streamId: "customer-001",
    expectedVersion: ExpectedVersion.Any,
    events: [new CustomerRenamed("customer-001", "New Name")],
    cancellationToken);
```

Use expected versions deliberately:

```csharp
ExpectedVersion.Any
ExpectedVersion.NoStream
ExpectedVersion.Exact(version)
```

Guidance:

- `Exact(version)`: strict event-sourced write flow with optimistic concurrency.
- `NoStream`: first event must create the stream.
- `Any`: append without optimistic version check, useful when the caller intentionally does not own stream concurrency.

## Appending Raw Events

Use `IRawEventStore` when the service stores events produced by other bounded contexts and should not reference every CLR event type from the whole system.

This is useful for centralized event stores, audit logs, forwarding services, and integration collectors. The raw API stores the same envelope shape as typed event sourcing, but it does not use event schemas from attributes, reducers, state rehydration, or snapshots.

```csharp
var rawEventStore = provider.GetRequiredService<IRawEventStore>();

await rawEventStore.AppendRawAsync(
    streamName: "integration-events",
    streamId: "customers:customer-001",
    expectedVersion: ExpectedVersion.Any,
    events:
    [
        new RawEventData(
            eventType: "Customers.CustomerRenamed",
            eventSchemaVersion: "1.0.0",
            payload: """
            {"customerId":"customer-001","name":"New Name"}
            """,
            metadata: """
            {"producer":"customers-api","messageId":"9ff3b801"}
            """)
    ],
    cancellationToken);
```

Raw events still receive envelope fields from the current `IEventExecutionContext`:

- `CorrelationId`
- `CausationId`
- `UserId`
- `TenantId`
- `Source`
- `OccurredAt`
- `StreamVersion`
- `GlobalPosition`

If `RawEventData.Metadata` is provided, it is stored as-is. If it is omitted, configured envelope metadata providers are collected and serialized into `Metadata`.

Read raw events with the same bounded APIs:

```csharp
var page = await rawEventStore.ReadStreamAsync(
    streamName: "integration-events",
    streamId: "customers:customer-001",
    fromVersion: 1,
    maxCount: 100,
    cancellationToken);

var next = await rawEventStore.ReadFromAsync(
    streamName: "integration-events",
    afterGlobalPosition: 5000,
    maxCount: 500,
    cancellationToken);
```

Use typed `IEventStore` for bounded-context write models that need reducers and rehydration. Use `IRawEventStore` for centralized storage where JSON is the contract and C# event classes would become operational debt.

## Reading Events

There is no unbounded stream-load API.

Read streams in bounded ranges:

```csharp
var events = await eventStore.ReadStreamAsync(
    streamName: "customers",
    streamId: "customer-001",
    fromVersion: 1,
    maxCount: 100,
    cancellationToken);
```

Read the global log through `IEventLogReader`:

```csharp
var reader = provider.GetRequiredService<IEventLogReader>();

var page = await reader.ReadFromAsync(
    streamName: "customers",
    afterGlobalPosition: 0,
    maxCount: 100,
    cancellationToken);
```

Get the current stream version:

```csharp
var version = await eventStore.GetCurrentVersionAsync(
    "customers",
    "customer-001",
    cancellationToken);
```

## Rehydrating State Manually

Use `IStateRehydrator` when you need to rebuild state without executing a command:

```csharp
var rehydrator = provider.GetRequiredService<IStateRehydrator>();

var rehydrated = await rehydrator.RehydrateAsync<CustomerState>(
    "customers",
    "customer-001",
    cancellationToken);

CustomerState state = rehydrated.State;
long version = rehydrated.Version;
```

The recommended overload resolves initial state from `IInitialStateFactory<TState>`.

For tests or custom flows, you can pass the initial state explicitly:

```csharp
var rehydrated = await rehydrator.RehydrateAsync(
    "customers",
    "customer-001",
    CustomerState.Empty,
    cancellationToken);
```

## Metadata, Correlation, And Causation

Register execution context metadata from the current request/message context. Avoid hardcoded values in real applications.

```csharp
public sealed class CurrentRequestEventExecutionContext : IEventExecutionContext
{
    private readonly ICurrentRequestContext _request;

    public CurrentRequestEventExecutionContext(ICurrentRequestContext request)
    {
        _request = request;
    }

    public string? CorrelationId => _request.CorrelationId;

    public string? CausationId => _request.CausationId;

    public string? UserId => _request.UserId;

    public string? TenantId => _request.TenantId;

    public string? Source => _request.Source;
}

services.AddScoped<CurrentRequestEventExecutionContext>();

services.AddEventExecutionContext(provider =>
    provider.GetRequiredService<CurrentRequestEventExecutionContext>());
```

Add custom envelope metadata:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.Envelope.AddMetadata("sample", _ => "customers");
});
```

Every persisted envelope stores:

- `EventId`
- `StreamName`
- `StreamId`
- `StreamVersion`
- `GlobalPosition`
- `EventType`
- `EventSchemaVersion`
- `OccurredAt`
- `CorrelationId`
- `CausationId`
- `UserId`
- `TenantId`
- `Source`
- `Payload`
- `Metadata`

## EF Core Storage

Register EF Core normally:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

Then register event sourcing and the EF adapter:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });
});

services.AddKrackendEntityFrameworkEventStore<AppDbContext>();
```

The adapter integrates event store tables into the application `DbContext`. You do not need to add `DbSet<EventStoreRecord>` manually.

If you want to configure the model explicitly:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.AddKrackendEventStore();
}
```

The default event table is `Events`. Table names should use PascalCase.

## Multiple Event Stores

Configure multiple logical stores:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });

    options.Stores.Add("orders", store =>
    {
        store.TableName = "OrderEvents";
    });
});
```

Use the store name as the stream name when appending or reading:

```csharp
await eventStore.AppendAsync(
    "orders",
    "order-001",
    ExpectedVersion.NoStream,
    [new OrderCreated("order-001")],
    cancellationToken);
```

## Snapshots

Snapshots store serialized state, not requests, envelopes, EF entities, or projections.

```txt
events -> reducers -> TState -> snapshot payload
```

Snapshot metadata includes:

- `StreamName`
- `StreamId`
- `StreamVersion`
- `StateType`
- `StateSchemaVersion`
- `Payload`
- `CreatedAt`

Enable snapshot candidates with a policy:

```csharp
services.AddSingleton<ISnapshotCandidatePolicy>(
    new IntervalSnapshotCandidatePolicy(interval: 100));
```

Process candidates outside the request path:

```csharp
var processor = provider.GetRequiredService<ISnapshotProcessor<CustomerState>>();

var results = await processor.ProcessPendingAsync(
    maxCount: 100,
    cancellationToken);
```

The processor resolves initial state from `IInitialStateFactory<TState>`.

For custom/test flows, an overload accepts explicit initial state:

```csharp
await processor.ProcessPendingAsync(CustomerState.Empty, maxCount: 100, cancellationToken);
```

## Schema Registries

For application code, prefer attributes and scanning:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.ScanAssemblyContaining<CustomerState>();
});
```

That scan registers `[EventSchema]` events and `[StateSchema]` states into the DI-owned registries used by the runtime.

Manual registration exists for advanced tooling, generated types, or adapter scenarios. Do not create separate registry instances in application code unless you are intentionally building an isolated test/tooling registry.

Duplicate `name + version` registrations fail:

- `DuplicateEventSchemaException`
- `DuplicateStateSchemaException`

Unregistered lookups fail:

- `EventTypeNotRegisteredException`
- `StateTypeNotRegisteredException`

## Errors

Public exceptions live under `Krackend.EventSourcing.Diagnostics`:

- `EventSchemaMissingException`
- `DuplicateEventSchemaException`
- `EventTypeNotRegisteredException`
- `EventReducerNotRegisteredException`
- `StateSchemaMissingException`
- `DuplicateStateSchemaException`
- `StateTypeNotRegisteredException`
- `InitialStateNotConfiguredException`
- `SnapshotStateSchemaMismatchException`
- `EventPayloadDeserializationException`
- `SnapshotDeserializationException`
- `SnapshotSerializerMissingException`
- `EventStoreConcurrencyException`

## Analyzers

Current diagnostics:

- `KES0001`: two CLR types declare the same `EventSchema(name, version)`.
- `KES0002`: a reducer handles an event without `[EventSchema]`.
- `KES0003`: two CLR types declare the same `StateSchema(name, version)`.
- `KES0004`: a reducer handles state without `[StateSchema]`.
- `KES0005`: an initial state factory creates state without `[StateSchema]`.

Manual registration is still supported, so not every valid configuration can be inferred statically.

## Testing

Use `Krackend.EventSourcing.Testing` for test helpers:

```csharp
var initialState = new TestInitialStateFactory<CustomerState>(CustomerState.Empty);
```

Build envelopes for rehydration tests:

```csharp
var envelopes = EventStreamBuilder
    .ForStream("customers", "customer-001")
    .Register<CustomerCreated>()
    .Add(new CustomerCreated("customer-001", "Sample Customer", "customer@example.test"))
    .Build();
```

Test reducers:

```csharp
var state = ReducerTest.Apply(
    CustomerState.Empty,
    new CustomerCreated("customer-001", "Sample Customer", "customer@example.test"),
    (current, @event) => current with
    {
        CustomerId = @event.CustomerId,
        Name = @event.Name,
        Email = @event.Email,
        IsCreated = true
    });
```

Test deciders:

```csharp
var events = await DeciderTest.DecideAsync(
    new CreateCustomerDecider(),
    CustomerState.Empty,
    new CreateCustomer("customer-001", "Sample Customer", "customer@example.test"));
```

Register the DI-friendly in-memory test store when an application test or external test host needs event-store behavior without a real storage adapter:

```csharp
services.AddKrackendEventSourcingTesting();

var eventStore = provider.GetRequiredService<IEventSourcingTestEventStore>();
var assertions = provider.GetRequiredService<IEventSourcingTestAssertions>();
var stream = EventStreamReference.Create("customers", "customer-001");

await eventStore.AppendAsync(stream, [new CustomerCreated("customer-001", "Sample Customer", "customer@example.test")]);

await assertions.ShouldHaveEventAsync<CustomerCreated>("customers", "customer-001");
await assertions.ShouldHaveVersionAsync("customers", "customer-001", 1);
```

External test hosts can use the adapter registration and wrap it with their own host-specific API:

```csharp
services.AddKrackendEventSourcingTestingAdapter();

var adapter = provider.GetRequiredService<IEventSourcingTestingAdapter>();
```

## Complete Example

```csharp
var services = new ServiceCollection();

services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.ScanAssemblyContaining<CustomerState>();
    options.Stores.Add("customers", store => store.TableName = "CustomerEvents");
});

services.AddEventSourcedInitialStateFactory<CustomerState>((_, _) =>
    ValueTask.FromResult(CustomerState.Empty));

services.AddKrackendEntityFrameworkEventStore<AppDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var service = scope.ServiceProvider.GetRequiredService<
    IEventSourcedApplicationService<CustomerState, CreateCustomer>>();

var result = await service.ExecuteAsync(
    new CreateCustomer("customer-001", "Sample Customer", "customer@example.test"));

Console.WriteLine(result.CurrentVersion);
Console.WriteLine(result.CurrentState.Name);
```

## Samples

The repository contains:

- `samples/Krackend.EventSourcing.Sqlite.Sample`: compact event-sourced application service flow with SQLite.
- `samples/Krackend.EventSourcing.Centralized.Sample`: centralized raw JSON event store flow with SQLite.
- `samples/Krackend.EventSourcing.Pelican.Sample`: exploratory Pelican/template integration sample. The integration pieces are intentionally outside the core event sourcing runtime.
