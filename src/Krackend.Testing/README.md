# Krackend.Testing

`Krackend.Testing` provides an in-memory event store for testing event sourcing flows without a real event store.

## Setup

```csharp
services.AddKrackendTesting();
```

Adapter-friendly registration for external test hosts:

```csharp
services.AddKrackendTestingAdapter();
```

## Usage

```csharp
var eventStore = provider.GetRequiredService<IKrackendTestEventStore>();
var stream = EventStreamReference.Create("customers", customerId);

await eventStore.AppendAsync(stream, [new CustomerCreated(customerId)]);

var events = await eventStore.ReadAsync(stream);

eventStore.ShouldHaveEvent<CustomerCreated>("customers", customerId);
eventStore.ShouldHaveVersion("customers", customerId, 1);
```

## Failure Simulation

```csharp
eventStore.FailNextAppendWithConcurrencyConflict(stream);
```
