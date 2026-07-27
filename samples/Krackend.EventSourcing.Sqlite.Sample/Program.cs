using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.EntityFrameworkCore;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Stores;
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
services.AddScoped<IEventDecider<CustomerState, CreateCustomer>, CreateCustomerDecider>();
services.AddScoped<IEventDecider<CustomerState, RenameCustomer>, RenameCustomerDecider>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var registry = scope.ServiceProvider.GetRequiredService<EventTypeRegistry>();
registry.Register<CustomerCreated>();
registry.Register<CustomerRenamed>();

var reducers = scope.ServiceProvider.GetRequiredService<IEventReducerRegistry>();
reducers
    .Register<CustomerState, CustomerCreated>((state, @event) => state with
    {
        CustomerId = @event.CustomerId,
        Name = @event.Name,
        Email = @event.Email,
        IsCreated = true
    })
    .Register<CustomerState, CustomerRenamed>((state, @event) => state with
    {
        Name = @event.Name
    });

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

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options)
        : base(options)
    {
    }
}

public sealed record CustomerState(
    string CustomerId,
    string Name,
    string Email,
    bool IsCreated)
{
    public static CustomerState Empty { get; } = new(string.Empty, string.Empty, string.Empty, false);
}

public sealed record CreateCustomer(string CustomerId, string Name, string Email);

public sealed record RenameCustomer(string CustomerId, string Name);

public sealed record CustomerCreated(string CustomerId, string Name, string Email);

public sealed record CustomerRenamed(string CustomerId, string Name);

public sealed class CreateCustomerDecider : IEventDecider<CustomerState, CreateCustomer>
{
    public ValueTask<IReadOnlyCollection<object>> DecideAsync(
        CustomerState state,
        CreateCustomer command,
        CancellationToken cancellationToken = default)
    {
        if (state.IsCreated)
            throw new InvalidOperationException("Customer already exists.");

        return ValueTask.FromResult<IReadOnlyCollection<object>>([
            new CustomerCreated(command.CustomerId, command.Name, command.Email)
        ]);
    }
}

public sealed class RenameCustomerDecider : IEventDecider<CustomerState, RenameCustomer>
{
    public ValueTask<IReadOnlyCollection<object>> DecideAsync(
        CustomerState state,
        RenameCustomer command,
        CancellationToken cancellationToken = default)
    {
        if (!state.IsCreated)
            throw new InvalidOperationException("Customer must exist before it can be renamed.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Customer name is required.", nameof(command));

        return ValueTask.FromResult<IReadOnlyCollection<object>>([
            new CustomerRenamed(command.CustomerId, command.Name)
        ]);
    }
}
