# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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
