namespace Krackend.Sagas.Orchestrations.Tests.Client.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Publishing;

internal sealed class RecordingOrchestrationClientPublisher : IOrchestrationClientPublisher
{
    private readonly IOrchestrationExecutionResultMetadataAccessor _metadataAccessor;

    public RecordingOrchestrationClientPublisher(IOrchestrationExecutionResultMetadataAccessor metadataAccessor)
    {
        _metadataAccessor = metadataAccessor ?? throw new ArgumentNullException(nameof(metadataAccessor));
    }

    public object? Payload { get; private set; }

    public OrchestrationReplyAddress? Address { get; private set; }

    public OrchestrationExecutionResultMetadata? ResultMetadata { get; private set; }

    public int PublishCount { get; private set; }

    public Task PublishAsync(
        object? payload,
        OrchestrationReplyAddress address,
        CancellationToken cancellationToken = default)
    {
        PublishCount++;
        Payload = payload;
        Address = address;
        ResultMetadata = _metadataAccessor.Get();
        return Task.CompletedTask;
    }
}
