namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Consuming.Dispatching;

internal sealed class KrackendClientConsumeInterceptor : IConsumeInterceptor
{
    private readonly IInstanceMetadataSetter _metadataSetter;

    public KrackendClientConsumeInterceptor(IInstanceMetadataSetter metadataSetter)
    {
        _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
    }

    public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = context.GetMetadata<InstanceMetadata>(OrchestrationMetadataConstants.InstanceMetadataKey);
            _metadataSetter.Set(metadata);
        }
        catch
        {
            _metadataSetter.Set(new InstanceMetadata());
        }

        return ValueTask.CompletedTask;
    }
}
