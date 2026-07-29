# Event Sourcing Release Checklist

Use this checklist before publishing the EventSourcing packages.

## Validation

Run:

```bash
dotnet build Krackend.sln --configuration Release
dotnet test Krackend.sln --configuration Release --no-build --verbosity minimal
```

Expected result:

- build succeeds with zero warnings
- all tests pass
- packages are generated for every packable EventSourcing project

## Packages

Release these packages together:

- `Krackend.EventSourcing.Abstractions`
- `Krackend.EventSourcing`
- `Krackend.EventSourcing.EntityFrameworkCore`
- `Krackend.EventSourcing.Projections`
- `Krackend.EventSourcing.Analyzers`

Do not publish Spider/Pelican extension packages until their integration contracts are stable.

## Stable Contract Checks

Before tagging:

- `IEventStore` does not expose unbounded stream reads.
- EF Core is not referenced by `Krackend.EventSourcing`.
- Projections are not referenced by `Krackend.EventSourcing`.
- Spider and Pelican are not referenced by `Krackend.EventSourcing`.
- Events use `EventSchema(name, version)`.
- Snapshot state uses `StateSchema(name, version)`.
- Missing reducers fail with `EventReducerNotRegisteredException`.
- Duplicate event schemas fail with `DuplicateEventSchemaException`.
- Event store table naming keeps the default `Events`.

## Versioning

Packages use MinVer from repository tags.

For a stable release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Only tag after the sample projects and docs are aligned with the public API.
