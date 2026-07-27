using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.EntityFrameworkCore;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Sqlite.Sample.Commands;
using Krackend.EventSourcing.Sqlite.Sample.Data;
using Krackend.EventSourcing.Sqlite.Sample.Events;
using Krackend.EventSourcing.Sqlite.Sample.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var databasePath = Path.Combine(AppContext.BaseDirectory, "event-sourcing-sample.db");

if (File.Exists(databasePath))
    File.Delete(databasePath);

var services = new ServiceCollection();

services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });

    options.Envelope.AddMetadata("sample", _ => "sqlite");
});

services.AddKrackendEntityFrameworkEventStore<SampleDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var createCustomer = scope.ServiceProvider.GetRequiredService<IEventSourcedApplicationService<CustomerState, CreateCustomer>>();
var renameCustomer = scope.ServiceProvider.GetRequiredService<IEventSourcedApplicationService<CustomerState, RenameCustomer>>();

var created = await createCustomer.ExecuteAsync(
    "customers",
    "customer-001",
    CustomerState.Empty,
    new CreateCustomer("customer-001", "Mario", "mario@example.com"));

var renamed = await renameCustomer.ExecuteAsync(
    "customers",
    "customer-001",
    CustomerState.Empty,
    new RenameCustomer("customer-001", "Mario Perez"));

var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
var envelopes = await eventStore.LoadAsync("customers", "customer-001");

Console.WriteLine($"SQLite database: {databasePath}");
Console.WriteLine($"Customer: {renamed.CurrentState.CustomerId}");
Console.WriteLine($"Name: {renamed.CurrentState.Name}");
Console.WriteLine($"Email: {renamed.CurrentState.Email}");
Console.WriteLine($"Created version: {created.CurrentVersion}");
Console.WriteLine($"Current version: {renamed.CurrentVersion}");
Console.WriteLine($"Events stored: {envelopes.Count}");

foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
{
    Console.WriteLine($"{envelope.StreamVersion}: {envelope.EventType} metadata={envelope.Metadata}");
}
