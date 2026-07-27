using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.Hooks;
using Krackend.EventSourcing.Pelican.Sample.State;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Krackend.EventSourcing.Snapshots;
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

services.AddSingleton<ISnapshotCandidatePolicy>(new IntervalSnapshotCandidatePolicy(1));
services.AddKrackendEntityFrameworkEventStore<SampleDbContext>();
services.AddPelican(typeof(Program).Assembly);

services.AddCommittedEvents(events =>
{
    events.CreateMultiMap<CustomerCreated>()
        .From<CreateCustomerCommand>()
        .From<Customer>()
        .ConstructUsing((_, customer) =>
            new CustomerCreated(customer.Id, customer.Name, customer.Email));
});
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
var candidateStore = scope.ServiceProvider.GetRequiredService<ISnapshotCandidateStore>();
var snapshotStore = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
var snapshotProcessor = scope.ServiceProvider.GetRequiredService<ISnapshotProcessor<CustomerState>>();

var pendingBefore = await candidateStore.GetPendingAsync(10);
var snapshotBefore = await snapshotStore.LoadLatestAsync("customers", response.Id);
var snapshotResults = await snapshotProcessor.ProcessPendingAsync(CustomerState.Empty, maxCount: 10);
var pendingAfter = await candidateStore.GetPendingAsync(10);
var snapshotAfter = await snapshotStore.LoadLatestAsync("customers", response.Id);
var rehydrator = scope.ServiceProvider.GetRequiredService<IStateRehydrator>();
var rehydrated = await rehydrator.RehydrateAsync("customers", response.Id, CustomerState.Empty);

Console.WriteLine($"SQLite database: {databasePath}");
Console.WriteLine($"Mediator response: {response.Id} {response.Name} {response.Email}");
Console.WriteLine($"Projection rows: {customers.Count}");
Console.WriteLine($"Events stored by hook: {envelopes.Count}");
Console.WriteLine($"Snapshot candidates before worker: {pendingBefore.Count}");
Console.WriteLine($"Snapshot before worker: {(snapshotBefore is null ? "none" : snapshotBefore.StreamVersion)}");
Console.WriteLine($"Snapshots saved by worker: {snapshotResults.Count(x => x.SnapshotSaved)}");
Console.WriteLine($"Snapshot candidates after worker: {pendingAfter.Count}");
Console.WriteLine($"Snapshot after worker: {(snapshotAfter is null ? "none" : snapshotAfter.StreamVersion)}");
Console.WriteLine($"Rehydrated from snapshot: {rehydrated.State.CustomerId} v{rehydrated.Version}");

foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
{
    Console.WriteLine($"{envelope.StreamVersion}: {envelope.EventType} metadata={envelope.Metadata}");
}
