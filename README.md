# Krackend

[![Build](https://github.com/mape1402/krackend/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/mape1402/krackend/actions/workflows/build-and-release.yml)
[![NuGet Package](https://img.shields.io/nuget/v/Krackend.Sagas.Orchestrations.Runtime.svg?label=package)](https://www.nuget.org/packages/Krackend.Sagas.Orchestrations.Runtime)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Krackend.Sagas.Orchestrations.Runtime.svg?label=downloads)](https://www.nuget.org/packages/Krackend.Sagas.Orchestrations.Runtime)
[![Branch Coverage](https://img.shields.io/badge/branch%20coverage-99.04%25-brightgreen.svg)](#sagas-orchestrations)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

![Krackend orchestration banner](assets/krackend-readme-hero.png)

Modular backend building blocks for .NET services and orchestration runtimes.

Krackend runtime libraries, hosts, and samples target .NET 10. Applications consuming these packages require .NET 10 or later. The Roslyn analyzer retains its compatible `netstandard2.0` target.

## Version 6.0 highlights

`v6.0.0` is a major orchestration release. It replaces the former direct version lifecycle with reviewed, applied deployment artifacts; adds portable orchestration-version packages; and hardens the runtime around durable dispatch cancellation, replay, response-timeout ownership, and recovery across replicas.

- **Governed delivery.** Drafts move through a review and explicit apply step before producing a deploy, deprecate, or archive artifact. Releases now target those immutable deployed artifacts, including direct and rollout delivery strategies.
- **Portable design.** Export an eligible orchestration version and import it into a selected orchestration as a new draft; its exported lifecycle state is informational and is never copied into the destination lifecycle.
- **Durable operations.** Mule-backed dispatches can be fenced, replayed, and cancelled durably. A response timeout is a single owned durable action, and instances that need reconciliation enter the `Idle` window before dead-lettering.
- **Observable runtime.** The Runtime UI exposes separate dashboard, instances, statistics, history, and artifact areas with scoped live updates. SignalR carries compact deltas so active views do not need to reload their data for each state change.
- **Safer Pigeon outcomes.** Terminal Pigeon consumer rejections become non-retryable orchestration failures, while client diagnostics can preserve full exception details for the matching task attempt.

Review the [orchestration reference](docs/sagas-orchestrations.md) and the [end-to-end walkthrough](docs/sagas-orchestrations-end-to-end.md) before upgrading a host. The release requires host-owned storage migrations for the new Control Plane lifecycle and Runtime durability fields.

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

Authentication integration package:

```bash
dotnet add package Krackend.Sagas.Orchestrations.WolfAuth
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

Storage ids and EF mapping:

Krackend uses string ULIDs for the `EventId`, `SnapshotId`, and `CandidateId` primary keys. The EF Core adapter stores them as portable 26-character text by default. The host can choose a compact binary representation and configure every persisted field without modifying its `DbContext`.

```csharp
services.AddScoped<IEventIdFactory, HostEventIdFactory>();
services.AddKrackendEventSourcing();
```

```csharp
services.AddKrackendEntityFrameworkEventStore<AppDbContext>(ef =>
{
    ef.Keys(keys => keys.UseUlidBytes("binary(16)"));
    ef.Events(events =>
    {
        events.ForAll((entity, store) =>
            entity.Property(x => x.Payload).HasColumnType("nvarchar(max)"));

        events.For("customers", (entity, store) =>
            entity.Property(x => x.StreamId).HasMaxLength(128));
    });
    ef.Snapshots(entity =>
        entity.Property(x => x.Payload).HasColumnType("nvarchar(max)"));
    ef.SnapshotCandidates(entity =>
        entity.Property(x => x.StreamId).HasMaxLength(128));
});
```

The provider package, column types, conversions, connection, and migrations remain owned by the host. `IRawEventStore` uses the same configured event entities and mappings as `IEventStore`. Binary key storage requires `IEventIdFactory` to keep returning valid ULID strings; custom non-ULID event ids should configure `EventId` separately through `Events`.

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
- `samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample`: control-plane host that mounts design, distribution, team management, WebUI, artifact delivery endpoints, and EF migrations.
- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Mongo.Sample`: Mongo-backed runtime host that uses the MongoDB EF Core provider while keeping Mule buffering, Pigeon messaging, Redis gossip, ButterMorph, Runtime WebUI, and runtime APIs wired through the same orchestration packages.
- `samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Mongo.Sample`: Mongo-backed control-plane host that uses the MongoDB EF Core provider with design, distribution, team management, WebUI, artifact delivery endpoints, APIs, and seed data.

Runtime samples register the Azure Service Bus Pigeon adapter only when `ConnectionStrings:AzureServiceBus` or `Pigeon:MessageBrokers:AzureServiceBus:ConnectionString` is configured. With the default empty value, the hosts still start for local storage, distribution, and WebUI smoke testing without requiring a broker.

## Sagas Orchestrations

Krackend Sagas Orchestrations is split into composable libraries so the runtime, control plane, transport adapters, client integrations, and UI modules can evolve independently:

- `Krackend.Sagas.Orchestrations.ControlPlane*` captures orchestration definitions, versions, releases, runtime nodes, credentials, and artifact delivery.
- `Krackend.Sagas.Orchestrations.Runtime*` consumes immutable artifacts, projects ingress configuration, runs durable Mule-backed work, dispatches transport-agnostic commands, tracks instances, and exposes runtime diagnostics.
- `Krackend.Sagas.Orchestrations.Client*` lets services start or answer orchestration work without changing business payloads. Client publishers pass the original business payload object to the selected transport adapter so the adapter owns serialization. Pigeon is one messaging adapter and Spider is a pipeline extension over the client core.
- The Spider client extension supports inline and deferred consumer execution. `EmitEvent` publishes new orchestration trigger events, while `UseOrchestration` keeps the service connected to an existing saga backchannel. Both can be composed in the same pipeline, allowing a consumer to emit one or more events and still report success or failure to the orchestration that invoked it. For deferred SquirrelBox consumers, orchestration metadata is restored from the inbox entry so success and failure callbacks still reach the runtime backchannel without wrapping or mutating the business payload.
- Failed client operations publish an `OrchestrationExecutionResultMetadata` record. By default it includes the normalized error code, message, CLR exception type, and complete `Exception.ToString()` diagnostic (including inner exceptions and stack trace). The runtime persists that diagnostic only on the matching task attempt and exposes it in the saga attempt detail with a copy action. Set `IncludeExceptionDetails = false` to redact all exception information, or set `IncludeExceptionStackTrace = false` to retain the message and type without the complete diagnostic. Treat complete diagnostics as sensitive operational data: secure the transport and runtime diagnostics access accordingly.
- `Krackend.Sagas.Orchestrations.SchemaRegistry*` keeps schema resolution provider-neutral, with KnOwl Control Plane available as the plug-in adapter for deployed ButterMorph contracts.
- `Krackend.Sagas.Orchestrations.WebUI.Shell`, `ControlPlane.WebUI`, and `Runtime.WebUI` provide Razor UI modules for host applications.
- `Krackend.Sagas.Orchestrations.ControlPlane.Api` and `Runtime.Api` expose optional REST endpoints over the same application/runtime services used by the WebUI modules.
- `Krackend.Sagas.Orchestrations.WolfAuth` adds optional WebUI authentication gates over host-owned WolfAuth/ASP.NET Core authentication. Krackend no longer ships its own subject, role, permission, or security storage packages.
- Control Plane and runtime ButterMorph integration is aligned with ButterMorph `2.1.3` for schema design, mapping, execution-condition authoring, runtime transforms, and runtime validations.

### Web UI and themes

The Control Plane and Runtime UIs use the Zoorce application shell and data grids. Catalogs offer grid and card views, retain the selected view in the browser, and load additional results as the user scrolls. Search, sorting, and paging are performed by the configured storage provider.

Configure the light and dark palettes when registering the UI in each host. The same configuration supplies the Zoorce shell and, in the Control Plane, the embedded ButterMorph designers. Existing top-level color properties are aliases for the light palette.

```csharp
using Krackend.Sagas.Orchestrations.WebUI.Shell;

builder.Services.AddOrchestratorControlPlaneWebUI(options =>
{
    options.Theme.Title = "Krackend";
    options.Theme.Mode = OrchestratorWebUIThemeMode.Light;
    options.Theme.Light.PrimaryColor = "#0b3d91";
    options.Theme.Light.SurfaceColor = "#ffffff";
    options.Theme.Light.ElevatedColor = "#f7faff";
    options.Theme.Light.PrimarySoftColor = "#e4edff";
    options.Theme.Light.SuccessColor = "#087a57";
    options.Theme.Light.SuccessSoftColor = "#ddf4e9";
    options.Theme.Dark.PrimaryColor = "#73a8ff";
    options.Theme.Dark.SurfaceColor = "#0d1b2f";
    options.Theme.Dark.ElevatedColor = "#172a42";
    options.Theme.Dark.PrimarySoftColor = "#253d62";
    options.Theme.Dark.SuccessColor = "#55d99c";
    options.Theme.Dark.SuccessSoftColor = "#173d30";
});

// In the separate runtime host:
builder.Services.AddOrchestratorRuntimeWebUI(options =>
{
    options.Theme.Mode = OrchestratorWebUIThemeMode.Light;
    options.Theme.Light.PrimaryColor = "#0b3d91";
    options.Theme.Dark.PrimaryColor = "#73a8ff";
});
```

When using `AddOrchestratorControlPlane` instead of the standalone UI registration, set `options.Theme` on that composition. Configure both palettes with sufficient contrast; the host-provided initial mode is independent of the user's light/dark selection.

Each palette can also configure `PrimaryForegroundColor`, `GlowColor`, warning, danger and information colors and their soft variants, five data visualization colors, code control colors, shadow, and overlay. Optional elevated, soft-accent, glow and primary-foreground colors are derived from the configured surface and primary color when omitted. Legacy top-level properties remain aliases for `Light`. ButterMorph receives the supported subset of both palettes; its UI is not restyled by Krackend.

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

With Mule 2.2, the runtime records cancellation intent before requesting cancellation of a pending durable dispatch. Reconciliation retries deferred cancellation after restarts or provider failures; it does not interrupt an action already claimed by a worker. Late callbacks and configured task retries retain their existing behavior. Custom runtime storage implementations should implement `IOrchestrationDispatchCancellationIntentRepository` and `ITaskDispatchCancellationRepository` to support recovery of cancellation intents. Hosts own their database migrations and must update the runtime model for `OrchestrationInstance.DispatchCancellationPending` and the `TaskDispatch.PhysicalCancellation*` fields, and the Mule model for `DurableAction.CancelledOnUtc` and `MuleReplay.CancelledOnUtc`. The SQL Server runtime sample includes migrations for these changes; other providers must generate their own.

Awaited task responses now own a single durable timeout action. Dispatch records the response deadline and its Mule reference after committing the `WaitingResponse` state; a callback clears that reference and cancels the timer. If a callback and timer race, both run under the orchestration lease and the stale side becomes a no-op. The runtime still accepts late callbacks according to its existing reconciliation policy. Entity Framework hosts must migrate `TaskExecution.ResponseDeadlineUtc`, `TimeoutActionId`, and `TimeoutActionLane` and add the corresponding waiting-response indexes; the SQL Server runtime sample includes that migration.

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

Runtime reconciliation runs in the background and can also be invoked through `IOrchestrationRuntimeReconciler`. It repairs only durable inconsistencies: unresolved physical dispatch cancellations, persisted-but-unconfirmed dispatches, compensation delivery intents, and awaited tasks missing their durable timeout reference. It does not enumerate every healthy `Running` or `Waiting` saga or re-enter the saga engine globally. This preserves restart and failover recovery without turning normal runtime load into repeated work.

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

Krackend `v5.2.2` uses ButterMorph `2.1.3` across the Control Plane designer, schema designer, runtime transform adapter, and validation adapter. Hosts only need to register the Krackend packages; the ButterMorph package family is versioned centrally by Krackend so design-time snapshots and runtime execution stay on the same mapping engine.

Authentication model:

Authentication belongs to the host. Krackend libraries do not configure Entra ID, JWT bearer, cookies, API keys, IdentityServer, Auth0, Keycloak, or any concrete provider. The host configures WolfAuth and ASP.NET Core authentication, then optionally enables Krackend's WolfAuth integration so the WebUI redirects anonymous browser users to a login shell and requires an authenticated `ClaimsPrincipal`.

```csharp
using Krackend.Sagas.Orchestrations.WolfAuth;
using WolfAuth.AspNetCore;

builder.Services
    .AddAuthentication("HostScheme")
    .AddJwtBearer("HostScheme", options =>
    {
        // Host-owned authentication configuration.
    });

builder.Services.UseWolfAuth(builder.Configuration.GetSection("Krackend:WolfAuth"), options =>
{
    options.ApplicationName = "Krackend";
    options.ChallengeSchemes.Add("HostScheme");
});

app.UseAuthentication();
app.UseWolfAuth();
app.UseKrackendWolfAuthLoginGate();
app.UseAuthorization();
app.MapKrackendWolfAuthEndpoints();
```

`UseWolfAuth` is only Krackend's product integration point. It does not create users, roles, permissions, or databases. If the package is not registered, the WebUI shell renders without login/logout controls and Krackend leaves authentication completely to the host.

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

The same pattern is available for Control Plane storage. The sample hosts keep their SQL Server-specific model customization beside their migrations so another host can choose a different EF Core provider without changing Krackend packages.

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
    options.Authorization.ReadPolicy = "ControlPlaneReaders";
    options.Authorization.DesignWritePolicy = "DesignWriters";
    options.Authorization.ReleaseExecutePolicy = "ReleaseOperators";
    options.Authorization.SecurityManagePolicy = "TeamManagers";
});

app.MapKrackendOrchestrationsRuntimeApi(options =>
{
    options.Authorization.ReadPolicy = "RuntimeReaders";
    options.Authorization.InstancesReadPolicy = "RuntimeInstanceReaders";
    options.Authorization.ManagePolicy = "RuntimeManagers";
    options.Authorization.ArtifactApplyPolicy = "RuntimeArtifactOperators";
});
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

Hosts can override the KnOwl catalog route templates when their Control Plane exposes a custom API shape, and can register `IKnOwlControlPlaneCatalogRequestConfigurator` when catalog requests need host-owned authentication headers.

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
    }, runtimeMule =>
    {
        runtimeMule.Profile = KrackendMuleRuntimeProfile.Balanced;
    });

builder.Services.AddKrackendOrchestrationsRuntimeButterMorph();

app.MapOrchestratorRuntimeDistributionEndpoints();
app.MapKrackendOrchestrationsRuntimeApi();
app.MapOrchestratorRuntimeReactiveHub();
```

Mule capacity profiles are runtime-owned, so hosts do not need to reproduce Krackend's internal lane topology. `Balanced` is the default; `Development` targets low-resource local work; `HighThroughput` raises the total reader and execution budget for provisioned production hosts; and `Custom` accepts explicit total budgets. Krackend partitions that budget across trigger intake, backchannels, dispatch, response timeouts, statistics, and artifact lifecycle lanes so a busy dispatch lane cannot starve callbacks or timeout processing.

```json
{
  "Runtime": {
    "Mule": {
      "Profile": "HighThroughput"
    }
  }
}
```

For `Custom`, set `TotalWorkerCount` and `TotalMaxDegreeOfParallelism`; only use per-lane overrides when there is a measured reason. Runtime response timers and scheduled dispatches are persisted in Mule and have an exact wake-up notification. This avoids a periodic query over every waiting saga while preserving restart recovery from durable storage.

When multiple independent runtime stores share Redis, give each one a distinct `Runtime:Mule:FastLane:KeyPrefix`. Replicas that share the same runtime database must use the same prefix; separate environments, tenants, or isolated runtime databases must not. The sample defaults to `krackend:runtime` and hosts can override it without reproducing the runtime lane configuration.

Pigeon adapter configuration:

Pigeon acknowledgements are a host transport concern. A runtime or service host that does not
explicitly complete deliveries must select `OnHandlerSuccess`; Pigeon's default is `Manual` and
will otherwise leave successfully handled Azure Service Bus deliveries eligible for redelivery
when their lock expires. The runtime samples use this safe default:

```json
{
  "Pigeon": {
    "ConsumerExecution": {
      "AcknowledgementMode": "OnHandlerSuccess"
    }
  }
}
```

Use `Manual` only when the host's consumer deliberately calls `ConsumeContext.CompleteAsync()`
or `FailAsync(...)` for every terminal path.

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
