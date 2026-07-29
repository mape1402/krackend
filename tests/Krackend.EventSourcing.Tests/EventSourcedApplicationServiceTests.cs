using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Diagnostics;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Stores;
using Krackend.EventSourcing.Streams;

namespace Krackend.EventSourcing.Tests;

public sealed class EventSourcedApplicationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_rehydrates_state_decides_events_and_commits_them()
    {
        var registry = new EventTypeRegistry()
            .Register<AccountOpened>("AccountOpened")
            .Register<MoneyDeposited>("MoneyDeposited");

        var serializer = new SystemTextJsonEventSerializer();
        var collector = new EventMetadataCollector(new EventEnvelopeOptions(), new EmptyServiceProvider());
        var factory = new EventEnvelopeFactory(registry, serializer, collector);
        var store = new InMemoryEventStore(factory);
        var reducers = new EventReducerRegistry()
            .Register<AccountState, AccountOpened>((state, @event) => state with
            {
                AccountId = @event.AccountId,
                Owner = @event.Owner,
                IsOpen = true
            })
            .Register<AccountState, MoneyDeposited>((state, @event) => state with
            {
                Balance = state.Balance + @event.Amount
            });

        await store.AppendAsync("accounts", "account-1", 0, [new AccountOpened("account-1", "Mario")]);

        var rehydrator = new StateRehydrator(store, serializer, registry, reducers);
        var service = new EventSourcedApplicationService<AccountState, DepositMoney>(
            rehydrator,
            new DepositMoneyDecider(),
            reducers,
            store,
            new StaticCommandStreamResolver<DepositMoney>("accounts", "account-1"),
            new DelegateInitialStateFactory<AccountState>(
                (_, _) => ValueTask.FromResult(AccountState.Empty),
                new EmptyServiceProvider()));

        var result = await service.ExecuteAsync(
            "accounts",
            "account-1",
            AccountState.Empty,
            new DepositMoney("account-1", 150m));

        Assert.Equal(1, result.PreviousVersion);
        Assert.Equal(2, result.CurrentVersion);
        Assert.Equal(150m, result.CurrentState.Balance);
        Assert.Single(result.CommittedEvents);
        Assert.Equal("MoneyDeposited", result.CommittedEvents.Single().EventType);
    }

    [Fact]
    public async Task ExecuteAsync_uses_configured_initial_state_factory()
    {
        var registry = new EventTypeRegistry()
            .Register<AccountOpened>("AccountOpened")
            .Register<MoneyDeposited>("MoneyDeposited");

        var serializer = new SystemTextJsonEventSerializer();
        var collector = new EventMetadataCollector(new EventEnvelopeOptions(), new EmptyServiceProvider());
        var factory = new EventEnvelopeFactory(registry, serializer, collector);
        var store = new InMemoryEventStore(factory);
        var reducers = new EventReducerRegistry()
            .Register<AccountState, AccountOpened>((state, @event) => state with
            {
                AccountId = @event.AccountId,
                Owner = @event.Owner,
                IsOpen = true
            })
            .Register<AccountState, MoneyDeposited>((state, @event) => state with
            {
                Balance = state.Balance + @event.Amount
            });

        await store.AppendAsync("accounts", "account-1", 0, [new AccountOpened("account-1", "Sample Owner")]);

        var rehydrator = new StateRehydrator(store, serializer, registry, reducers);
        var service = new EventSourcedApplicationService<AccountState, DepositMoney>(
            rehydrator,
            new DepositMoneyDecider(),
            reducers,
            store,
            new StaticCommandStreamResolver<DepositMoney>("accounts", "account-1"),
            new DelegateInitialStateFactory<AccountState>(
                (_, _) => ValueTask.FromResult(AccountState.Empty),
                new EmptyServiceProvider()));

        var result = await service.ExecuteAsync(new DepositMoney("account-1", 150m));

        Assert.Equal(1, result.PreviousVersion);
        Assert.Equal(2, result.CurrentVersion);
        Assert.Equal(150m, result.CurrentState.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_throws_when_initial_state_is_not_configured()
    {
        var registry = new EventTypeRegistry();
        var serializer = new SystemTextJsonEventSerializer();
        var collector = new EventMetadataCollector(new EventEnvelopeOptions(), new EmptyServiceProvider());
        var factory = new EventEnvelopeFactory(registry, serializer, collector);
        var store = new InMemoryEventStore(factory);
        var reducers = new EventReducerRegistry();
        var rehydrator = new StateRehydrator(store, serializer, registry, reducers);
        var service = new EventSourcedApplicationService<AccountState, DepositMoney>(
            rehydrator,
            new DepositMoneyDecider(),
            reducers,
            store,
            new StaticCommandStreamResolver<DepositMoney>("accounts", "account-1"),
            new MissingInitialStateFactory<AccountState>());

        var exception = await Assert.ThrowsAsync<InitialStateNotConfiguredException>(() =>
            service.ExecuteAsync(new DepositMoney("account-1", 150m)));

        Assert.Equal(typeof(AccountState), exception.StateType);
    }

    private sealed record AccountState(
        string AccountId,
        string Owner,
        decimal Balance,
        bool IsOpen)
    {
        public static AccountState Empty { get; } = new(string.Empty, string.Empty, 0m, false);
    }

    private sealed record DepositMoney(string AccountId, decimal Amount);

    private sealed record AccountOpened(string AccountId, string Owner);

    private sealed record MoneyDeposited(string AccountId, decimal Amount);

    private sealed class DepositMoneyDecider : IEventDecider<AccountState, DepositMoney>
    {
        public ValueTask<IReadOnlyCollection<object>> DecideAsync(
            AccountState state,
            DepositMoney command,
            CancellationToken cancellationToken = default)
        {
            if (!state.IsOpen)
                throw new InvalidOperationException("Account must be open.");

            if (command.Amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(command), "Deposit amount must be greater than zero.");

            return ValueTask.FromResult<IReadOnlyCollection<object>>([
                new MoneyDeposited(command.AccountId, command.Amount)
            ]);
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class StaticCommandStreamResolver<TCommand> : ICommandStreamResolver<TCommand>
    {
        private readonly EventStreamReference _stream;

        public StaticCommandStreamResolver(string streamName, string streamId)
        {
            _stream = EventStreamReference.Create(streamName, streamId);
        }

        public EventStreamReference Resolve(TCommand command) => _stream;
    }
}
