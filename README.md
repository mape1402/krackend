# Krackend

Modular backend building blocks for .NET services.

[![Build](https://github.com/mape1402/krackend/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/mape1402/krackend/actions/workflows/build-and-release.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Packages

Event sourcing packages:

```bash
dotnet add package Krackend.EventSourcing.Abstractions
dotnet add package Krackend.EventSourcing
dotnet add package Krackend.EventSourcing.EntityFrameworkCore
dotnet add package Krackend.EventSourcing.Projections
dotnet add package Krackend.EventSourcing.Analyzers
dotnet add package Krackend.EventSourcing.Testing
```

Saga orchestration core packages:

```bash
dotnet add package Krackend.Sagas.Orchestrations.Abstractions
dotnet add package Krackend.Sagas.Orchestrations.Contracts
```

Saga orchestration control-plane packages:

```bash
dotnet add package Krackend.Sagas.Orchestrations.ControlPlane
dotnet add package Krackend.Sagas.Orchestrations.ControlPlane.Application
dotnet add package Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework
dotnet add package Krackend.Sagas.Orchestrations.ControlPlane.WebUI
dotnet add package Krackend.Sagas.Orchestrations.ControlPlane.Api
```

Saga orchestration runtime packages:

```bash
dotnet add package Krackend.Sagas.Orchestrations.Runtime
dotnet add package Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework
dotnet add package Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule
dotnet add package Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
dotnet add package Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis
dotnet add package Krackend.Sagas.Orchestrations.Runtime.ButterMorph
dotnet add package Krackend.Sagas.Orchestrations.Runtime.WebUI
dotnet add package Krackend.Sagas.Orchestrations.Runtime.Api
```

Saga orchestration client and schema packages:

```bash
dotnet add package Krackend.Sagas.Orchestrations.Client
dotnet add package Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon
dotnet add package Krackend.Sagas.Orchestrations.Client.Spider
dotnet add package Krackend.Sagas.Orchestrations.SchemaRegistry.Abstractions
dotnet add package Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl
dotnet add package Krackend.Sagas.Orchestrations.WebUI.Shell
```

Security packages:

```bash
dotnet add package Krackend.Security
dotnet add package Krackend.Security.Storage.EntityFramework
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
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample`: host that mounts runtime storage, Mule buffering, Pigeon messaging, Redis gossip, ButterMorph, and Runtime WebUI.
- `samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample`: control-plane host that mounts design, distribution, security, WebUI, artifact delivery endpoints, and EF migrations.

## Sagas Orchestrations

Krackend Sagas Orchestrations is split into composable libraries so the runtime, control plane, transport adapters, client integrations, and UI modules can evolve independently:

- `Krackend.Sagas.Orchestrations.ControlPlane*` captures orchestration definitions, versions, releases, runtime nodes, credentials, and artifact delivery.
- `Krackend.Sagas.Orchestrations.Runtime*` consumes immutable artifacts, projects ingress configuration, runs durable Mule-backed work, dispatches transport-agnostic commands, tracks instances, and exposes runtime diagnostics.
- `Krackend.Sagas.Orchestrations.Client*` lets services start or answer orchestration work without changing business payloads. Pigeon is one messaging adapter and Spider is a pipeline extension over the client core.
- `Krackend.Sagas.Orchestrations.SchemaRegistry*` keeps schema resolution provider-neutral, with KnOwl Control Plane available as the plug-in adapter for deployed ButterMorph contracts.
- `Krackend.Sagas.Orchestrations.WebUI.Shell`, `ControlPlane.WebUI`, and `Runtime.WebUI` provide Razor UI modules for host applications.
- `Krackend.Sagas.Orchestrations.ControlPlane.Api` and `Runtime.Api` expose optional REST endpoints over the same application/runtime services used by the WebUI modules.
- `Krackend.Security*` keeps authentication in the host and adds provider-agnostic product authorization with subjects, roles, permissions, scopes, bootstrap admins, ASP.NET Core policies, and EF storage.

Security model:

Authentication belongs to the host. Krackend libraries do not configure Entra ID, JWT bearer, cookies, API keys, IdentityServer, Auth0, Keycloak, or any concrete provider. The host authenticates a `ClaimsPrincipal`; Krackend resolves the external subject from configured claims and evaluates product permissions.

```csharp
builder.Services
    .AddAuthentication("HostScheme")
    .AddJwtBearer("HostScheme", options =>
    {
        // Host-owned authentication configuration.
    });

builder.Services.AddKrackendSecurity(options =>
{
    options.RequireKnownSubject = true;
    options.Subject.Provider = "entra-id";
    options.BootstrapAdmins.Add(new KrackendBootstrapSubject
    {
        Provider = "entra-id",
        SubjectId = "external-user-object-id"
    });
});

builder.Services.AddKrackendSecurityStorageEntityFramework(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Security"));
});
```

REST APIs can keep an optional global policy and also opt into granular product policies:

```csharp
app.MapKrackendOrchestrationsControlPlaneApi(options =>
{
    options.Authorization.UseKrackendDefaults();
});

app.MapKrackendOrchestrationsRuntimeApi(options =>
{
    options.Authorization.UseKrackendDefaults();
});

app.MapKrackendSecurityAdministrationApi();
```

The WebUI and REST API are sibling entry points over the same application/runtime services:

```text
WebUI -> Application/Runtime services
API   -> Application/Runtime services
```

KnOwl schema registry setup for a design/control-plane host:

```csharp
builder.Services.AddKrackendKnOwlSchemaRegistry(options =>
{
    options.BaseUri = new Uri("https://knowl-control-plane.local");
    options.ProviderKey = "knowl";
});
```

The KnOwl adapter reads deployed contracts from the KnOwl Control Plane. Event bindings resolve one event artifact, while command bindings can resolve the command request and reply artifacts together so orchestration artifacts keep immutable snapshots for validation, transformation, runtime dispatch, and response handling.

Minimal runtime host setup:

```csharp
builder.Services.AddOrchestratorRuntimeWebUI(options =>
{
    options.RoutePrefix = "runtime";
});

builder.Services.AddOrchestratorRuntimeStorageEntityFramework(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Runtime"));
});

builder.Services
    .AddKrackendOrchestrationsRuntime()
    .AddPigeon(builder.Configuration)
    .AddMule(mule =>
    {
        mule.UseEntityFrameworkCore<RuntimeDbContext>();
    });

builder.Services.AddKrackendOrchestrationsRuntimeButterMorph();

app.MapOrchestratorRuntimeDistributionEndpoints();
app.MapKrackendOrchestrationsRuntimeApi();
app.MapOrchestratorRuntimeReactiveHub();
```

Minimal control-plane host setup:

```csharp
builder.Services.AddOrchestratorControlPlane(options =>
{
    options.AdminRootPath = "admin";
    options.ConfigureStorage = db =>
        db.UseSqlServer(builder.Configuration.GetConnectionString("ControlPlane"));
});

app.MapOrchestratorArtifactDeliveryEndpoints();
app.MapKrackendOrchestrationsControlPlaneApi();
```

See [docs/sagas-orchestrations.md](docs/sagas-orchestrations.md) for the full package, function, endpoint, storage, WebUI, bootstrap, and migration-ownership reference. See [docs/sagas-orchestrations-end-to-end.md](docs/sagas-orchestrations-end-to-end.md) for the complete Orchestrator walkthrough with models, lifecycle steps, distribution flow, runtime execution, playbooks, risks, and diagnostics.

## Release

Packages are produced only by explicit `dotnet pack` or by the `Build and Release` workflow. The repository `.release` file contains the next release tag, for example `v2.2.0`; when that marker changes on `main`, the workflow validates the changelog section, creates the release branch/tag, packs all source libraries, and publishes the NuGet artifacts.

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
