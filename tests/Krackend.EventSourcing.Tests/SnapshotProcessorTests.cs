using Krackend.EventSourcing.Configuration;
using Krackend.EventSourcing.Core;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Metadata;
using Krackend.EventSourcing.Registry;
using Krackend.EventSourcing.Serialization;
using Krackend.EventSourcing.Snapshots;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Tests;

public sealed class SnapshotProcessorTests
{
    [Fact]
    public async Task Append_marks_candidate_and_processor_creates_snapshot_later()
    {
        var registry = new EventTypeRegistry().Register<CounterIncremented>();
        var eventSerializer = new SystemTextJsonEventSerializer();
        var snapshotSerializer = new SystemTextJsonSnapshotSerializer();
        var snapshotStore = new InMemorySnapshotStore();
        var candidateStore = new InMemorySnapshotCandidateStore();
        var marker = new SnapshotCandidateMarker(
            snapshotStore,
            candidateStore,
            new IntervalSnapshotCandidatePolicy(2));
        var store = new InMemoryEventStore(
            new EventEnvelopeFactory(
                registry,
                eventSerializer,
                new EventMetadataCollector(new EventEnvelopeOptions(), new EmptyServiceProvider())),
            marker);

        await store.AppendAsync("counters", "counter-1", ExpectedVersion.Any, [new CounterIncremented(1)]);
        Assert.Empty(await candidateStore.GetPendingAsync(10));
        Assert.Null(await snapshotStore.LoadLatestAsync("counters", "counter-1"));

        await store.AppendAsync("counters", "counter-1", ExpectedVersion.Any, [new CounterIncremented(2)]);
        var candidates = await candidateStore.GetPendingAsync(10);
        Assert.Single(candidates);

        var reducers = new EventReducerRegistry()
            .Register<CounterState, CounterIncremented>((state, @event) => state with
            {
                Value = state.Value + @event.Amount
            });
        var processor = new SnapshotProcessor<CounterState>(
            store,
            eventSerializer,
            registry,
            reducers,
            snapshotStore,
            snapshotSerializer,
            candidateStore,
            new EventSourcingOptions { RehydrationBatchSize = 1 });

        var results = await processor.ProcessPendingAsync(CounterState.Empty, 10);
        var snapshot = await snapshotStore.LoadLatestAsync("counters", "counter-1");

        Assert.Single(results);
        Assert.True(results.Single().SnapshotSaved);
        Assert.Equal(2, snapshot!.StreamVersion);
        Assert.Equal(new CounterState(3), snapshotSerializer.Deserialize(snapshot.Payload, typeof(CounterState)));
        Assert.Empty(await candidateStore.GetPendingAsync(10));
    }

    private sealed record CounterState(int Value)
    {
        public static CounterState Empty { get; } = new(0);
    }

    private sealed record CounterIncremented(int Amount);

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
