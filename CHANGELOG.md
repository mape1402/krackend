# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

------

## [v3.0.0] - 2026-09-18

- ### Added

  - Added provider-agnostic `Krackend.Security` and `Krackend.Security.Storage.EntityFramework` packages for product authorization with subjects, roles, direct permissions, external group role mappings, bootstrap administrators, ASP.NET Core policies, EF storage, and optional administration endpoints.
  - Added granular authorization options to the Control Plane and Runtime REST API packages so hosts can protect read, write, release, runtime management, and artifact apply operations independently.
  - Added host-level Entity Framework model customization hooks for Control Plane, Runtime, and Security storage adapters.
  - Added unit and integration coverage for subject resolution, bootstrap admin sync, direct permission assignments, direct role assignments, external group role assignments, wildcard admin access, scoped denial, and ASP.NET Core policy behavior.
  - Added architecture tests that guard the generic Entity Framework storage packages from choosing a concrete database provider or embedding provider-specific SQL.

- ### Changed

  - Made orchestration and security Entity Framework storage packages provider-agnostic by moving SQL Server package references, SQL Server column types, filtered indexes, and provider-specific duplicate handling out of the reusable storage adapters.
  - Updated sample hosts so SQL Server-specific mappings and filtered indexes live in the host projects and design-time factories, matching the host-owned provider model.
  - Moved runtime diagnostics contracts and reader from Runtime WebUI into Runtime core so WebUI and REST API remain sibling entry points over shared runtime services.
  - Updated Control Plane REST API actor resolution to prefer the authenticated principal when one exists while preserving the previous request-body fallback for unauthenticated compatibility scenarios.

------

## [v2.2.0] - 2026-09-17

- ### Added

  - Added optional `Krackend.Sagas.Orchestrations.ControlPlane.Api` and `Krackend.Sagas.Orchestrations.Runtime.Api` packages for mounting REST endpoints over existing Control Plane and Runtime services.
  - Added endpoint-level tests that validate REST route mapping, service delegation, runtime artifact standup signaling, ingress reads, and runtime design node reads across `net9.0` and `net10.0`.

------

## [v2.1.0] - 2026-09-17

- ### Added

  - Added KnOwl Control Plane command catalog resolution for exact and latest deployed command contracts, including request and reply contract artifacts.
  - Added design-time command schema bindings so orchestration tasks can reference a KnOwl command once and resolve request and response payload contracts separately.
  - Added schema context snapshot resolution before opening orchestration transform and validation contexts, keeping ButterMorph inputs aligned with the latest deployed KnOwl contracts.

- ### Changed

  - Updated the KnOwl schema registry adapter to consume the command request/reply catalog shape exposed by KnOwl `1.0.3`.
  - Changed orchestration artifact generation to expand command bindings into command request and command response snapshots while keeping published artifacts self-contained.
  - Updated orchestration stage design UI labels to expose command-level schema bindings without forcing users to pick request and response contracts independently.

- ### Fixed

  - Fixed command response schema resolution for orchestration artifacts and designer schema contexts.
  - Fixed KnOwl catalog path handling for deployed event and command contracts.
  - Fixed central package version alignment for EF Core and dependency injection packages across `net9.0` and `net10.0` builds.
  - Hardened runtime and schema registry test coverage around command request/reply resolution and versioned messaging orchestration scenarios.

------

## [v2.0.1] - 2026-09-15

- ### Added

  - Added `Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl`, a KnOwl Control Plane schema registry adapter for resolving deployed ButterMorph event and command contracts.
  - Added HTTP catalog resolution for KnOwl deployed contract artifacts by exact version and latest deployed version.
  - Added schema registry configuration wiring to the Control Plane sample host.

- ### Changed

  - Replaced the placeholder Atlas schema registry package with the KnOwl Control Plane adapter.
  - Enriched design and artifact schema snapshots with contract identity, source artifact id, resolver name, schema payload, and content hash so published orchestration artifacts remain self-contained.
  - Made the designer default schema registry provider key configurable through Control Plane WebUI options.

- ### Fixed

  - Fixed schema snapshot reuse so existing legacy snapshots remain usable while newly resolved snapshots are compared against their contract identity.
  - Fixed schema snapshot projection between Design storage, publish-time resolution, generated artifacts, and the transformation context payload.

------

## [v2.0.0] - 2026-09-14

- ### Added

  - Added NuGet package metadata for all source libraries, including runtime, client, control-plane, storage, messaging, buffering, schema-registry, and WebUI modules.
  - Added XML documentation generation for source packages and summaries for public runtime and adapter APIs introduced by the orchestration work.
  - Added a unified `Build and Release` workflow with `.release` marker validation, release branch/tag creation, GitHub release notes, NuGet packing, and trusted publishing.
  - Added package publication coverage for `Krackend.EventSourcing.Projections` and the current Krackend saga orchestration package layout.

- ### Changed

  - Changed package generation to run through explicit `dotnet pack` instead of package-on-build.
  - Updated the README package list and orchestration setup guidance to the current ControlPlane, Runtime, Client, adapter, schema registry, and WebUI package structure.

------

## [v1.2.1] - 2026-08-10

- ### Fixed

  - Corrected the event sourcing testing release surface to keep DI-friendly testing adapter APIs in `Krackend.EventSourcing.Testing` and avoid publishing a separate `Krackend.Testing` package.

------

## [v1.2.0] - 2026-08-09

- ### Added

  - Added DI-friendly in-memory event store and adapter services to `Krackend.EventSourcing.Testing` for external test host integrations.
  - Added event sourcing test assertions for stream existence, event type, event order, stream version, metadata, and serialized payload.
  - Added expected-version behavior and concurrency failure simulation to the event sourcing testing adapter.

------

## [v1.1.0] - 2026-08-06

- ### Added

  - Added `IRawEventStore` and `RawEventData` for appending raw JSON events without CLR event types.
  - Added raw append support to the in-memory and EF Core event stores.
  - Added dependency injection registration and tests for centralized raw event store scenarios.
  - Added a centralized raw event store sample using SQLite.

------

## [v1.0.0] - 2026-07-29

- ### Added

  - Added modular EventSourcing packages: Abstractions, Core, EntityFrameworkCore, Analyzers, and Testing.
  - Added event and state schema versioning with `EventSchemaAttribute`, `StateSchemaAttribute`, and `SemanticVersion`.
  - Added paged stream reads, expected version append modes, EF Core stores, snapshots, snapshot candidates, and diagnostic exceptions.
  - Added `IInitialStateFactory<TState>` support for application services, state rehydration, and snapshot processing.
  - Added automatic discovery of `IInitialStateFactory<TState>`, event schemas, state schemas, deciders, reducers, and stream resolvers.
  - Added `EventStreamAttribute` for declaring command stream names without custom stream resolver classes.
  - Added `IStateSchemaRegistry` with duplicate state schema detection.
  - Added Roslyn analyzers for duplicate event/state schemas and reducers or initial state factories using types without schemas.
  - Added `Krackend.EventSourcing.Testing` with helpers for stream envelopes, reducers, deciders, and test initial state factories.

------

## [v0.0.10] - 2025-08-11

Preview Version

- ### Added

  - 🎉 Preview Version: Add PublishOnSuccess extension method.

------

## [v0.0.9] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix orchestrator operations.

------

## [v0.0.8] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix roadmap building.

------

## [v0.0.7] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix controller dependency injection.

------

## [v0.0.6] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix roadmap creation.

------

## [v0.0.5] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix payload request type for orchestration consumers.

------

## [v0.0.4] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix dispatch action.

------

## [v0.0.3] - 2025-08-11

Preview Version

- ### Fixed

  - 🐛 Fix stage builder.

------

## [v0.0.2] - 2025-08-10

Preview Version

- ### Added

  - 🎉 Preview Version: Add orchestration controller services.

------

## [v0.0.1] - 2025-07-22

Preview Version

### Fixed

- 🐛 Change Forward extension method to ForwardSuccess
- 🐛 Fix default transform payload

------

## [v0.0.0] - 2025-07-21

Preview Version

### Added

- 🎉 Preview Version: Add orchestration working services and pipelines.
