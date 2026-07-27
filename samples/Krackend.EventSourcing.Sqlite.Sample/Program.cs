using Krackend.EventSourcing.Aggregates;
using Krackend.EventSourcing.DependencyInjection;
using Krackend.EventSourcing.EntityFrameworkCore;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Repositories;
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
    options.Stores.Add("banking", store =>
    {
        store.TableName = "BankingEvents";
    });

    options.Envelope.AddMetadata("sample", _ => "sqlite");
});

services.AddKrackendEntityFrameworkEventStore<SampleDbContext>();
services.AddScoped<IEventSourcedRepository<BankAccount>>(provider =>
    new EventSourcedRepository<BankAccount>(
        provider.GetRequiredService<IEventStore>(),
        provider.GetRequiredService<Krackend.EventSourcing.Serialization.IEventSerializer>(),
        provider.GetRequiredService<IEventTypeRegistry>(),
        "banking"));

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var registry = scope.ServiceProvider.GetRequiredService<EventTypeRegistry>();
registry.Register<AccountOpened>();
registry.Register<MoneyDeposited>();

var repository = scope.ServiceProvider.GetRequiredService<IEventSourcedRepository<BankAccount>>();
var account = new BankAccount();

account.Open("account-001", "Mario");
account.Deposit(150m);
await repository.SaveAsync(account);

var rehydrated = await repository.LoadAsync("account-001");
var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
var envelopes = await eventStore.LoadAsync("banking", "account-001");

Console.WriteLine($"SQLite database: {databasePath}");
Console.WriteLine($"Account: {rehydrated.Id}");
Console.WriteLine($"Owner: {rehydrated.Owner}");
Console.WriteLine($"Balance: {rehydrated.Balance}");
Console.WriteLine($"Version: {rehydrated.Version}");
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

public sealed class BankAccount : AggregateRoot
{
    public string Owner { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public void Open(string accountId, string owner)
    {
        if (!string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException("Account is already open.");

        Raise(new AccountOpened(accountId, owner));
    }

    public void Deposit(decimal amount)
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException("Account must be opened before depositing money.");

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be greater than zero.");

        Raise(new MoneyDeposited(Id, amount));
    }

    private void Apply(AccountOpened @event)
    {
        Id = @event.AccountId;
        Owner = @event.Owner;
    }

    private void Apply(MoneyDeposited @event)
    {
        Balance += @event.Amount;
    }
}

public sealed record AccountOpened(string AccountId, string Owner);

public sealed record MoneyDeposited(string AccountId, decimal Amount);
