using Krackend.EventSourcing.Aggregates;
using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Repositories;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Tests;

public sealed class EventSourcedRepositoryTests
{
    [Fact]
    public async Task SaveAsync_commits_pending_events_and_loads_aggregate_state()
    {
        var registry = new EventTypeRegistry().Register<AccountOpened>();
        var serializer = new SystemTextJsonEventSerializer();
        var collector = new EventMetadataCollector(new EventEnvelopeOptions(), new EmptyServiceProvider());
        var factory = new EventEnvelopeFactory(registry, serializer, collector);
        var store = new InMemoryEventStore(factory);
        var repository = new EventSourcedRepository<Account>(store, serializer, registry, "accounts");
        var account = new Account();

        account.Open("account-1", "Mario");
        await repository.SaveAsync(account);

        var loaded = await repository.LoadAsync("account-1");

        Assert.Equal("account-1", loaded.Id);
        Assert.Equal("Mario", loaded.Owner);
        Assert.Equal(1, loaded.Version);
        Assert.Empty(account.PendingEvents);
    }

    private sealed class Account : AggregateRoot
    {
        public string Owner { get; private set; } = string.Empty;

        public void Open(string id, string owner)
            => Raise(new AccountOpened(id, owner));

        private void Apply(AccountOpened @event)
        {
            Id = @event.AccountId;
            Owner = @event.Owner;
        }
    }

    private sealed record AccountOpened(string AccountId, string Owner);

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
