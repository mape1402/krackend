using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.Hooks;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pelican.Mediator;

var databasePath = Path.Combine(AppContext.BaseDirectory, "pelican-event-sourcing-sample.db");

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

    options.Routing.DefaultStreamName = "customers";
    options.Envelope.AddMetadata("sample", _ => "pelican-hooks");
});

services.AddKrackendEntityFrameworkEventStore<SampleDbContext>();
services.AddPelican(typeof(Program).Assembly);

services.AddScoped<ICommittedEventFactory<CreateCustomerCommand, Customer>, CustomerCreatedEventFactory>();
services.AddScoped<
    ICommandHandlerHook<CreateCustomerCommand, Customer>,
    EventSourcingPostSaveHook<CreateCustomerCommand, Customer>>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
await using var transaction = await dbContext.Database.BeginTransactionAsync();

var response = await mediator.Send(new CreateCustomerCommand(
    "customer-001",
    "Mario",
    "mario@example.com"));

await transaction.CommitAsync();

var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
var envelopes = await eventStore.LoadAsync("customers", response.Id);
var customers = await dbContext.Customers.AsNoTracking().ToListAsync();

Console.WriteLine($"SQLite database: {databasePath}");
Console.WriteLine($"Mediator response: {response.Id} {response.Name} {response.Email}");
Console.WriteLine($"Projection rows: {customers.Count}");
Console.WriteLine($"Events stored by hook: {envelopes.Count}");

foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
{
    Console.WriteLine($"{envelope.StreamVersion}: {envelope.EventType} metadata={envelope.Metadata}");
}
