using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Tests;

public sealed class StateRehydratorSnapshotTests
{
    [Fact]
    public async Task RehydrateAsync_starts_from_snapshot_and_reads_only_later_events()
    {
        var registry = new EventTypeRegistry().Register<MoneyDeposited>();
        var serializer = new SystemTextJsonEventSerializer();
        var reducers = new EventReducerRegistry()
            .Register<AccountState, MoneyDeposited>((state, @event) => state with
            {
                Balance = state.Balance + @event.Amount
            });
        var snapshotStore = new InMemorySnapshotStore();
        var snapshotSerializer = new SystemTextJsonSnapshotSerializer();

        await snapshotStore.SaveAsync(new Snapshot(
            "accounts",
            "account-1",
            100,
            snapshotSerializer.Serialize(new AccountState(500)),
            DateTimeOffset.UtcNow));

        var eventStore = new RecordingEventStore([
            new EventEnvelope(
                EventId: Guid.NewGuid(),
                StreamName: "accounts",
                StreamId: "account-1",
                StreamType: null,
                StreamVersion: 101,
                GlobalPosition: 1,
                EventType: "MoneyDeposited",
                EventSchemaVersion: "1.0.0",
                OccurredAt: DateTimeOffset.UtcNow,
                CorrelationId: null,
                CausationId: null,
                UserId: null,
                TenantId: null,
                Source: null,
                Payload: serializer.Serialize(new MoneyDeposited(25)),
                Metadata: null)
        ]);

        var rehydrator = new StateRehydrator(
            eventStore,
            serializer,
            registry,
            reducers,
            snapshotStore,
            snapshotSerializer,
            new EventSourcingOptions { RehydrationBatchSize = 25 });

        var result = await rehydrator.RehydrateAsync("accounts", "account-1", new AccountState(0));

        Assert.Equal(525, result.State.Balance);
        Assert.Equal(101, result.Version);
        Assert.Equal(101, eventStore.Reads.Single().FromVersion);
        Assert.Equal(25, eventStore.Reads.Single().MaxCount);
    }

    [Fact]
    public async Task RehydrateAsync_applies_reducers_for_each_event_schema_version()
    {
        var serializer = new SystemTextJsonEventSerializer();
        var registry = new EventTypeRegistry()
            .Register<LegacyBalanceMoved>()
            .Register<BalanceMoved>();
        var reducers = new EventReducerRegistry()
            .Register<BalanceState, LegacyBalanceMoved>((state, @event) => state with
            {
                Balance = @event.Balance,
                Description = "Legacy reducer"
            })
            .Register<BalanceState, BalanceMoved>((state, @event) => state with
            {
                Balance = @event.Balance,
                Description = @event.Description
            });
        var eventStore = new RecordingEventStore([
            new EventEnvelope(
                EventId: Guid.NewGuid(),
                StreamName: "accounts",
                StreamId: "account-1",
                StreamType: null,
                StreamVersion: 1,
                GlobalPosition: 1,
                EventType: "BalanceMoved",
                EventSchemaVersion: "1.0.0",
                OccurredAt: DateTimeOffset.UtcNow,
                CorrelationId: null,
                CausationId: null,
                UserId: null,
                TenantId: null,
                Source: null,
                Payload: serializer.Serialize(new LegacyBalanceMoved
                {
                    AccountId = "account-1",
                    Amount = 50m,
                    Balance = 50m
                }),
                Metadata: null),
            new EventEnvelope(
                EventId: Guid.NewGuid(),
                StreamName: "accounts",
                StreamId: "account-1",
                StreamType: null,
                StreamVersion: 2,
                GlobalPosition: 2,
                EventType: "BalanceMoved",
                EventSchemaVersion: "1.1.0",
                OccurredAt: DateTimeOffset.UtcNow,
                CorrelationId: null,
                CausationId: null,
                UserId: null,
                TenantId: null,
                Source: null,
                Payload: serializer.Serialize(new BalanceMoved
                {
                    AccountId = "account-1",
                    Amount = 25m,
                    Balance = 75m,
                    Description = "Current reducer"
                }),
                Metadata: null)
        ]);
        var rehydrator = new StateRehydrator(
            eventStore,
            serializer,
            registry,
            reducers,
            options: new EventSourcingOptions());

        var result = await rehydrator.RehydrateAsync("accounts", "account-1", BalanceState.Empty);

        Assert.Equal(75m, result.State.Balance);
        Assert.Equal("Current reducer", result.State.Description);
    }

    private sealed record AccountState(decimal Balance);

    private sealed record MoneyDeposited(decimal Amount);

    private sealed record BalanceState(decimal Balance, string Description)
    {
        public static BalanceState Empty { get; } = new(0m, string.Empty);
    }

    [EventSchema("BalanceMoved", "1.0.0")]
    private sealed class LegacyBalanceMoved
    {
        public string AccountId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal Balance { get; set; }
    }

    [EventSchema("BalanceMoved", "1.1.0")]
    private sealed class BalanceMoved
    {
        public string AccountId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal Balance { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    private sealed class RecordingEventStore : IEventStore
    {
        private readonly IReadOnlyCollection<EventEnvelope> _events;

        public RecordingEventStore(IReadOnlyCollection<EventEnvelope> events)
        {
            _events = events;
        }

        public List<ReadRequest> Reads { get; } = [];

        public Task<IReadOnlyCollection<EventEnvelope>> LoadAsync(
            string streamName,
            string streamId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Snapshot rehydration must use ranged stream reads.");

        public Task<IReadOnlyCollection<EventEnvelope>> ReadStreamAsync(
            string streamName,
            string streamId,
            long fromVersion,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            Reads.Add(new ReadRequest(fromVersion, maxCount));

            return Task.FromResult<IReadOnlyCollection<EventEnvelope>>(
                _events
                    .Where(envelope => envelope.StreamVersion >= fromVersion)
                    .Take(maxCount)
                    .ToArray());
        }

        public Task<long> GetCurrentVersionAsync(
            string streamName,
            string streamId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_events.Max(envelope => envelope.StreamVersion));

        public Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
            string streamName,
            string streamId,
            long expectedVersion,
            IReadOnlyCollection<object> events,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
            string streamName,
            string streamId,
            ExpectedVersion expectedVersion,
            IReadOnlyCollection<object> events,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record ReadRequest(long FromVersion, int MaxCount);
}
