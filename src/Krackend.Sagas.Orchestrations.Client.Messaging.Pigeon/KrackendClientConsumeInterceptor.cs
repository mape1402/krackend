namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Consuming.Dispatching;

internal sealed class KrackendClientConsumeInterceptor : IConsumeInterceptor
{
    private readonly IOrchestrationMessageMetadataSetter _metadataSetter;
    private readonly IOrchestrationExecutionResultMetadataSetter _resultMetadataSetter;

    public KrackendClientConsumeInterceptor(
        IOrchestrationMessageMetadataSetter metadataSetter,
        IOrchestrationExecutionResultMetadataSetter resultMetadataSetter)
    {
        _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
        _resultMetadataSetter = resultMetadataSetter ?? throw new ArgumentNullException(nameof(resultMetadataSetter));
    }

    public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
    {
        _resultMetadataSetter.Clear();

        try
        {
            var metadata = context.GetMetadata<OrchestrationMessageMetadata>(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey);
            _metadataSetter.Set(metadata);
        }
        catch
        {
            _metadataSetter.Set(new OrchestrationMessageMetadata());
        }

        return ValueTask.CompletedTask;
    }
}
