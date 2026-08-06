using Krackend.EventSourcing.Centralized.Sample.Data;
using Krackend.EventSourcing.Centralized.Sample.Ingestion;
using Krackend.EventSourcing.Centralized.Sample.Requests;
using Krackend.EventSourcing.Centralized.Sample.Runtime;
using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var databasePath = Path.Combine(AppContext.BaseDirectory, "central-event-store-sample.db");

if (File.Exists(databasePath))
    File.Delete(databasePath);

var services = new ServiceCollection();

services.AddScoped<SampleExecutionContext>();
services.AddEventExecutionContext(provider =>
    provider.GetRequiredService<SampleExecutionContext>());

services.AddDbContext<CentralEventStoreDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("integration-events", store =>
    {
        store.TableName = "IntegrationEvents";
    });

    options.Envelope.AddMetadata("collector", _ => "central-event-store");
});

services.AddScoped<CentralEventIngestionService>();
services.AddKrackendEntityFrameworkEventStore<CentralEventStoreDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<CentralEventStoreDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var executionContext = scope.ServiceProvider.GetRequiredService<SampleExecutionContext>();
executionContext.CorrelationId = "http-request-8f70";
executionContext.CausationId = "message-3b91";
executionContext.TenantId = "tenant-demo";
executionContext.Source = "central-ingestion-api";

var ingestion = scope.ServiceProvider.GetRequiredService<CentralEventIngestionService>();

await ingestion.AppendAsync(new IncomingIntegrationEvent(
    BoundedContext: "customers",
    AggregateType: "customer",
    AggregateId: "customer-001",
    EventType: "Customers.CustomerCreated",
    EventSchemaVersion: new SemanticVersion(1, 0, 0),
    Payload: """
    {"customerId":"customer-001","name":"Ada Lovelace","email":"ada@example.test"}
    """,
    Metadata: """
    {"producer":"customers-api","messageId":"customers-0001"}
    """,
    ExpectedStreamVersion: 0));

await ingestion.AppendAsync(new IncomingIntegrationEvent(
    BoundedContext: "customers",
    AggregateType: "customer",
    AggregateId: "customer-001",
    EventType: "Customers.CustomerRenamed",
    EventSchemaVersion: new SemanticVersion(1, 1, 0),
    Payload: """
    {"customerId":"customer-001","firstName":"Ada","lastName":"Byron","displayName":"Ada Byron"}
    """,
    Metadata: """
    {"producer":"customers-api","messageId":"customers-0002"}
    """,
    ExpectedStreamVersion: 1));

await ingestion.AppendAsync(new IncomingIntegrationEvent(
    BoundedContext: "payments",
    AggregateType: "payment",
    AggregateId: "payment-001",
    EventType: "Payments.PaymentCaptured",
    EventSchemaVersion: new SemanticVersion(2, 0, 0),
    Payload: """
    {"paymentId":"payment-001","customerId":"customer-001","amount":250.75,"currency":"USD"}
    """,
    Metadata: """
    {"producer":"payments-api","messageId":"payments-0001"}
    """));

var rawEventStore = scope.ServiceProvider.GetRequiredService<IRawEventStore>();

var customerStream = await rawEventStore.ReadStreamAsync(
    "integration-events",
    "customers:customer:customer-001",
    fromVersion: 1,
    maxCount: 100);

var globalLog = await rawEventStore.ReadFromAsync(
    "integration-events",
    afterGlobalPosition: 0,
    maxCount: 100);

Console.WriteLine($"Central event store database: {databasePath}");
Console.WriteLine();
Console.WriteLine("Customer stream");

foreach (var envelope in customerStream.OrderBy(x => x.StreamVersion))
{
    Console.WriteLine($"{envelope.StreamVersion}: {envelope.EventType} schema={envelope.EventSchemaVersion}");
    Console.WriteLine($"payload={envelope.Payload}");
}

Console.WriteLine();
Console.WriteLine("Global log");

foreach (var envelope in globalLog.OrderBy(x => x.GlobalPosition))
{
    Console.WriteLine($"{envelope.GlobalPosition}: {envelope.StreamId} / {envelope.EventType}");
    Console.WriteLine($"correlation={envelope.CorrelationId} causation={envelope.CausationId} tenant={envelope.TenantId}");
}
