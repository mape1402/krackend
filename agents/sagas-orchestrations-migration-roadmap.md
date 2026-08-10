# Krackend.Sagas.Orchestrations Migration Roadmap

## Goal

Migrate the old singular `Krackend.Sagas.Orchestration` package into the new plural `Krackend.Sagas.Orchestrations` family, using the existing `Dmx.Orchestrator` codebase as the source for the expanded orchestration runtime. The old singular package is intentionally removed instead of kept as a compatibility facade.

## Guardrails

- Start from current `origin/main`.
- Keep every step independently buildable when practical.
- Commit after each completed step.
- Avoid behavioral rewrites while moving code.
- Keep runtime core independent from EF Core, Razor/WebUI, and Pigeon-specific types.
- Keep adapters optional.
- Do not migrate UI, storage, messaging, and runtime engine in one huge commit.
- Prefer package boundaries that make NuGet consumption easier than the current 27-project layout.

## Proposed Package Shape

1. `Krackend.Sagas.Orchestrations.Abstractions`
   - Shared IDs, primitives, metadata, contracts, envelopes, runtime interfaces.

2. `Krackend.Sagas.Orchestrations`
   - Main core package.
   - Runtime module, engine services, orchestration execution, intake contracts, in-memory intake.
   - No EF Core, no Razor/WebUI, no direct Pigeon dependency.

3. `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer`
   - SQL Server persistence adapter for runtime state/artifacts.
   - Later can absorb design/distribution/security storage if control plane migration is added.

4. `Krackend.Sagas.Orchestrations.Messaging.Abstractions`
   - Messaging facade, metadata accessor/writer, publisher/consumer contracts.

5. `Krackend.Sagas.Orchestrations.Messaging.Pigeon`
   - Pigeon adapter only.

6. `Krackend.Sagas.Orchestrations.Web`
   - Runtime/backend endpoint mapping extensions, without Razor UI.

7. Later phases, once runtime core is stable:
   - `Krackend.Sagas.Orchestrations.Design`
   - `Krackend.Sagas.Orchestrations.Distribution`
   - `Krackend.Sagas.Orchestrations.Security`
   - `Krackend.Sagas.Orchestrations.WebUI`
   - `Krackend.Sagas.Orchestrations.Client`
   - `Krackend.Sagas.Orchestrations.Client.Pigeon`

## Step 1 - Repository Preparation

- Pull latest `main`.
- Create a migration branch.
- Add `agents/` to `.gitignore`.
- Add this roadmap as a force-added tracked file.
- Commit repository preparation.

## Step 2 - Remove Singular Package Surface

- Remove `src/Krackend.Sagas.Orchestration`.
- Remove `tests/Krackend.Sagas.Orchestration.Tests`.
- Remove both projects from `Krackend.sln`.
- Clean README package references that point at singular orchestration only if they exist.
- Commit the package removal.

## Step 3 - Create New Package Skeleton

- Add new projects:
  - `Krackend.Sagas.Orchestrations.Abstractions`
  - `Krackend.Sagas.Orchestrations`
  - `Krackend.Sagas.Orchestrations.Messaging.Abstractions`
  - `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer`
  - `Krackend.Sagas.Orchestrations.Tests`
- Add them to the solution.
- Configure package metadata consistent with the repository.
- Commit skeleton.

## Step 4 - Migrate Runtime Abstractions And Primitives

- Copy/adapt source from `C:\dmx\Dmx.Orchestrator`:
  - `Orchestrator.Core`
  - runtime storage contracts from `Orchestrator.Runtime`
  - messaging contracts from `Orchestrator.Runtime.Messaging.Abstractions`
  - runtime intake contracts from `Orchestrator.Runtime.Intake`
- Rename namespaces to `Krackend.Sagas.Orchestrations.*`.
- Keep implementation dependencies minimal.
- Add compile-focused tests for core contracts where useful.
- Commit abstractions/primitives.

## Step 5 - Migrate Runtime Core And Engine

- Copy/adapt:
  - `Orchestrator.Runtime`
  - `Orchestrator.Runtime.Intake.InMemory`
  - `Orchestrator.Engine`
- Place in `Krackend.Sagas.Orchestrations`.
- Keep `AddKrackendSagasOrchestrationsRuntime` and `AddKrackendSagasOrchestrationsEngine` extension methods.
- Ensure the engine depends on messaging facade abstractions, not Pigeon.
- Add unit tests around DI registration and simple engine composition.
- Commit runtime core and engine.

## Step 6 - Migrate SQL Server Runtime Adapter

- Copy/adapt `Orchestrator.Runtime.Storage.SqlServer`.
- Place in `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer`.
- Keep migrations in the adapter package unless an explicit migrations host is needed.
- Add DI registration tests where possible.
- Commit SQL Server adapter.

## Step 7 - Migrate Messaging Facade

- Move facade abstractions fully into `Krackend.Sagas.Orchestrations.Messaging.Abstractions`.
- Ensure metadata travels as a single `Orchestrator.Metadata` object.
- Ensure the main runtime package can use in-memory messaging fallback without Pigeon.
- Commit messaging facade.

## Step 8 - Migrate Pigeon Adapter

- Copy/adapt `Orchestrator.Runtime.Messaging.Pigeon`.
- Place in `Krackend.Sagas.Orchestrations.Messaging.Pigeon`.
- Keep all Pigeon-specific references inside this adapter.
- Commit Pigeon adapter.

## Step 9 - Add Runtime Web Endpoints

- Copy/adapt runtime endpoint mappings from `Orchestrator.Runtime.Interaction`.
- Place in `Krackend.Sagas.Orchestrations.Web`.
- Keep Razor UI out of this phase.
- Commit web endpoints.

## Step 10 - Documentation And Samples

- Update README package list and usage examples.
- Add a minimal runtime host sample that mounts packages by references.
- Commit docs/sample.

## Step 11 - Validation

- Run solution build.
- Run tests.
- Pack projects if build/test is green.
- Commit any required fixes separately by concern.

## Deferred Work

- Full Control Plane migration: Design, Distribution, Security, WebUI.
- Client SDK and Client.Pigeon.
- Compatibility shims for old namespaces are intentionally not planned unless requested later.
