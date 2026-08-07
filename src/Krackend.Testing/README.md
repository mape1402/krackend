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

Append with expected-version behavior and metadata:

```csharp
await eventStore.AppendAsync(
    stream,
    ExpectedVersion.NoStream,
    [new CustomerCreated(customerId)],
    new Dictionary<string, object?>
    {
        ["correlation-id"] = correlationId
    });
```

Assertions:

```csharp
eventStore.ShouldHaveStream("customers", customerId);
eventStore.ShouldHaveEvent<CustomerCreated>("customers", customerId);
eventStore.ShouldHaveEventsInOrder("customers", customerId, typeof(CustomerCreated), typeof(CustomerRenamed));
eventStore.ShouldHaveVersion("customers", customerId, 2);
eventStore.ShouldHaveMetadata("correlation-id", correlationId);
eventStore.ShouldHaveSerializedPayload("customers", customerId, """{"CustomerId":"customer-001"}""");
```

## Failure Simulation

```csharp
eventStore.FailNextAppendWithConcurrencyConflict(stream);
```

External test hosts can wrap `AddKrackendTestingAdapter()` with a host-specific extension:

```csharp
testHost.UseKrackendTesting();
```
