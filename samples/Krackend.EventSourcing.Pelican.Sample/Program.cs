using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Pelican.Sample.Commands;
using Krackend.EventSourcing.Pelican.Sample.Data;
using Krackend.EventSourcing.Pelican.Sample.Domain;
using Krackend.EventSourcing.Pelican.Sample.Events;
using Krackend.EventSourcing.Pelican.Sample.Hooks;
using Krackend.EventSourcing.Pelican.Sample.RequestContext;
using Krackend.EventSourcing.Pelican.Sample.State;
using Krackend.EventSourcing.Pelican.Sample.TemplateCore;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OctoMap;
using Pelican.Mediator;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables("KRACKEND_ES_")
    .Build();

var connectionString = configuration.GetConnectionString("EventSourcingSample");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:EventSourcingSample with user secrets or environment variables before running the sample. "
        + "Example: dotnet user-secrets set \"ConnectionStrings:EventSourcingSample\" \"Server=...;Database=...;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True\"");
}

var services = new ServiceCollection();

services.AddSingleton(new CustomerInitialStateDefaults(0m));
services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddKrackendEventSourcing(options =>
{
    options.Stores.Add("customers", store =>
    {
        store.TableName = "CustomerEvents";
    });

    options.Envelope.AddMetadata("sample", _ => "pelican-hooks");
});

services.AddSingleton<ICurrentRequestContextAccessor, CurrentRequestContextAccessor>();
services.AddEventExecutionContext(provider =>
    new CurrentRequestEventExecutionContext(
        provider.GetRequiredService<ICurrentRequestContextAccessor>()));
services.AddSingleton<ISnapshotCandidatePolicy>(new IntervalSnapshotCandidatePolicy(1));
services.AddKrackendEntityFrameworkEventStore<SampleDbContext>();
services.AddPelican(typeof(Program).Assembly);
services.AddOctoMap(typeof(Program).Assembly);

services.AddCommittedEvents(events =>
{
    events.Map<CreateCustomerCommand, Customer, CustomerCreated>();
    events.Map<RenameCustomerLegacyCommand, Customer, CustomerRenamedV1>();
    events.Map<RenameCustomerCommand, Customer, CustomerRenamed>();
    events.Map<ApplyLegacyBalanceMovementCommand, Customer, CustomerBalanceMovedV1>();
    events.Map<ApplyBalanceMovementCommand, Customer, CustomerBalanceMoved>();
});
services.AddScoped<
    ICommandHandlerHook<CreateCustomerCommand, Customer>,
    EventSourcingPostSaveHook<CreateCustomerCommand, Customer>>();
services.AddScoped<
    ICommandHandlerHook<RenameCustomerLegacyCommand, Customer>,
    EventSourcingPostSaveHook<RenameCustomerLegacyCommand, Customer>>();
services.AddScoped<
    ICommandHandlerHook<RenameCustomerCommand, Customer>,
    EventSourcingPostSaveHook<RenameCustomerCommand, Customer>>();
services.AddScoped<
    ICommandHandlerHook<ApplyBalanceMovementCommand, Customer>,
    EventSourcingPostSaveHook<ApplyBalanceMovementCommand, Customer>>();
services.AddScoped<
    ICommandHandlerHook<ApplyLegacyBalanceMovementCommand, Customer>,
    EventSourcingPostSaveHook<ApplyLegacyBalanceMovementCommand, Customer>>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
await dbContext.Database.EnsureDeletedAsync();
await dbContext.Database.EnsureCreatedAsync();

var requestContextAccessor = scope.ServiceProvider.GetRequiredService<ICurrentRequestContextAccessor>();
requestContextAccessor.Current = new CurrentRequestContext(
    CorrelationId: Guid.NewGuid().ToString("N"),
    CausationId: $"console:{Guid.NewGuid():N}",
    UserId: configuration["Sample:UserId"] ?? "operator-001",
    TenantId: configuration["Sample:TenantId"] ?? "tenant-001",
    Source: configuration["Sample:Source"] ?? "pelican-sample");

var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
await using var transaction = await dbContext.Database.BeginTransactionAsync();

var created = await mediator.Send(new CreateCustomerCommand(
    "customer-001",
    "Sample Customer",
    "customer@example.test"));
var renamed = await mediator.Send(new RenameCustomerLegacyCommand(
    "customer-001",
    "Sample Customer Legacy"));
var renamedAgain = await mediator.Send(new RenameCustomerCommand(
    "customer-001",
    "Sample Customer Current",
    "Legal name update"));
var legacyDeposit = await mediator.Send(new ApplyLegacyBalanceMovementCommand(
    "customer-001",
    250m));
var payment = await mediator.Send(new ApplyBalanceMovementCommand(
    "customer-001",
    -75m,
    "Card payment"));

await transaction.CommitAsync();

var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
var envelopes = await eventStore.ReadStreamAsync("customers", created.Id, fromVersion: 1, maxCount: 100);
var customers = await dbContext.Customers.AsNoTracking().ToListAsync();
var candidateStore = scope.ServiceProvider.GetRequiredService<ISnapshotCandidateStore>();
var snapshotStore = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();
var snapshotProcessor = scope.ServiceProvider.GetRequiredService<ISnapshotProcessor<CustomerState>>();

var pendingBefore = await candidateStore.GetPendingAsync(10);
var snapshotBefore = await snapshotStore.LoadLatestAsync("customers", created.Id);
var snapshotResults = await snapshotProcessor.ProcessPendingAsync(maxCount: 10);
var pendingAfter = await candidateStore.GetPendingAsync(10);
var snapshotAfter = await snapshotStore.LoadLatestAsync("customers", created.Id);
var rehydrator = scope.ServiceProvider.GetRequiredService<IStateRehydrator>();
var rehydrated = await rehydrator.RehydrateAsync<CustomerState>("customers", created.Id);

Console.WriteLine("SQL Server connection: ConnectionStrings:EventSourcingSample");
Console.WriteLine($"Created response: {created.Id} {created.Name} {created.Email} balance={created.Balance}");
Console.WriteLine($"Legacy rename response: {renamed.Id} {renamed.Name} {renamed.Email} balance={renamed.Balance}");
Console.WriteLine($"Current rename response: {renamedAgain.Id} {renamedAgain.Name} {renamedAgain.Email} balance={renamedAgain.Balance}");
Console.WriteLine($"Legacy deposit response: {legacyDeposit.Id} {legacyDeposit.Name} balance={legacyDeposit.Balance}");
Console.WriteLine($"Payment response: {payment.Id} {payment.Name} balance={payment.Balance}");
Console.WriteLine($"Projection rows: {customers.Count}");
Console.WriteLine($"Events stored by hook: {envelopes.Count}");
Console.WriteLine($"Snapshot candidates before worker: {pendingBefore.Count}");
Console.WriteLine($"Snapshot before worker: {(snapshotBefore is null ? "none" : snapshotBefore.StreamVersion)}");
Console.WriteLine($"Snapshots saved by worker: {snapshotResults.Count(x => x.SnapshotSaved)}");
Console.WriteLine($"Snapshot candidates after worker: {pendingAfter.Count}");
Console.WriteLine($"Snapshot after worker: {(snapshotAfter is null ? "none" : snapshotAfter.StreamVersion)}");
Console.WriteLine($"Rehydrated from snapshot: {rehydrated.State.CustomerId} {rehydrated.State.Name} balance={rehydrated.State.Balance} v{rehydrated.Version}");

foreach (var envelope in envelopes.OrderBy(x => x.StreamVersion))
{
    Console.WriteLine(
        $"{envelope.StreamVersion}: {envelope.EventType} schema={envelope.EventSchemaVersion} "
        + $"correlation={envelope.CorrelationId} causation={envelope.CausationId} "
        + $"user={envelope.UserId} tenant={envelope.TenantId} source={envelope.Source} "
        + $"metadata={envelope.Metadata}");
}
