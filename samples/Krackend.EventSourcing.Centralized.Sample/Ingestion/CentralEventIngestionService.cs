using Krackend.EventSourcing.Centralized.Sample.Requests;
using Krackend.EventSourcing.Envelopes;
using Krackend.EventSourcing.Stores;

namespace Krackend.EventSourcing.Centralized.Sample.Ingestion;

public sealed class CentralEventIngestionService
{
    private const string CentralStreamName = "integration-events";

    private readonly IRawEventStore _eventStore;

    public CentralEventIngestionService(IRawEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public Task<IReadOnlyCollection<EventEnvelope>> AppendAsync(
        IncomingIntegrationEvent request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var streamId = $"{request.BoundedContext}:{request.AggregateType}:{request.AggregateId}";
        var expectedVersion = request.ExpectedStreamVersion is null
            ? ExpectedVersion.Any
            : ExpectedVersion.Exact(request.ExpectedStreamVersion.Value);

        return _eventStore.AppendRawAsync(
            CentralStreamName,
            streamId,
            expectedVersion,
            [
                new RawEventData(
                    request.EventType,
                    request.EventSchemaVersion,
                    request.Payload,
                    request.Metadata)
            ],
            cancellationToken);
    }
}
