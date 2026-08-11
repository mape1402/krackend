# Krackend

Modular backend building blocks for .NET services.

[![Build](https://github.com/mape1402/krackend/actions/workflows/CI.yml/badge.svg)](https://github.com/mape1402/krackend/actions/workflows/CI.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Packages

Event sourcing packages:

```bash
dotnet add package Krackend.EventSourcing
dotnet add package Krackend.EventSourcing.EntityFrameworkCore
dotnet add package Krackend.EventSourcing.Analyzers
dotnet add package Krackend.EventSourcing.Testing
```

Optional event sourcing packages:

```bash
dotnet add package Krackend.EventSourcing.Abstractions
```

Saga orchestration packages:

```bash
dotnet add package Krackend.Sagas.Orchestrations
dotnet add package Krackend.Sagas.Orchestrations.Abstractions
dotnet add package Krackend.Sagas.Orchestrations.Contracts
dotnet add package Krackend.Sagas.Orchestrations.Design
dotnet add package Krackend.Sagas.Orchestrations.Design.Interaction
dotnet add package Krackend.Sagas.Orchestrations.Design.Storage.SqlServer
dotnet add package Krackend.Sagas.Orchestrations.Design.WebUI
dotnet add package Krackend.Sagas.Orchestrations.Distribution
dotnet add package Krackend.Sagas.Orchestrations.Distribution.Interaction
dotnet add package Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer
dotnet add package Krackend.Sagas.Orchestrations.Distribution.WebUI
dotnet add package Krackend.Sagas.Orchestrations.Messaging.Abstractions
dotnet add package Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer
dotnet add package Krackend.Sagas.Orchestrations.Runtime.WebUI
dotnet add package Krackend.Sagas.Orchestrations.Security
dotnet add package Krackend.Sagas.Orchestrations.Security.Interaction
dotnet add package Krackend.Sagas.Orchestrations.Security.Storage.SqlServer
dotnet add package Krackend.Sagas.Orchestrations.Security.WebUI
dotnet add package Krackend.Sagas.Orchestrations.Web
dotnet add package Krackend.Sagas.Orchestrations.WebUI.Shell
```

Optional saga orchestration packages:

```bash
dotnet add package Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap
dotnet add package Krackend.Sagas.Orchestrations.Messaging.Pigeon
```

## Event Sourcing

`Krackend.EventSourcing` provides a modular write-model runtime for event sourcing:

- event and state schema versioning
- expected-version appends
- paged stream reads
- state rehydration with reducers
- snapshots of state
- EF Core event store adapter
- raw JSON event appends for centralized event stores
- runtime diagnostics
- Roslyn analyzers
- testing helpers

Minimal setup:

```csharp
services.AddKrackendEventSourcing(options =>
{
    options.ScanAssemblyContaining<Program>();
    options.Stores.Add("customers", store => store.TableName = "CustomerEvents");
});

services.AddEventSourcedInitialStateFactory<CustomerState, CustomerInitialStateFactory>();
services.AddKrackendEntityFrameworkEventStore<AppDbContext>();
```

Usage:

```csharp
[EventStream("customers")]
public sealed record RenameCustomer(string CustomerId, string Name)
    : IEventStreamCommand
{
    public string StreamId => CustomerId;
}

await customerService.ExecuteAsync(new RenameCustomer("customer-001", "New Name"));
```

Centralized raw event store usage:

```csharp
var rawEventStore = provider.GetRequiredService<IRawEventStore>();

await rawEventStore.AppendRawAsync(
    streamName: "integration-events",
    streamId: "customers:customer-001",
    expectedVersion: ExpectedVersion.Any,
    events:
    [
        new RawEventData(
            "Customers.CustomerRenamed",
            "1.0.0",
            "{\"customerId\":\"customer-001\",\"name\":\"New Name\"}")
    ]);
```

See [docs/event-sourcing.md](docs/event-sourcing.md) for the full guide.

Samples:

- `samples/Krackend.EventSourcing.Sqlite.Sample`: typed event-sourced write model with SQLite.
- `samples/Krackend.EventSourcing.Centralized.Sample`: centralized raw JSON event store with SQLite.
- `samples/Krackend.EventSourcing.Pelican.Sample`: exploratory Pelican/template integration.
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample`: host that mounts saga orchestration runtime libraries and Runtime WebUI.
- `samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample`: control-plane host that mounts Design, Distribution, Security, WebUI, Bootstrap, and EF migrations.

## Sagas Orchestrations

`Krackend.Sagas.Orchestrations` provides saga orchestration runtime and control-plane modules as composable libraries. Hosts can reference only the pieces they need:

- `Krackend.Sagas.Orchestrations.Abstractions` for artifact, primitive, runtime and storage contracts
- `Krackend.Sagas.Orchestrations` for runtime services, in-memory trigger intake and engine execution
- `Krackend.Sagas.Orchestrations.Contracts` for cross-module integration events
- `Krackend.Sagas.Orchestrations.Design*` for orchestration definition authoring, interaction services, SQL Server storage and Razor UI
- `Krackend.Sagas.Orchestrations.Distribution*` for artifact release, promotion, runtime-node distribution, SQL Server storage and Razor UI
- `Krackend.Sagas.Orchestrations.Security*` for teams/security interaction, SQL Server storage and Razor UI
- `Krackend.Sagas.Orchestrations.Messaging.Abstractions` for broker-neutral publishing, consuming and orchestration metadata
- `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer` for SQL Server runtime storage
- `Krackend.Sagas.Orchestrations.Web` for minimal API host endpoints
- `Krackend.Sagas.Orchestrations.WebUI.Shell` and `Krackend.Sagas.Orchestrations.Runtime.WebUI` for Razor UI modules
- `Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap` for wiring Design, Distribution, Security and WebUI into a control-plane host
- `Krackend.Sagas.Orchestrations.Messaging.Pigeon` for the optional Pigeon messaging adapter

Minimal host setup:

```csharp
builder.Services.AddKrackendSagasOrchestrationsRuntime(options =>
{
    options.EnvironmentKey = "local";
});

builder.Services.AddKrackendSagasOrchestrationsEngine();
builder.Services.AddKrackendSagasOrchestrationsInMemoryIntakeBuffer();
builder.Services.AddKrackendSagasOrchestrationsMessaging();
builder.Services.AddKrackendSagasOrchestrationsSqlServer(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SagasRuntime"));
});
builder.Services.AddKrackendSagasOrchestrationsWeb();

app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();
```

See [docs/sagas-orchestrations.md](docs/sagas-orchestrations.md) for the full package, function, endpoint, storage, WebUI, bootstrap, and migration-ownership reference. See [docs/sagas-orchestrations-end-to-end.md](docs/sagas-orchestrations-end-to-end.md) for the complete Orchestrator walkthrough with models, lifecycle steps, distribution flow, runtime execution, playbooks, risks, and diagnostics.

## Event Sourcing Testing

`Krackend.EventSourcing.Testing` provides reducer/decider helpers and DI-friendly services for testing event sourcing flows without a real event store. It is meant for application tests, package adapters, and external test hosts that need to assert event sourcing behavior without booting EF Core, SQL Server, SQLite, or a production event store.

```csharp
services.AddKrackendEventSourcingTesting();

var eventStore = provider.GetRequiredService<IEventSourcingTestEventStore>();
var assertions = provider.GetRequiredService<IEventSourcingTestAssertions>();
var stream = EventStreamReference.Create("customers", customerId);

await eventStore.AppendAsync(stream, [new CustomerCreated(customerId)]);

await assertions.ShouldHaveEventAsync<CustomerCreated>("customers", customerId);
await assertions.ShouldHaveVersionAsync("customers", customerId, 1);
```

Append with expected-version behavior and metadata:

```csharp
await eventStore.AppendAsync(
    stream,
    ExpectedVersion.NoStream,
    [new CustomerCreated(customerId)],
    new Dictionary<string, object?>
    {
        ["correlation-id"] = correlationId
    });
```

Available assertions include:

- stream existence
- event type
- event order
- stream version
- metadata
- serialized payload

Failure simulation:

```csharp
eventStore.FailNextAppendWithConcurrencyConflict(stream);
```

External test host integrations can use:

```csharp
services.AddKrackendEventSourcingTestingAdapter();
```

That adapter-friendly registration allows wrappers such as:

```csharp
testHost.UseKrackendTesting();
```
