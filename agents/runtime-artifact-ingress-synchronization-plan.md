# Runtime Artifact Ingress Synchronization Plan

## Objective

Build the first clean runtime slice:

Design promotes orchestration artifacts into the runtime. The runtime stores them in its own database. When the runtime host starts, and whenever new artifacts are promoted while it is already running, it reads active runtime artifacts and registers the required ingress handlers.

This phase ends when the runtime can:

- Read active orchestration artifacts from runtime storage without loading the whole table.
- Derive messaging event-trigger consumers from artifact trigger bindings.
- Derive the orchestration back channel as `orchestrations.{orchestrationKey}`.
- Register handlers dynamically while the host is running.
- Keep Pigeon behind the messaging adapter boundary.
- Skip non-messaging ingress types without failing the host.

This phase does not rebuild the sample host. The runtime sample host content is disposable and must not be used as the source of truth.

## Current Structure Read

Artifact contracts live in:

- `src/Krackend.Sagas.Orchestrations.Abstractions/ArtifactModels/OrchestrationArtifact.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/ArtifactModels/TriggerBindingArtifact.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/ArtifactModels/EventTriggerChannelArtifact.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/ArtifactModels/StageArtifact.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/ArtifactModels/TaskArtifact.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/ArtifactModels/MessagingTaskConfigurationArtifact.cs`

Runtime artifact storage contract currently lives in:

- `src/Krackend.Sagas.Orchestrations.Abstractions/Runtime/Core/RuntimeOrchestrationArtifact.cs`
- `src/Krackend.Sagas.Orchestrations.Abstractions/Runtime/Storage/IRuntimeArtifactRepository.cs`

Runtime SQL storage currently lives in:

- `src/Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer/Repositories/RuntimeArtifactRepository.cs`
- `src/Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer/Infrastructure/RuntimeStorageDbContext.cs`
- `src/Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer/Configurations/RuntimeArtifactEntityConfiguration.cs`

Runtime consumer synchronization currently lives in:

- `src/Krackend.Sagas.Orchestrations.Web/Features/ArtifactDeployments/Services/RuntimeArtifactConsumerSynchronizer.cs`
- `src/Krackend.Sagas.Orchestrations.Web/Features/ArtifactDeployments/Services/RuntimeArtifactConsumerBindings.cs`
- `src/Krackend.Sagas.Orchestrations.Web/Features/ArtifactDeployments/Services/RuntimeActiveArtifactConsumerHostedService.cs`
- `src/Krackend.Sagas.Orchestrations.Web/Features/ArtifactDeployments/Services/RuntimeArtifactDeploymentService.cs`

Messaging abstraction and Pigeon adapter currently live in:

- `src/Krackend.Sagas.Orchestrations.Messaging.Abstractions/Consuming/IMessageConsumerRegistry.cs`
- `src/Krackend.Sagas.Orchestrations.Messaging.Abstractions/Consuming/MessageConsumerRegistration.cs`
- `src/Krackend.Sagas.Orchestrations.Messaging.Abstractions/Consuming/MessageConsumeContext.cs`
- `src/Krackend.Sagas.Orchestrations.Messaging.Pigeon/PigeonMessageConsumerRegistry.cs`

The current Pigeon boundary is acceptable as a first adapter boundary: runtime code depends on `IMessageConsumerRegistry`, not on Pigeon directly.

## Corrections Required

### Runtime Is The Environment

The current runtime model still contains `EnvironmentKey` in runtime artifact deployment, artifact repository lookup, ingress envelopes, and several runtime entities.

For this phase:

- Do not use `EnvironmentKey` to resolve active artifacts for consumer registration.
- Do not require artifact payloads to be tagged by environment.
- Treat the runtime database as the environment boundary.

Later cleanup can remove `EnvironmentKey` from deeper runtime execution records, but the first slice must stop building artifact consumer registration around it.

### No Full Table Reads

Current startup synchronization calls `GetAll(environment)` and filters active artifacts in memory.

Replace this with paged active artifact reads:

- Query only `IsActive = true`.
- Query only deployable orchestration artifacts, initially `ArtifactType = "orchestration.deploy"`.
- Page by stable ordering, preferably `(DeployedOnUtc, Id)` or just `Id` if storage supports it cleanly.
- Use a bounded page size from options.
- Never materialize all runtime artifacts at once.

### Hot Artifact Promotion

Runtime deployment already calls the synchronizer after persisting an artifact. Keep that behavior, but make it explicit:

- Deploy request persists artifact.
- If artifact activates runtime configuration, deactivate older active artifact for the same orchestration key/version policy.
- After commit succeeds, call ingress synchronization for that artifact.
- The synchronizer registers/removes consumers without requiring host restart.

Startup synchronization and deployment-time synchronization must share the same registration path.

## Proposed Runtime Structure

Use clearer names than the initial illustrative interfaces.

### Runtime Artifact Access

`IRuntimeArtifactCatalog`

Purpose:

- Read active runtime artifacts in pages.
- Read by id/version/key when needed by the engine.
- Hide storage mechanics from ingress synchronization.

Important methods:

```csharp
Task<RuntimeArtifactPage> ReadActiveDeployments(
    RuntimeArtifactPageCursor cursor,
    int pageSize,
    CancellationToken cancellationToken = default);
```

Implementation:

- `EfCoreRuntimeArtifactCatalog`
- Uses `RuntimeStorageDbContext` directly for paged reads.
- Does not use generic UoW/repository on the hot path.

The existing `IRuntimeArtifactRepository` can remain temporarily for non-hot-path lookups until the engine cleanup reaches it, but startup ingress registration must not use `GetAll`.

### Ingress Synchronization

`IRuntimeIngressSynchronizer`

Purpose:

- Synchronize active artifact ingresses at startup using paged reads.
- Synchronize one promoted artifact while the host is already running.
- Avoid duplicate registration.
- Apply lifecycle transitions.

Implementation:

- `RuntimeIngressSynchronizer`

Responsibilities:

- Parse artifact payload into a runtime artifact document.
- Build desired ingress bindings.
- Dispatch each binding to the correct registrar.
- Skip unsupported ingress kinds.
- Keep an in-memory registration index keyed by:

```text
artifactId + orchestrationKey + orchestrationVersion + ingressKind + topic + messageVersion
```

### Ingress Binding Model

Create broker-neutral binding models:

- `RuntimeIngressBinding`
- `RuntimeMessagingIngressBinding`
- `RuntimeIngressBindingSet`

Binding set per artifact:

- Event trigger bindings from enabled artifact trigger bindings.
- Back channel binding built from `orchestrations.{orchestrationKey}`.

Event trigger messaging binding:

- Topic: from `EventTriggerChannelArtifact.Topic`.
- Version: from `EventTriggerChannelArtifact.Version`.
- Kind: trigger.
- Artifact id/version/key attached.

Back-channel messaging binding:

- Topic: `orchestrations.{orchestrationKey}`.
- Version: artifact orchestration version.
- Kind: task response.
- Artifact id/version/key attached.

### Ingress Registration

`IRuntimeIngressRegistrar`

Purpose:

- Register one logical ingress binding.
- Remove one logical ingress binding.
- Report skipped unsupported bindings.

Implementations:

- `MessagingRuntimeIngressRegistrar`
- Later: `HttpRuntimeIngressRegistrar`

`MessagingRuntimeIngressRegistrar` depends on:

- `IMessageConsumerRegistry`
- `IRuntimeDurableWorkScheduler`
- `IRuntimeBackChannelResponseHandler`

It must not depend on Pigeon.

### Messaging Adapter

Keep:

- `IMessageConsumerRegistry`
- `MessageConsumerRegistration`
- `MessageConsumeContext`
- `PigeonMessageConsumerRegistry`

Pigeon remains only inside:

- `Krackend.Sagas.Orchestrations.Messaging.Pigeon`

The runtime host registers Pigeon through the adapter package, but runtime orchestration code does not call Pigeon types.

## Artifact Parsing Rules

Use the real artifact contract where possible:

- `OrchestrationArtifact.Key`
- `OrchestrationArtifact.Version`
- `OrchestrationArtifact.TriggerBindings`
- `TriggerBindingArtifact.IsEnabled`
- `TriggerBindingArtifact.TriggerChannel`
- `EventTriggerChannelArtifact.Topic`
- `EventTriggerChannelArtifact.Version`

The parser may tolerate old JSON shapes only as defensive compatibility, but the main path must be the artifact model, not ad hoc JSON conventions.

Unsupported trigger channels:

- Skip and log/record result.
- Do not fail host startup.
- Do not register an ingress.

Disabled triggers:

- Skip.

Missing topic for messaging trigger:

- Skip with validation result.

Missing or invalid version:

- Use artifact version only for back channel.
- Event trigger must use the configured event trigger version when present.
- Invalid event trigger version should skip that trigger and report it.

## Startup Flow

1. Host starts.
2. `RuntimeActiveIngressHostedService` creates a scope.
3. It calls `IRuntimeIngressSynchronizer.SynchronizeActiveArtifacts`.
4. Synchronizer reads active deploy artifacts page by page.
5. For each artifact:
   - Build trigger bindings.
   - Build back-channel binding.
   - Register messaging consumers through `MessagingRuntimeIngressRegistrar`.
6. Continue until no next page.

No `GetAll`.
No environment filter.
No sample-host manual queue configuration.

## Hot Promotion Flow

1. Deployment endpoint receives promoted artifact.
2. Deployment service validates and stores artifact in runtime DB.
3. If deploy artifact is active, previous active artifact for same orchestration key is deactivated.
4. Deployment service calls `IRuntimeIngressSynchronizer.SynchronizeArtifact(artifact)`.
5. Synchronizer:
   - Registers new event trigger consumers.
   - Registers or replaces the back-channel consumer.
   - Removes trigger consumers for deprecated/archived artifacts when lifecycle requires it.
6. Runtime accepts messages immediately after registration.

## Handler Behavior For This Phase

Trigger handler:

- Receives `MessageConsumeContext`.
- Builds `RuntimeIngressEnvelope`.
- Sets:
  - `Kind = Trigger`
  - `OrchestrationName = artifact.OrchestrationDefinitionKey` or artifact key
  - `OrchestrationVersion = artifact.Version`
  - `Payload = context.Message`
  - `Source.Kind = Message`
  - `Source.Address = context.Topic`
  - `Source.Version = context.Version`
- Schedules `IRuntimeDurableWorkScheduler.ScheduleProcessIngress`.

Back-channel handler:

- Receives `MessageConsumeContext`.
- Uses orchestrator metadata to resume task response.
- Builds `RuntimeIngressEnvelope` with `Kind = TaskResponse`.
- Schedules `IRuntimeDurableWorkScheduler.ScheduleProcessIngress`.

## Sample Host Rule

The runtime sample host must not define queues manually.

For this plan:

- Ignore existing runtime sample host implementation.
- Later delete/recreate sample host as a thin composition root only.
- Sample host should only configure:
  - runtime storage
  - messaging adapter
  - runtime ingress synchronization
  - diagnostics/logging

No orchestration-specific queues belong in sample host code.

## Implementation Phases

### Phase 1: Contracts And Storage Paging

- Add paged artifact catalog contract.
- Add page cursor/result models.
- Implement EF Core active deploy artifact paging.
- Keep repository compatibility where existing engine still needs it.
- Add tests proving paging does not call `GetAll` or materialize all artifacts.

### Phase 2: Binding Builder

- Add artifact-to-ingress binding builder.
- Parse `OrchestrationArtifact` payload as the primary model.
- Build messaging event trigger bindings from enabled event trigger channels.
- Build back channel as `orchestrations.{orchestrationKey}` using artifact version.
- Skip unsupported/malformed bindings with structured results.

### Phase 3: Runtime Ingress Synchronizer

- Replace `RuntimeArtifactConsumerSynchronizer` responsibilities with generic ingress synchronization.
- Add startup hosted service using paged active reads.
- Add artifact-level synchronization for hot promotion.
- Add registration index to avoid duplicate registrations.

### Phase 4: Messaging Registrar

- Keep `IMessageConsumerRegistry` as adapter boundary.
- Move messaging-specific registration into `MessagingRuntimeIngressRegistrar`.
- Register trigger and back-channel handlers through the abstraction.
- Ensure Pigeon is only referenced by the Pigeon adapter package.

### Phase 5: Deployment Integration

- Update deployment service to store artifact, activate/deactivate, then synchronize artifact.
- Stop requiring artifact/environment match as the runtime registration axis.
- Keep any external request compatibility only if needed, but do not use environment to select runtime artifacts.

### Phase 6: Sample Host Cleanup

- Delete the current runtime sample host content.
- Recreate a minimal host only after the runtime library path is clean.
- No manual queues in sample host.

## Validation

Unit tests:

- Reads active artifacts in pages.
- Does not call full-table `GetAll` for startup sync.
- Builds trigger binding from event trigger topic/version.
- Builds back channel as `orchestrations.{key}`.
- Uses artifact orchestration version for back channel registration.
- Skips disabled triggers.
- Skips unsupported non-messaging trigger channels.
- Skips malformed messaging triggers without crashing startup.
- Registers new artifact while host is running.
- Does not duplicate registrations for the same artifact/binding key.
- Removes/replaces registrations when artifact lifecycle requires it.
- Does not reference Pigeon from runtime/core/web synchronizer code.

Integration tests:

- Seed runtime DB with multiple active artifacts and verify paged startup registration.
- Promote a new artifact after startup and verify handler is registered without restart.
- Publish one trigger message and verify durable ingress is scheduled.
- Publish one back-channel response and verify durable ingress is scheduled.

## Acceptance Criteria For First Cut

- Active artifacts are loaded paginated.
- Runtime startup registers messaging event trigger consumers from artifact configuration.
- Runtime startup registers one back channel per active orchestration artifact.
- Artifact promotion registers consumers while the host is running.
- Pigeon remains behind `IMessageConsumerRegistry`.
- Sample host has no manual queues.
- Non-messaging ingresses are skipped cleanly.
- No runtime artifact registration path relies on `GetAll(environment)` or environment tagging.
