# Krackend.Sagas.Orchestrations Runtime Host Sample

Minimal ASP.NET Core host that mounts the saga orchestration runtime from libraries.

It wires:

- `Krackend.Sagas.Orchestrations`
- `Krackend.Sagas.Orchestrations.Messaging.Abstractions`
- `Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer`
- `Krackend.Sagas.Orchestrations.Web`

The sample uses SQL Server storage and in-memory trigger intake. Messaging is registered through the neutral facade; broker-specific adapters such as Pigeon can be added by the host when needed.
