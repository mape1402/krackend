using Krackend.EventSourcing.Configuration;
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
                Guid.NewGuid(),
                "accounts",
                "account-1",
                null,
                101,
                1,
                "MoneyDeposited",
                1,
                DateTimeOffset.UtcNow,
                serializer.Serialize(new MoneyDeposited(25)),
                null)
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

    private sealed record AccountState(decimal Balance);

    private sealed record MoneyDeposited(decimal Amount);

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
            IReadOnlyCollection<object> events,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
            string streamName,
            string streamId,
            long expectedVersion,
            IReadOnlyCollection<object> events,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record ReadRequest(long FromVersion, int MaxCount);
}
