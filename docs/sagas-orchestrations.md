# Krackend.Sagas.Orchestrations

This document is the precise reference for the current `Krackend.Sagas.Orchestrations.*` package family. It describes what each package does, which public host functions are available, what those functions register or map, what runtime/control-plane features exist today, and how migrations must be handled.

For the complete start-to-finish Orchestrator walkthrough, including all major models, lifecycle steps, distribution flow, runtime execution, playbooks, risks, and diagnostics, see [sagas-orchestrations-end-to-end.md](sagas-orchestrations-end-to-end.md).

## Current Package Family

| Package | Purpose | Depends on host migrations? |
| --- | --- | --- |
| `Krackend.Sagas.Orchestrations.Abstractions` | Shared artifact contracts, primitive value objects/enums, runtime entities, runtime storage repository contracts, and intake-buffer contracts. | No |
| `Krackend.Sagas.Orchestrations` | Runtime module, in-memory intake buffer, engine, trigger promotion, artifact resolving, message dispatching, and default in-memory message publisher. | No |
| `Krackend.Sagas.Orchestrations.Contracts` | Integration event contracts and lifecycle events shared across Design, Distribution, and Security. | No |
| `Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap` | One-call composition for Design, Distribution, Security, WebUI shell, SQL Server storage adapters, and in-process integration events. | No |
| `Krackend.Sagas.Orchestrations.Design` | Control-plane design domain: domains, orchestration definitions, versions, stages, tasks, triggers, variables, branch rules, parallel groups, team projections, and deployment records. | No |
| `Krackend.Sagas.Orchestrations.Design.Interaction` | Design application services, commands, queries, validators, mappers, artifact snapshot building, lifecycle transition policy, and integration event handlers. | No |
| `Krackend.Sagas.Orchestrations.Design.Storage.SqlServer` | EF Core SQL Server adapter for Design repositories. Stores complex design subdocuments as JSON text columns. | No |
| `Krackend.Sagas.Orchestrations.Design.WebUI` | Razor Pages UI for Design. Adds routes and navigation entries for orchestration authoring. | No |
| `Krackend.Sagas.Orchestrations.Distribution` | Control-plane distribution domain: environments, runtime nodes, artifact releases, release targets, attempts, orchestration projections, and node policies. | No |
| `Krackend.Sagas.Orchestrations.Distribution.Interaction` | Distribution services, artifact delivery endpoints, artifact builders, validation policy, release/promotion operations, runtime node operations, and lifecycle event handlers. | No |
| `Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer` | EF Core SQL Server adapter for Distribution repositories. | No |
| `Krackend.Sagas.Orchestrations.Distribution.WebUI` | Razor Pages UI for Distribution. Adds routes and navigation entries for environments, runtime nodes, artifacts, and releases. | No |
| `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer` | EF Core SQL Server adapter for runtime storage repositories. | No |
| `Krackend.Sagas.Orchestrations.Messaging.Abstractions` | Broker-neutral message publisher/consumer contracts and scoped orchestration metadata context. | No |
| `Krackend.Sagas.Orchestrations.Messaging.Pigeon` | Optional Pigeon adapter for the messaging facade. Adds Pigeon publish/consume metadata interceptors. | No |
| `Krackend.Sagas.Orchestrations.Runtime.WebUI` | Razor Pages UI for runtime artifacts. Adds runtime UI routes and navigation. | No |
| `Krackend.Sagas.Orchestrations.Security` | Control-plane security domain: teams and team members. | No |
| `Krackend.Sagas.Orchestrations.Security.Interaction` | Security services, commands, queries, validators, and team membership operations. | No |
| `Krackend.Sagas.Orchestrations.Security.Storage.SqlServer` | EF Core SQL Server adapter for Security repositories. | No |
| `Krackend.Sagas.Orchestrations.Security.WebUI` | Razor Pages UI for Security teams. Adds routes and navigation. | No |
| `Krackend.Sagas.Orchestrations.Web` | Runtime host minimal API endpoints and runtime artifact pull/deployment/engine interaction services. | No |
| `Krackend.Sagas.Orchestrations.WebUI.Shell` | Shared Razor UI shell, static assets, and navigation registry. | No |

Important rule: packages under `src` must not contain EF migrations. Migrations belong in host applications only. The repository currently keeps sample migrations under:

- `samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample/Migrations/RuntimeStorage`
- `samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample/Migrations`

## Runtime Host Setup

Minimal runtime host:

```csharp
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.EntityFrameworkCore;

builder.Services.AddKrackendSagasOrchestrationsRuntime(options =>
{
    options.EnvironmentKey = "local";
});

builder.Services.AddKrackendSagasOrchestrationsInMemoryIntakeBuffer(options =>
{
    options.Capacity = 10_000;
});

builder.Services.AddKrackendSagasOrchestrationsSqlServer(db =>
{
    db.UseSqlServer(
        builder.Configuration.GetConnectionString("Runtime"),
        sql => sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));
});

builder.Services.AddKrackendSagasOrchestrationsEngine();
builder.Services.AddKrackendSagasOrchestrationsWeb();

app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();
```

Runtime host with Razor UI:

```csharp
using Krackend.Sagas.Orchestrations.Runtime.WebUI;

builder.Services.AddRazorPages();
builder.Services.AddOrchestratorRuntimeWebUI(options =>
{
    options.RoutePrefix = "runtime";
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
```

## Control-Plane Host Setup

Use `Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap` when the host should mount Design, Distribution, Security, SQL Server storage, WebUI modules, and in-process integration events in one call:

```csharp
using Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap;
using Krackend.Sagas.Orchestrations.Distribution.Interaction;
using Microsoft.EntityFrameworkCore;

const string adminRootPath = "admin";
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddRazorPages();

builder.Services.AddOrchestratorControlPlane(options =>
{
    options.AdminRootPath = adminRootPath;
    options.ConfigureSqlServer = db =>
    {
        db.UseSqlServer(
            connectionString,
            sql => sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name));
    };
});

app.MapStaticAssets();
app.MapOrchestratorArtifactDeliveryEndpoints();
app.MapRazorPages().WithStaticAssets();
```

`AddOrchestratorControlPlane` validates that `ConfigureSqlServer` is provided. If `AdminRootPath` is blank, it defaults to `admin`. The prefix is trimmed and normalized by removing leading/trailing slashes.

## Public Host Functions

### Runtime And Engine

| Function | Package | What it does | Required caller input | Failure behavior |
| --- | --- | --- | --- | --- |
| `AddKrackendSagasOrchestrationsRuntime(Action<RuntimeModuleOptions>)` | `Krackend.Sagas.Orchestrations` | Registers `RuntimeEnvironmentDescriptor` as a singleton. | `EnvironmentKey` must be non-empty. | Throws `ArgumentNullException` when configure is null. Throws `InvalidOperationException` when `EnvironmentKey` is blank. |
| `AddKrackendSagasOrchestrationsInMemoryIntakeBuffer(Action<InMemoryTriggerIntakeBufferOptions>? = null)` | `Krackend.Sagas.Orchestrations` | Registers `InMemoryTriggerIntakeBufferOptions` and `ITriggerIntakeBuffer` backed by `InMemoryTriggerIntakeBuffer`. | Optional capacity. Default is defined by `InMemoryTriggerIntakeBufferOptions`. | Throws `InvalidOperationException` when capacity is less than or equal to zero. |
| `AddKrackendSagasOrchestrationsEngine()` | `Krackend.Sagas.Orchestrations` | Registers messaging metadata, default `IMessagePublisher`, `IMessagingCommandDispatcher`, `IArtifactResolver`, `ITriggerPromoter`, `RuntimeEngineDependencies`, and `IRuntimeEngine`. | Runtime storage repositories and intake buffer must also be registered for runtime execution to resolve. | Missing dependencies fail at service resolution time. |

### Runtime Storage

| Function | Package | What it registers |
| --- | --- | --- |
| `AddKrackendSagasOrchestrationsSqlServer(Action<DbContextOptionsBuilder>)` | `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer` | `RuntimeStorageDbContext`, `IRuntimeArtifactRepository`, `ITriggerIntakeRepository`, `ITriggerIntakeAttemptRepository`, `IOrchestrationInstanceRepository`, `IStageExecutionRepository`, `ITaskExecutionRepository`, `ITaskExecutionAttemptRepository`, `ITaskDispatchRepository`, `IExecutionTransitionRepository`, `IInstanceVariableRepository`, and `IEnvironmentVariableRepository`. |

This package intentionally does not include migrations or a design-time DbContext factory. Configure `MigrationsAssembly` in the host.

### Messaging

| Function | Package | What it registers |
| --- | --- | --- |
| `AddKrackendSagasOrchestrationsMessaging()` | `Krackend.Sagas.Orchestrations.Messaging.Abstractions` | Scoped `OrchestratorMetadataContext`, `IOrchestratorMetadataAccessor`, and `IOrchestratorMetadataWriter`. |
| `AddKrackendSagasOrchestrationsMessagingPigeon(IConfiguration, Action<GlobalSettingsBuilder>)` | `Krackend.Sagas.Orchestrations.Messaging.Pigeon` | Base messaging metadata context, Pigeon, Pigeon publish/consume metadata interceptors, `IMessagePublisher` replaced by `PigeonMessagePublisher`, and `IMessageConsumerRegistry` backed by `PigeonMessageConsumerRegistry`. |

Pigeon registration throws `ArgumentNullException` when configuration or configure callback is null.

### Runtime Web

| Function | Package | What it registers or maps |
| --- | --- | --- |
| `AddKrackendSagasOrchestrationsWeb(Action<RuntimeArtifactPullOptions>? = null)` | `Krackend.Sagas.Orchestrations.Web` | `IRuntimeArtifactDeploymentService`, `IRuntimeArtifactConsumerSynchronizer`, `IRuntimeBackChannelResponseHandler`, `IRuntimeTriggerInteractionService`, and typed `HttpClient` for `IRuntimeArtifactPullService`. |
| `MapKrackendSagasOrchestrationsArtifactEndpoints()` | `Krackend.Sagas.Orchestrations.Web` | Runtime artifact deploy, active lookup, and pull endpoints. |
| `MapKrackendSagasOrchestrationsEngineEndpoints()` | `Krackend.Sagas.Orchestrations.Web` | Trigger enqueue and engine process endpoints. |

### Control Plane Bootstrap

| Function | Package | What it registers |
| --- | --- | --- |
| `AddOrchestratorControlPlane(Action<ControlPlaneModuleOptions>)` | `Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap` | WebUI shell, in-process integration event publisher, Design WebUI, Design interaction, Design SQL Server storage, Distribution WebUI, Distribution interaction, Distribution SQL Server storage, Security WebUI, Security interaction, and Security SQL Server storage. |

`ControlPlaneModuleOptions`:

- `AdminRootPath`: base route for the Design UI. Defaults to `admin` when blank.
- `ConfigureSqlServer`: required `DbContextOptionsBuilder` callback shared by Design, Distribution, and Security storage adapters.

### Design

| Function | Package | What it registers |
| --- | --- | --- |
| `AddOrchestratorDesignInteraction()` | `Krackend.Sagas.Orchestrations.Design.Interaction` | Pelican handlers, FluentValidation validators, validation pipeline behavior, mappers, Design application services, artifact snapshot builder, team projection service, team lifecycle event handlers, and `IOrchestrationVersionTransitionPolicy`. |
| `AddOrchestratorDesignStorageSqlServer(Action<DbContextOptionsBuilder>)` | `Krackend.Sagas.Orchestrations.Design.Storage.SqlServer` | `DesignStorageDbContext`, Sieve options, `ISieveProcessor`, and all Design repositories. |
| `AddOrchestratorDesignWebUI()` | `Krackend.Sagas.Orchestrations.Design.WebUI` | Design Razor Pages route conventions and Design navigation contributor. |
| `AddOrchestratorDesignWebUI(Action<OrchestratorDesignWebUIOptions>)` | `Krackend.Sagas.Orchestrations.Design.WebUI` | Same as above, with custom route prefix. |

### Distribution

| Function | Package | What it registers or maps |
| --- | --- | --- |
| `AddOrchestratorDistributionInteraction()` | `Krackend.Sagas.Orchestrations.Distribution.Interaction` | Runtime environment/node services, artifact/release/release-target services, artifact delivery service, orchestration node policy service, JSON artifact validation policy, artifact builders, and orchestration lifecycle event handlers. |
| `AddOrchestratorDistributionStorageSqlServer(Action<DbContextOptionsBuilder>)` | `Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer` | `DistributionStorageDbContext`, Sieve options, `ISieveProcessor`, and all Distribution repositories. |
| `AddOrchestratorDistributionWebUI()` | `Krackend.Sagas.Orchestrations.Distribution.WebUI` | Distribution Razor Pages route conventions and Distribution navigation contributor. |
| `AddOrchestratorDistributionWebUI(Action<OrchestratorDistributionWebUIOptions>)` | `Krackend.Sagas.Orchestrations.Distribution.WebUI` | Same as above, with custom route prefix. |
| `MapOrchestratorArtifactDeliveryEndpoints()` | `Krackend.Sagas.Orchestrations.Distribution.Interaction` | Push, pull-pending, and ack endpoints used by runtime nodes. |

### Security

| Function | Package | What it registers |
| --- | --- | --- |
| `AddOrchestratorSecurityInteraction()` | `Krackend.Sagas.Orchestrations.Security.Interaction` | Pelican handlers, FluentValidation validators, validation pipeline behavior, and `ITeamInteractionService`. |
| `AddOrchestratorSecurityStorageSqlServer(Action<DbContextOptionsBuilder>)` | `Krackend.Sagas.Orchestrations.Security.Storage.SqlServer` | `SecurityStorageDbContext`, `ITeamRepository`, and `ITeamMemberRepository`. |
| `AddOrchestratorSecurityWebUI()` | `Krackend.Sagas.Orchestrations.Security.WebUI` | Security Razor Pages route conventions and Security navigation contributor. |
| `AddOrchestratorSecurityWebUI(Action<OrchestratorSecurityWebUIOptions>)` | `Krackend.Sagas.Orchestrations.Security.WebUI` | Same as above, with custom route prefix. |

### Runtime WebUI And Shared Shell

| Function | Package | What it registers |
| --- | --- | --- |
| `AddOrchestratorWebUIShell()` | `Krackend.Sagas.Orchestrations.WebUI.Shell` | Singleton `OrchestratorNavigationRegistry`. |
| `AddOrchestratorRuntimeWebUI()` | `Krackend.Sagas.Orchestrations.Runtime.WebUI` | Shared shell, runtime Razor Pages route conventions, and Runtime navigation contributor. |
| `AddOrchestratorRuntimeWebUI(Action<OrchestratorRuntimeWebUIOptions>)` | `Krackend.Sagas.Orchestrations.Runtime.WebUI` | Same as above, with custom route prefix. |

## HTTP Endpoints

### Runtime Endpoints

Register with:

```csharp
app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();
```

| Method | Route | Service | Result behavior |
| --- | --- | --- | --- |
| `POST` | `/runtime/artifacts/deploy` | `IRuntimeArtifactDeploymentService.Deploy` | `200 OK` when accepted, `400 BadRequest` otherwise. |
| `GET` | `/runtime/artifacts/active/{orchestrationDefinitionKey}` | `IRuntimeArtifactDeploymentService.GetActive` | `200 OK` with artifact when found, `404 NotFound` when missing. |
| `POST` | `/runtime/artifacts/pull` | `IRuntimeArtifactPullService.PullPending` | `200 OK` when succeeded, `400 BadRequest` otherwise. |
| `POST` | `/runtime/triggers` | `IRuntimeTriggerInteractionService.Enqueue` | `200 OK` when accepted, `400 BadRequest` otherwise. |
| `POST` | `/runtime/engine/process-next` | `IRuntimeTriggerInteractionService.ProcessNext` | `200 OK` when succeeded, `400 BadRequest` otherwise. |
| `POST` | `/runtime/engine/process-all?maxItems=25` | `IRuntimeTriggerInteractionService.ProcessAll` | Always returns `200 OK` with result. Default `maxItems` is 25. |

### Distribution Artifact Delivery Endpoints

Register with:

```csharp
app.MapOrchestratorArtifactDeliveryEndpoints();
```

| Method | Route | Service | Result behavior |
| --- | --- | --- | --- |
| `POST` | `/distribution/release-targets/{releaseTargetId}/push` | `IArtifactDeliveryInteractionService.Push` | `200 OK` when succeeded, `400 BadRequest` otherwise. Actor is currently hard-coded to `api`. |
| `GET` | `/distribution/runtime-nodes/{runtimeNodeId}/artifacts/pending` | `IArtifactDeliveryInteractionService.GetPendingForPull` | `200 OK` with pending packages. |
| `POST` | `/distribution/runtime-nodes/{runtimeNodeId}/artifacts/{releaseTargetId}/ack` | `IArtifactDeliveryInteractionService.AcknowledgePull` | `200 OK` with acknowledgement result. Blank runtime artifact id is used when request body is null. |

## Runtime Behavior

### Runtime Module

`RuntimeEnvironmentDescriptor` is the runtime identity for a host process. The runtime environment key is trimmed during registration and should match the Distribution runtime environment/node policy model.

### Intake Buffer

`ITriggerIntakeBuffer` is the contract used by the engine to receive runtime triggers. The current built-in implementation is `InMemoryTriggerIntakeBuffer`.

The in-memory buffer is useful for local hosts and tests. It is process-local and not durable. Production hosts should pair runtime execution with SQL Server runtime storage and a durable intake strategy when required by deployment topology.

### Engine

`AddKrackendSagasOrchestrationsEngine` wires:

- `IMessagePublisher`
- `IMessagingCommandDispatcher`
- `IArtifactResolver`
- `ITriggerPromoter`
- `RuntimeEngineDependencies`
- `IRuntimeEngine`

The engine needs runtime storage repositories and an intake buffer at resolution/runtime. The registration intentionally does not hide those dependencies because hosts can choose adapters independently.

### Runtime Storage

The SQL Server runtime adapter persists:

- runtime artifacts
- trigger intake rows and attempts
- orchestration instances
- stage executions
- task executions
- task execution attempts
- task dispatches
- execution transitions
- instance variables
- environment variables

Migrations are host-owned. A host should pass `MigrationsAssembly(...)` to `UseSqlServer` when it wants EF migrations in its own assembly.

## Messaging

### Metadata Context

`AddKrackendSagasOrchestrationsMessaging` registers a scoped `OrchestratorMetadataContext`. The same scoped instance is exposed as:

- `IOrchestratorMetadataAccessor`
- `IOrchestratorMetadataWriter`

Metadata key:

```csharp
OrchestratorMetadataConstants.MetadataKey // "Orchestrator.Metadata"
```

`OrchestratorMessageMetadata` carries saga/orchestration runtime context, including:

- saga id
- orchestration id/key/version
- orchestration instance id
- task execution id
- dispatch id
- correlation id
- runtime state snapshot
- response topic/version
- environment

### Pigeon Adapter

`AddKrackendSagasOrchestrationsMessagingPigeon` connects the broker-neutral messaging facade to Pigeon. It:

- registers the base metadata context
- calls `AddPigeon`
- adds publish metadata interceptor
- adds consume metadata interceptor
- replaces `IMessagePublisher` with `PigeonMessagePublisher`
- registers `IMessageConsumerRegistry`

Use this package only in hosts that want Pigeon. The runtime core does not require it.

## Design Module

### Domain Model

`Krackend.Sagas.Orchestrations.Design` contains the authoring model for:

- `Domain`
- `OrchestrationDefinition`
- `OrchestrationVersion`
- `StageDefinition`
- `TaskDefinition`
- `TriggerBinding`
- `VariableDefinition`
- `ParallelGroupDefinition`
- `BranchRuleDefinition`
- `TeamProjection`
- `DeploymentRecord`

Task configuration implementations:

- `HttpTaskConfiguration`
- `HumanApprovalTaskConfiguration`
- `MessagingTaskConfiguration`
- `PluginTaskConfiguration`

Trigger channel implementation:

- `EventTriggerChannel`

Condition/transformation implementations:

- `DslConditionConfiguration`
- `DslTransformationConfiguration`

Retry and timeout implementations:

- `FixedRetryStrategy`
- `FailTimeoutBehaviorPolicy`
- `ReconcileTimeoutBehaviorPolicy`
- `WaitTimeoutBehaviorPolicy`

### Design Interaction Services

`AddOrchestratorDesignInteraction` registers these service surfaces:

| Service | Functions |
| --- | --- |
| `IDomainInteractionService` | `Upsert`, `SetIsActive`, `GetById`, `GetAll` |
| `IOrchestrationInteractionService` | `Create`, `Update`, `Activate`, `Deactivate`, `GetById`, `GetAll` |
| `IOrchestrationVersionInteractionService` | `Create`, `Update`, `SetInReview`, `ReturnToDraft`, `ReopenReview`, `Approve`, `Deploy`, `Deprecate`, `Archive`, `GetById`, `GetAll` |
| `IStageInteractionService` | `Create`, `Update`, `SetExecutionCondition`, `Delete`, `GetAll`, `GetById` |
| `ITaskInteractionService` | `Create`, `Update`, `Delete`, `Enable`, `Disable`, `SetExecutionCondition`, `SetTransformation`, `GetAll`, `GetById` |
| `ITriggerBindingInteractionService` | `Create`, `Update`, `Delete`, `Enable`, `Disable`, `GetAll`, `GetById` |
| `IVariableInteractionService` | `Create`, `Update`, `Delete`, `GetAll`, `GetById` |
| `IParallelGroupInteractionService` | `Create`, `Update`, `Delete`, `GetAll`, `GetById` |
| `IBranchRuleInteractionService` | `Create`, `Update`, `Delete`, `GetAll`, `GetById` |
| `ITeamProjectionInteractionService` | `Search` |

### Design Commands And Queries

Current commands:

- Branch rules: `CreateBranchRuleDefinitionCommand`, `UpdateBranchRuleDefinitionCommand`, `DeleteBranchRuleDefinitionCommand`
- Domains: `UpsertDomainCommand`, `SetDomainIsActiveCommand`
- Orchestration definitions: `CreateOrchestrationDefinitionCommand`, `UpdateOrchestrationDefinitionCommand`, `ActivateOrchestrationDefinitionCommand`, `DeactivateOrchestrationDefinitionCommand`
- Orchestration versions: `CreateOrchestrationVersionCommand`, `UpdateOrchestrationVersionCommand`, `SetOrchestrationVersionInReviewCommand`, `ReturnOrchestrationVersionToDraftCommand`, `ReopenOrchestrationVersionReviewCommand`, `ApproveOrchestrationVersionCommand`, `DeployOrchestrationVersionCommand`, `DeprecateOrchestrationVersionCommand`, `ArchiveOrchestrationVersionCommand`
- Parallel groups: `CreateParallelGroupDefinitionCommand`, `UpdateParallelGroupDefinitionCommand`, `DeleteParallelGroupDefinitionCommand`
- Stages: `CreateStageDefinitionCommand`, `UpdateStageDefinitionCommand`, `SetStageExecutionConditionCommand`, `DeleteStageDefinitionCommand`
- Tasks: `CreateTaskDefinitionCommand`, `UpdateTaskDefinitionCommand`, `DeleteTaskDefinitionCommand`, `EnableTaskDefinitionCommand`, `DisableTaskDefinitionCommand`, `SetTaskExecutionConditionCommand`, `SetTaskTransformationCommand`
- Trigger bindings: `CreateTriggerBindingCommand`, `UpdateTriggerBindingCommand`, `DeleteTriggerBindingCommand`, `EnableTriggerBindingCommand`, `DisableTriggerBindingCommand`
- Variables: `CreateVariableDefinitionCommand`, `UpdateVariableDefinitionCommand`, `DeleteVariableDefinitionCommand`

Current queries:

- Branch rules: `GetBranchRuleDefinitionsQuery`, `GetBranchRuleDefinitionByIdQuery`
- Domains: `GetDomainsQuery`, `GetDomainByIdQuery`
- Orchestration definitions: `GetOrchestrationDefinitionsQuery`, `GetOrchestrationDefinitionByIdQuery`
- Orchestration versions: `GetOrchestrationVersionsQuery`, `GetOrchestrationVersionByIdQuery`
- Parallel groups: `GetParallelGroupDefinitionsQuery`, `GetParallelGroupDefinitionByIdQuery`
- Stages: `GetStageDefinitionsQuery`, `GetStageDefinitionByIdQuery`
- Tasks: `GetTaskDefinitionsQuery`, `GetTaskDefinitionByIdQuery`
- Trigger bindings: `GetTriggerBindingsQuery`, `GetTriggerBindingByIdQuery`
- Variables: `GetVariableDefinitionsQuery`, `GetVariableDefinitionByIdQuery`

### Version Lifecycle Policy

`IOrchestrationVersionTransitionPolicy` exposes:

- `CanTransition(from, to)`
- `EnsureCanTransition(from, to)`
- `GetAllowedTargets(from)`

Allowed transitions:

| From | Allowed targets |
| --- | --- |
| `Draft` | `InReview` |
| `InReview` | `Draft`, `Approved` |
| `Approved` | `InReview`, `Deployed` |
| `Deployed` | `Deprecated` |
| `Deprecated` | `Archived` |
| `Archived` | none |

Invalid transitions throw `InvalidOperationException` through `EnsureCanTransition`.

### Design Storage

`AddOrchestratorDesignStorageSqlServer` registers:

- `DesignStorageDbContext`
- `IDomainRepository`
- `ITeamProjectionRepository`
- `IOrchestrationDefinitionRepository`
- `IOrchestrationVersionRepository`
- `IStageRepository`
- `ITaskRepository`
- `ITriggerBindingRepository`
- `IVariableDefinitionRepository`
- `IParallelGroupRepository`
- `IBranchRuleRepository`
- Sieve filtering/sorting/paging infrastructure

The adapter uses JSON model/converter types for polymorphic design subdocuments such as task configuration, condition configuration, transformation configuration, retry strategy, trigger channel, and timeout behavior policy.

## Distribution Module

### Domain Model

`Krackend.Sagas.Orchestrations.Distribution` contains:

- `RuntimeEnvironment`
- `RuntimeNode`
- `RuntimeCapability`
- `Artifact`
- `Release`
- `ReleaseTarget`
- `ReleaseAttempt`
- `ReleasePlanTarget`
- `OrchestrationProjection`
- `OrchestrationAllowedRuntimeNode`

### Distribution Interaction Services

`AddOrchestratorDistributionInteraction` registers:

| Service | Functions |
| --- | --- |
| `IRuntimeEnvironmentInteractionService` | `Upsert`, `SetEnabled`, `GetAll` |
| `IRuntimeNodeInteractionService` | `Upsert`, `SetEnabled`, `GetAll` |
| `IArtifactInteractionService` | artifact release listing operations |
| `IReleaseInteractionService` | release creation/listing operations |
| `IReleaseTargetInteractionService` | release target listing/attempt operations |
| `IArtifactDeliveryInteractionService` | `Push`, `GetPendingForPull`, `AcknowledgePull` |
| `IOrchestrationNodePolicyInteractionService` | `GetOrchestrations`, `Get`, `Replace`, `GetByOrchestrationIds` |
| `IArtifactValidationPolicy` | `CanValidate` and artifact validation behavior |

Registered lifecycle event handlers:

- `OrchestrationVersionDeployedEvent`
- `OrchestrationVersionDeprecatedEvent`
- `OrchestrationVersionArchivedEvent`
- `OrchestrationDefinitionCreatedEvent`
- `OrchestrationDefinitionUpdatedEvent`
- `OrchestrationDefinitionDeactivatedEvent`

Artifact builders:

- deployed version artifact builder
- deprecated version artifact builder
- archived version artifact builder

### Distribution Storage

`AddOrchestratorDistributionStorageSqlServer` registers:

- `DistributionStorageDbContext`
- `IEnvironmentRepository`
- `IRuntimeNodeRepository`
- `IOrchestrationProjectionRepository`
- `IOrchestrationNodePolicyRepository`
- `IArtifactRepository`
- `IReleaseRepository`
- `IReleaseTargetRepository`
- Sieve filtering/sorting/paging infrastructure

## Security Module

### Domain Model

`Krackend.Sagas.Orchestrations.Security` contains:

- `Team`
- `TeamMember`

### Security Interaction Services

`AddOrchestratorSecurityInteraction` registers:

| Service | Functions |
| --- | --- |
| `ITeamInteractionService` | `Upsert`, `SetIsActive`, `AddMember`, `RemoveMember`, `GetAll`, `GetMembers` |

Current commands:

- `UpsertTeamCommand`
- `SetTeamIsActiveCommand`
- `AddTeamMemberCommand`
- `RemoveTeamMemberCommand`

Current queries:

- `GetTeamsQuery`
- `GetTeamMembersQuery`

### Security Storage

`AddOrchestratorSecurityStorageSqlServer` registers:

- `SecurityStorageDbContext`
- `ITeamRepository`
- `ITeamMemberRepository`

## WebUI Modules

All WebUI packages are Razor class libraries. Hosts must call `AddRazorPages`, `MapStaticAssets`, and `MapRazorPages().WithStaticAssets()` when serving the pages.

### Shared Shell

`Krackend.Sagas.Orchestrations.WebUI.Shell` provides:

- shared shell layout
- shared shell CSS/JS assets
- `IOrchestratorNavigationContributor`
- `OrchestratorNavigationItem`
- `OrchestratorNavigationRegistry`

`OrchestratorNavigationRegistry.GetItems()` merges all registered contributors, orders by `Order`, then by `Label` case-insensitively.

### Current Navigation Entries

| Module | Label | Area | Page | Order |
| --- | --- | --- | --- | --- |
| Design | `Orchestrations` | `OrchestratorDesign` | `/Orchestrations/Index` | 10 |
| Design | `Domains` | `OrchestratorDesign` | `/Domains/Index` | 20 |
| Distribution | `Environments` | `OrchestratorDistribution` | `/Environments/Index` | 20 |
| Distribution | `Runtime Nodes` | `OrchestratorDistribution` | `/RuntimeNodes/Index` | 21 |
| Distribution | `Artifacts` | `OrchestratorDistribution` | `/ArtifactReleases/Index` | 22 |
| Distribution | `Releases` | `OrchestratorDistribution` | `/Promotions/Index` | 23 |
| Security | `Teams` | `OrchestratorSecurity` | `/Teams/Index` | 30 |
| Runtime | `Artifacts` | `OrchestratorRuntime` | `/Artifacts/Index` | 40 |

### Route Prefix Defaults

| Package | Options type | Default route prefix |
| --- | --- | --- |
| `Krackend.Sagas.Orchestrations.Design.WebUI` | `OrchestratorDesignWebUIOptions` | `orchestrator-design` |
| `Krackend.Sagas.Orchestrations.Distribution.WebUI` | `OrchestratorDistributionWebUIOptions` | `orchestrator-distribution` |
| `Krackend.Sagas.Orchestrations.Security.WebUI` | `OrchestratorSecurityWebUIOptions` | `orchestrator-security` |
| `Krackend.Sagas.Orchestrations.Runtime.WebUI` | `OrchestratorRuntimeWebUIOptions` | `runtime` |

`AddOrchestratorControlPlane` overrides these defaults for the control-plane composition:

- Design WebUI: `{AdminRootPath}`
- Distribution WebUI: `{AdminRootPath}/orchestrator-distribution`
- Security WebUI: `{AdminRootPath}/orchestrator-security`

## Integration Events

`Krackend.Sagas.Orchestrations.Contracts` defines:

- `IIntegrationEvent`
- `IIntegrationEventHandler<TEvent>`
- `IIntegrationEventPublisher`

Control-plane lifecycle events include:

- team lifecycle events
- orchestration definition lifecycle events
- orchestration version lifecycle events

`Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap` registers `InProcessIntegrationEventPublisher`, which resolves all `IIntegrationEventHandler<TEvent>` handlers from the current service provider and invokes them sequentially. If no handlers are registered, it logs a warning and returns without throwing.

## Samples

### Runtime Host Sample

Location:

```text
samples/Krackend.Sagas.Orchestrations.RuntimeHost.Sample
```

Demonstrates:

- runtime module registration
- in-memory intake buffer
- SQL Server runtime storage adapter
- runtime engine
- runtime minimal API endpoints
- runtime Razor UI
- runtime EF migrations owned by the host

Configuration keys:

```json
{
  "ConnectionStrings": {
    "Runtime": ""
  },
  "Runtime": {
    "EnvironmentKey": "local",
    "ArtifactPull": {
      "DistributionBaseUri": "",
      "RuntimeNodeId": ""
    },
    "Intake": {
      "InMemory": {
        "Capacity": 10000
      }
    }
  }
}
```

### Control-Plane Host Sample

Location:

```text
samples/Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample
```

Demonstrates:

- `AddOrchestratorControlPlane`
- Design/Distribution/Security modules
- control-plane WebUI
- artifact delivery endpoints
- health checks
- SQL Server EF migrations owned by the host

Configuration keys:

```json
{
  "ConnectionStrings": {
    "Default": ""
  }
}
```

## Current Test Coverage

`tests/Krackend.Sagas.Orchestrations.Tests` covers:

- package marker resolution
- runtime DI registration
- messaging metadata sharing
- Pigeon package registration surface
- SQL Server runtime repository registration
- runtime web endpoint registration surface
- package boundary guard: no migrations under `src`
- package boundary guard: no old `Orchestrator.*` namespaces in loaded Sagas assemblies
- test project references every `Krackend.Sagas.Orchestrations.*` package
- control-plane bootstrap validation and registrations
- in-process event publishing
- Design version transition policy
- Design/Distribution/Security interaction registrations
- Design/Distribution/Security storage registrations
- WebUI navigation composition and standalone runtime UI shell registration

Run:

```bash
dotnet test tests/Krackend.Sagas.Orchestrations.Tests/Krackend.Sagas.Orchestrations.Tests.csproj
```

Full repository validation:

```bash
dotnet build Krackend.sln --no-restore
dotnet test Krackend.sln --no-build
```

## Operational Rules

1. NuGet packages must stay migration-free. EF migrations belong in the consuming host.
2. Runtime core must not depend on SQL Server, Razor, Pigeon, or a specific host.
3. Pigeon is optional. The runtime engine has a default in-memory message publisher registration when no concrete publisher is provided.
4. Hosts decide which storage adapters to mount.
5. WebUI packages are Razor class libraries; the host remains responsible for Razor Pages and static assets middleware.
6. Control-plane bootstrap is convenience composition. Advanced hosts may register Design, Distribution, Security, storage, and WebUI modules manually.
7. `AddOrchestrator*` names remain on migrated control-plane APIs today. Runtime package APIs use `AddKrackendSagasOrchestrations*` and `MapKrackendSagasOrchestrations*`.
