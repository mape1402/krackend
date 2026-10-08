# Krackend

[![Build](https://github.com/mape1402/krackend/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/mape1402/krackend/actions/workflows/build-and-release.yml)
[![NuGet Package](https://img.shields.io/nuget/v/Krackend.Sagas.Orchestrations.Runtime.svg?label=package)](https://www.nuget.org/packages/Krackend.Sagas.Orchestrations.Runtime)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Krackend.Sagas.Orchestrations.Runtime.svg?label=downloads)](https://www.nuget.org/packages/Krackend.Sagas.Orchestrations.Runtime)
[![Branch Coverage](https://img.shields.io/badge/branch%20coverage-99.04%25-brightgreen.svg)](#sagas-orchestrations)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

![Krackend orchestration banner](assets/krackend-readme-hero.png)

Modular backend building blocks for .NET services and orchestration runtimes.

## Packages

Install only the packages required by the host role you are building.

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
dotnet add package Krackend.Sagas.Orchestrations.Security
dotnet add package Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework
```

## Event Sourcing

`Krackend.EventSourcing` provides a modular write-model runtime for event sourcing:

- event and state schema versioning
- expected-version appends
- paged stream reads
- state rehydration with reducers
- snapshots of state
- EF Core event store adapter
- configurable event envelope ids through `IEventIdFactory`, with ULID ids by default
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

Event ids:

Krackend uses ULID ids by default. The core envelope exposes `EventId` as a string, and the EF Core adapter stores it as text by default. Hosts can replace the generator by registering `IEventIdFactory` before calling `AddKrackendEventSourcing`, or customize the EF Core property mapping when they want provider-specific storage such as SQL Server `binary(16)`.

```csharp
services.AddScoped<IEventIdFactory, HostEventIdFactory>();
services.AddKrackendEventSourcing();
```

```csharp
services.AddKrackendEntityFrameworkEventStore<AppDbContext>(options =>
{
    options.ConfigureEventIdProperty((property, store) =>
    {
        property.HasUlidBytesConversion("binary(16)");
    });
});
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
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Mongo.Sample`: Mongo-backed runtime host that uses the MongoDB EF Core provider while keeping Mule buffering, Pigeon messaging, Redis gossip, ButterMorph, Runtime WebUI, and runtime APIs wired through the same orchestration packages.
- `samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Mongo.Sample`: Mongo-backed control-plane host that uses the MongoDB EF Core provider with design, distribution, security, WebUI, artifact delivery endpoints, APIs, and seed data.

Runtime samples register the Azure Service Bus Pigeon adapter only when `ConnectionStrings:AzureServiceBus` or `Pigeon:MessageBrokers:AzureServiceBus:ConnectionString` is configured. With the default empty value, the hosts still start for local storage, distribution, and WebUI smoke testing without requiring a broker.

## Sagas Orchestrations

Krackend Sagas Orchestrations is split into composable libraries so the runtime, control plane, transport adapters, client integrations, and UI modules can evolve independently:

- `Krackend.Sagas.Orchestrations.ControlPlane*` captures orchestration definitions, versions, releases, runtime nodes, credentials, and artifact delivery.
- `Krackend.Sagas.Orchestrations.Runtime*` consumes immutable artifacts, projects ingress configuration, runs durable Mule-backed work, dispatches transport-agnostic commands, tracks instances, and exposes runtime diagnostics.
- `Krackend.Sagas.Orchestrations.Client*` lets services start or answer orchestration work without changing business payloads. Pigeon is one messaging adapter and Spider is a pipeline extension over the client core.
- The Spider client extension supports inline and deferred consumer execution. `EmitEvent` publishes new orchestration trigger events, while `UseOrchestration` keeps the service connected to an existing saga backchannel. Both can be composed in the same pipeline, allowing a consumer to emit one or more events and still report success or failure to the orchestration that invoked it. For deferred SquirrelBox consumers, orchestration metadata is restored from the inbox entry so success and failure callbacks still reach the runtime backchannel without wrapping or mutating the business payload.
- `Krackend.Sagas.Orchestrations.SchemaRegistry*` keeps schema resolution provider-neutral, with KnOwl Control Plane available as the plug-in adapter for deployed ButterMorph contracts.
- `Krackend.Sagas.Orchestrations.WebUI.Shell`, `ControlPlane.WebUI`, and `Runtime.WebUI` provide Razor UI modules for host applications.
- `Krackend.Sagas.Orchestrations.ControlPlane.Api` and `Runtime.Api` expose optional REST endpoints over the same application/runtime services used by the WebUI modules.
- `Krackend.Sagas.Orchestrations.Security*` keeps authentication in the host and adds provider-agnostic orchestration authorization with subjects, roles, permissions, scopes, bootstrap admins, ASP.NET Core policies, and EF storage.

Spider event publishing:

Use `EmitEvent` when a service should publish an orchestration trigger event without reporting that publication as a saga reply. Keep `UseOrchestration` on the same pipeline when the service is also handling a command from an existing saga and must answer the runtime backchannel.

```csharp
app.MapPost("/sales", async (CreateSaleRequest request, ISaleService sales) =>
{
    await sales.CreateAsync(request);
})
.EmitEvent(request => new SaleCreated(request.SaleId), "events.sales.sale.created")
.UseOrchestration();
```

`EmitEvent` uses the same routing pattern as `UseOrchestration`, including conditional routes and optional payload transforms. Multiple `EmitEvent` registrations can run before the final `UseOrchestration` hook when a service needs to raise follow-up business events and still complete the command that the saga sent.

Design validation:

Orchestrator component keys are internal identifiers and use alphanumeric segments separated by dots or underscores, starting with a letter. They preserve the casing captured by the host/UI and are not normalized to lowercase by Krackend.

Messaging topics are external broker addresses, not orchestrator keys. Event trigger topics, task command topics, and compensation topics are stored as captured after trimming and can follow the naming rules of the selected transport or provider, including dashes, underscores, dots, and uppercase characters.

Enable flags are part of the deployed orchestration shape. Disabled triggers do not start the orchestration, disabled stages are skipped by the runtime, and disabled tasks are not dispatched. This is useful for staged rollouts or temporarily removing a branch from execution without deleting the design history.

Orchestration metadata:

Krackend propagates transversal metadata through transport metadata so business payloads stay focused on business data. Runtime command dispatches include `Krackend.Sagas.Orchestrations.Message.Metadata` for the current runtime backchannel and flat propagated entries such as `Krackend.Sagas.Orchestrations.Trigger.Metadata`, `audit.context`, and `security.context`. The runtime no longer duplicates propagated metadata inside the reserved `Krackend.Sagas.Orchestrations.Propagation.Metadata` envelope when publishing commands; that envelope is only understood as a legacy inbound shape for compatibility.

`Krackend.Sagas.Orchestrations.Trigger.Metadata` is the canonical trigger metadata entry. It carries values such as correlation id, trace id, event id, event type, idempotency key, aggregate type, and causation id. The runtime uses the trigger correlation id as the saga correlation id when it is available, and still accepts the legacy `trigger_metadata` key when reading older messages.

Client consumers forward incoming propagation metadata when publishing follow-up messages. Non-reserved metadata entries such as `audit.context` and `security.context` are attached back to the outgoing transport metadata, and `Krackend.Sagas.Orchestrations.Trigger.Metadata` is forwarded as the canonical trigger context. Reserved Krackend metadata keys are not blindly forwarded.

When a client publishes a new trigger through `EmitEvent`, the outgoing trigger context is produced by `IOrchestrationTriggerMetadataAccessor`. That event receives a fresh `Krackend.Sagas.Orchestrations.Trigger.Metadata` entry for the new trigger publication. If the service was already executing inside a saga, the previous orchestration trace context is preserved separately under `Krackend.Sagas.Orchestrations.Origin.Metadata` so the new event can be traced back to the saga that emitted it without making the old saga correlation id the principal trigger correlation id of the new event.

Runtime trigger promotion is idempotent when a stable start key is available. The Mule adapter maps the durable action deduplication key, or the durable action id when no deduplication key exists, into the runtime start idempotency key. The core runtime also falls back to the trigger metadata idempotency key. Retried trigger actions therefore continue the already-promoted `OrchestrationInstance` instead of creating a second saga instance after a partial start, timeout, or action re-execution. Hosts using Entity Framework runtime storage should apply a migration for the nullable `StartIdempotencyKey` column and its unique non-null index.

Runtime task dispatch is adapter-based. Initial dispatch, retry dispatch, compensation dispatch, and runtime artifact compatibility validation resolve an installed task adapter by `TaskKind` instead of hardcoding the messaging transport in the engine. The built-in runtime registers the Messaging adapter by default; other task kinds must provide and register an `ITaskRuntimeAdapter` before artifacts using that kind can be deployed or dispatched.

Retry policies are explicit error-code allowlists. When `MaxRetries` is greater than zero, `RetryableErrorCodes` must contain at least one non-empty code; an empty list means the runtime will not retry any failure. Runtime callbacks can also set `IsRetryableCandidate = false` to suppress retry even when the reported error code appears in the allowlist.

Execution policies and extension bundles:

Orchestration artifacts use an extension-ready schema. Existing linear artifacts are migrated at runtime to schema v2, which adds built-in capability metadata, required external capabilities, required bundles, and execution policy slots without changing the behavior of existing messaging orchestrations.

The runtime resolves execution policy hierarchically: environment defaults, runtime-node overrides, orchestration overrides, stage overrides, and finally task overrides. The selected policy is validated against runtime node capabilities and stored on task attempt/dispatch metadata as `ResolvedExecutionPolicy`. The default provider is `built-in-local`, so existing tasks continue to execute through the installed in-process task adapters.

External bundles are explicit runtime state. Artifacts that require an external bundle or capability are accepted only when the runtime node has an activated `RuntimeExtensionPackage` with the matching bundle id, extension key, semantic version, checksum when provided, and manifest capability. The in-memory repository is suitable for tests or simple hosts; `Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework` persists activation state in `Runtime.RuntimeExtensionPackages` for durable multi-replica hosts.

That activation state is part of the shared Entity Framework runtime model, so relational hosts and the MongoDB EF provider used by the Mongo runtime sample validate bundles through the same repository contract.

Hosts can configure runtime execution defaults through `Runtime:Execution` or `RuntimeExecutionOptions`:

```csharp
services.Configure<RuntimeExecutionOptions>(options =>
{
    options.EnvironmentPolicy = new ExecutionPolicyArtifact
    {
        DefaultProviderKey = ExecutionConstants.BuiltInLocalProvider,
        AllowedProviderKeys = [ExecutionConstants.BuiltInLocalProvider],
        RequireSandboxForExternalExtensions = true
    };
    options.RuntimeNodeCapabilities = RuntimeNodeCapabilitiesArtifact.LocalDefaults;
});
```

Custom execution providers can be registered by hosts through the runtime builder:

```csharp
services
    .AddKrackendOrchestrationsRuntime()
    .AddExecutionSandboxProvider<KubernetesExecutionSandboxProvider>();
```

Recoverable runtime failures:

When a task exhausts its configured retries, times out, or leaves the orchestration unable to advance, the runtime moves the instance into `DeadLettered` when the failure can still be reviewed by an operator. `DeadLettered` is not a broker queue; it is an explicit durable state that says the saga stopped, preserved its context, and can be resumed after the underlying issue is corrected. `Failed` and `Aborted` are reserved for outcomes that should not continue automatically.

The Runtime API exposes recovery operations under the configured runtime API prefix, `/api/v1/runtime` by default:

```http
POST /api/v1/runtime/instances/{instanceId}/replay
Content-Type: application/json

{ "payload": "{...optional replacement payload...}" }
```

```http
POST /api/v1/runtime/instances/{instanceId}/abort
Content-Type: application/json

{ "reason": "External order was cancelled by support." }
```

Replay finds the latest failed or timed-out task first and dispatches it again through the configured task adapter. If no failed task exists but a stage failed, replay restarts that stage. If neither exists, the runtime re-enters the forward engine with the preserved instance snapshot or the optional replacement payload. Abort marks the instance as `Aborted` and records the operator reason in metadata and transitions.

Late callbacks are accepted conservatively. If an earlier timeout attempt eventually reports success after the instance already moved to `DeadLettered`, the task execution can be completed so idempotent downstream systems can reconcile correctly, while the instance remains in the operator-visible stopped state until replay or abort.

Runtime operations and disaster recovery:

The runtime evaluates registered dependency probes and exposes an admission controller before accepting ingress, callbacks, dispatch, timeout processing, startup projection, recovery, and reconciliation work. Primary persistence is a critical dependency: if the main database is unavailable, the runtime moves to `Closed` and stops accepting new work so brokers can retain messages or upstream systems can retry through their own outbox/retry strategy. Optional dependencies such as Redis gossip degrade the runtime instead of closing it; the durable database remains the source of truth.

Transport and infrastructure adapters receive operational state changes through `IRuntimeDegradationHandler`. The engine stays transport-agnostic and the adapter decides how to pause, resume, nack, defer, or log according to the selected protocol. The built-in Pigeon adapter listens for closed/reopened states without hardcoding broker behavior into the runtime engine.

Runtime reconciliation runs in the background and can also be invoked through `IOrchestrationRuntimeReconciler`. It processes due timeouts, reloads recoverable instances from durable storage, and re-enters the saga engine with preserved message metadata and snapshot payloads. This is the recovery path after orchestrator restarts, transient process crashes, or missed in-memory timers.

```json
{
  "Runtime": {
    "Operations": {
      "Enabled": true,
      "ScanIntervalSeconds": 5
    },
    "Reconciliation": {
      "Enabled": true,
      "ScanIntervalSeconds": 15,
      "BatchSize": 100
    }
  }
}
```

Compensation:

Tasks and event triggers can define compensating tasks. The runtime evaluates the compensation execution condition, applies the compensation transformation, and dispatches the transformed compensation request through the task adapter abstraction. This keeps compensation transport-agnostic and lets messaging, or another installed task kind, own its own dispatch behavior.

ButterMorph compensation contexts include the data that is technically available at that point in the saga. Task compensation can use the trigger payload and metadata, forward task requests and replies up to the task being compensated, the failed task request when available, and previous compensation replies. Trigger compensation intentionally exposes only the selected event trigger payload, trigger metadata, and transversal metadata, because forward tasks may never have executed when trigger compensation runs. This lets a compensation step reverse the paired forward task and still emit additional compensating side effects when the design requires them.

The Control Plane WebUI opens ButterMorph directly for compensation execution conditions and transformations. Trigger compensation is edited in its own tab with the same task configuration surface used by regular tasks, including timeout, retry, and on-error policy settings.

Security model:

Authentication belongs to the host. Krackend libraries do not configure Entra ID, JWT bearer, cookies, API keys, IdentityServer, Auth0, Keycloak, or any concrete provider. The host authenticates a `ClaimsPrincipal`; Krackend resolves the external subject from configured claims and evaluates orchestration permissions.

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

Entity Framework storage provider model:

The `*.Storage.EntityFramework` packages define portable EF Core storage mappings and repository implementations, but they do not choose SQL Server, PostgreSQL, SQLite, or any other database provider. The host owns the provider package, connection string, migrations assembly, and any provider-specific model details.

```csharp
builder.Services.AddOrchestratorRuntimeStorageEntityFramework(
    db => db.UseSqlServer(
        builder.Configuration.GetConnectionString("Runtime"),
        sql => sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name)),
    storage => storage.ConfigureModel = modelBuilder =>
    {
        // Optional host-owned SQL Server details, such as filtered indexes
        // or provider-specific column types used by this host's migrations.
    });
```

The same pattern is available for Control Plane and Security storage. The sample hosts keep their SQL Server-specific model customization beside their migrations so another host can choose a different EF Core provider without changing Krackend packages.

Distribution credential protection:

Control Plane runtime-node credentials and Runtime design-node credentials are durable records. The Entity Framework storage adapters therefore persist the ASP.NET Core Data Protection key ring in the same storage model by default, under `Distribution.DataProtectionKeys` for Control Plane and `Runtime.DataProtectionKeys` for Runtime. This keeps protected distribution credentials decryptable across restarts, rollouts, and multiple replicas.

Hosts can keep the default database-backed key ring, set a stable Data Protection application name, or replace/harden key storage through the storage options:

```csharp
builder.Services.AddOrchestratorRuntimeStorageEntityFramework(
    db => db.UseSqlServer(builder.Configuration.GetConnectionString("Runtime")),
    storage =>
    {
        storage.DataProtectionApplicationName = "krackend-runtime-prod";
        storage.ConfigureDataProtection = dataProtection =>
        {
            // Optional host-owned Data Protection hardening such as a certificate
            // or another persistent key repository.
        };
    });
```

The same options are available on `AddOrchestratorControlPlaneStorageEntityFramework`. If a host disables database key persistence or replaces it with another repository, that repository must be durable and shared by every replica that needs to decrypt existing distribution credentials. Credentials protected with a key that has already been lost cannot be recovered and must be reimported or regenerated.

MongoDB sample hosts are included to validate that provider choice belongs to the host:

```csharp
builder.Services.AddOrchestratorRuntimeStorageEntityFramework(
    db => db.UseMongoDB(
        builder.Configuration.GetConnectionString("Runtime")!,
        builder.Configuration.GetValue("Mongo:DatabaseName", "KrackendRuntime")));
```

The Mongo samples expect MongoDB to run as a replica set because the orchestration runtime uses transactions for durable state changes.

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

Pigeon adapter configuration:

Krackend keeps the regular Pigeon global settings callback and also exposes Pigeon's full `IPigeonServiceBuilder` so hosts can configure serializer options, custom serializers, route interceptors, or other Pigeon features without bypassing the Krackend adapter:

```csharp
using System.Text.Json;

builder.Services
    .AddKrackendOrchestrationsRuntime()
    .AddPigeon(
        builder.Configuration,
        settings =>
        {
            settings.SetDomain("orders-runtime");
        },
        pigeon =>
        {
            pigeon.ConfigureJsonOptions(options =>
            {
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
        });
```

The same full-builder overload is available for `Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon`.

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

Packages are produced only by explicit `dotnet pack` or by the `Build and Release` workflow. The repository `.release` file contains the next release tag, for example `v3.0.0`; when that marker changes on `main`, the workflow validates the changelog section, creates the release branch/tag, packs all source libraries, and publishes the NuGet artifacts.

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
