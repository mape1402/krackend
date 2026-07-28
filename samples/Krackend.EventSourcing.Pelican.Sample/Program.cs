using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.Hooks;
using Krackend.EventSourcing.Pelican.Sample.State;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OctoMap;
using Pelican.Mediator;

const string serverName = "DF-1613-02";
const string databaseName = "KrackendEventSourcingSample";
const string connectionString =
    $"Server={serverName};Database={databaseName};Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

var services = new ServiceCollection();

services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });

    options.Routing.DefaultStreamName = "customers";
    options.Envelope.AddMetadata("sample", _ => "pelican-hooks");
});

services.AddEventExecutionContext(_ => new EventExecutionContext(
    CorrelationId: "request-001",
    CausationId: "http-request-001",
    UserId: "mario",
    TenantId: "elysium",
    Source: "pelican-sample"));
services.AddSingleton<ISnapshotCandidatePolicy>(new IntervalSnapshotCandidatePolicy(1));
services.AddKrackendEntityFrameworkEventStore<SampleDbContext>();
services.AddPelican(typeof(Program).Assembly);
services.AddOctoMap(typeof(Program).Assembly);

services.AddCommittedEvents(events =>
{
    events.Map<CreateCustomerCommand, Customer, CustomerCreated>();
    events.Map<RenameCustomerCommand, Customer, CustomerRenamed>();
});
services.AddScoped<
    ICommandHandlerHook<CreateCustomerCommand, Customer>,
    EventSourcingPostSaveHook<CreateCustomerCommand, Customer>>();
services.AddScoped<
    ICommandHandlerHook<RenameCustomerCommand, Customer>,
    EventSourcingPostSaveHook<RenameCustomerCommand, Customer>>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
await dbContext.Database.EnsureDeletedAsync();
await dbContext.Database.EnsureCreatedAsync();

var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
await using var transaction = await dbContext.Database.BeginTransactionAsync();

var created = await mediator.Send(new CreateCustomerCommand(
    "customer-001",
    "Mario",
    "mario@example.com"));
var renamed = await mediator.Send(new RenameCustomerCommand(
    "customer-001",
    "Mario Perez"));
var renamedAgain = await mediator.Send(new RenameCustomerCommand(
    "customer-001",
    "Mario Perez Jr"));

await transaction.CommitAsync();

var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
var envelopes = await eventStore.LoadAsync("customers", created.Id);
var customers = await dbContext.Customers.AsNoTracking().ToListAsync();
var candidateStore = scope.ServiceProvider.GetRequiredService<ISnapshotCandidateStore>();
var snapshotStore = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
var snapshotProcessor = scope.ServiceProvider.GetRequiredService<ISnapshotProcessor<CustomerState>>();

var pendingBefore = await candidateStore.GetPendingAsync(10);
var snapshotBefore = await snapshotStore.LoadLatestAsync("customers", created.Id);
var snapshotResults = await snapshotProcessor.ProcessPendingAsync(CustomerState.Empty, maxCount: 10);
var pendingAfter = await candidateStore.GetPendingAsync(10);
var snapshotAfter = await snapshotStore.LoadLatestAsync("customers", created.Id);
var rehydrator = scope.ServiceProvider.GetRequiredService<IStateRehydrator>();
var rehydrated = await rehydrator.RehydrateAsync("customers", created.Id, CustomerState.Empty);

Console.WriteLine($"SQL Server: {serverName}");
Console.WriteLine($"Database: {databaseName}");
Console.WriteLine($"Created response: {created.Id} {created.Name} {created.Email}");
Console.WriteLine($"Renamed response: {renamed.Id} {renamed.Name} {renamed.Email}");
Console.WriteLine($"Renamed again response: {renamedAgain.Id} {renamedAgain.Name} {renamedAgain.Email}");
Console.WriteLine($"Projection rows: {customers.Count}");
Console.WriteLine($"Events stored by hook: {envelopes.Count}");
Console.WriteLine($"Snapshot candidates before worker: {pendingBefore.Count}");
Console.WriteLine($"Snapshot before worker: {(snapshotBefore is null ? "none" : snapshotBefore.StreamVersion)}");
Console.WriteLine($"Snapshots saved by worker: {snapshotResults.Count(x => x.SnapshotSaved)}");
Console.WriteLine($"Snapshot candidates after worker: {pendingAfter.Count}");
Console.WriteLine($"Snapshot after worker: {(snapshotAfter is null ? "none" : snapshotAfter.StreamVersion)}");
Console.WriteLine($"Rehydrated from snapshot: {rehydrated.State.CustomerId} {rehydrated.State.Name} v{rehydrated.Version}");

foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
{
    Console.WriteLine(
        $"{envelope.StreamVersion}: {envelope.EventType} "
        + $"correlation={envelope.CorrelationId} causation={envelope.CausationId} "
        + $"user={envelope.UserId} tenant={envelope.TenantId} source={envelope.Source} "
        + $"metadata={envelope.Metadata}");
}
