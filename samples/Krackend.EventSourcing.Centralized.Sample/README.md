# Centralized Raw Event Store Sample

This sample demonstrates how to build a centralized event store that receives events from multiple bounded contexts without referencing their CLR event classes.

The sample uses:

- `IRawEventStore`
- `RawEventData`
- EF Core storage with SQLite
- one central logical stream named `integration-events`
- raw JSON payloads and metadata
- envelope correlation, causation, tenant, source, stream version, and global position

Run it with:

```bash
dotnet run --framework net9.0 --project samples/Krackend.EventSourcing.Centralized.Sample/Krackend.EventSourcing.Centralized.Sample.csproj
```

The sample appends:

- `Customers.CustomerCreated` schema `1.0.0`
- `Customers.CustomerRenamed` schema `1.1.0`
- `Payments.PaymentCaptured` schema `2.0.0`

The central store does not define any C# event type for those events. It stores the raw JSON payload exactly as received.
