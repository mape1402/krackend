# Krackend

Modular backend building blocks for .NET services.

[![Build](https://github.com/mape1402/krackend/actions/workflows/CI.yml/badge.svg)](https://github.com/mape1402/krackend/actions/workflows/CI.yml)
[![NuGet](https://img.shields.io/nuget/v/Krackend.Sagas.Orchestration.svg)](https://www.nuget.org/packages/Krackend.Sagas.Orchestration/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Packages

```bash
dotnet add package Krackend.Sagas.Orchestration
```

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

## Event Sourcing

`Krackend.EventSourcing` provides a modular write-model runtime for event sourcing:

- event and state schema versioning
- expected-version appends
- paged stream reads
- state rehydration with reducers
- snapshots of state
- EF Core event store adapter
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

See [docs/event-sourcing.md](docs/event-sourcing.md) for the full guide.
