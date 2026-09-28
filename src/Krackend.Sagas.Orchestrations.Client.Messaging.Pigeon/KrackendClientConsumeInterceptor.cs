namespace Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using global::Pigeon.Messaging.Consuming.Dispatching;

internal sealed class KrackendClientConsumeInterceptor : IConsumeInterceptor, IConsumeExecutionInterceptor
{
    private readonly IOrchestrationMessageMetadataSetter _metadataSetter;
    private readonly IOrchestrationExecutionResultMetadataSetter _resultMetadataSetter;
    private readonly IOrchestrationPropagationMetadataSetter _propagationMetadataSetter;
    private readonly PigeonPropagationMetadataMapper _propagationMetadataMapper = new();

    public KrackendClientConsumeInterceptor(
        IOrchestrationMessageMetadataSetter metadataSetter,
        IOrchestrationExecutionResultMetadataSetter resultMetadataSetter)
        : this(metadataSetter, resultMetadataSetter, null)
    {
    }

    public KrackendClientConsumeInterceptor(
        IOrchestrationMessageMetadataSetter metadataSetter,
        IOrchestrationExecutionResultMetadataSetter resultMetadataSetter,
        IOrchestrationPropagationMetadataSetter propagationMetadataSetter)
    {
        _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
        _resultMetadataSetter = resultMetadataSetter ?? throw new ArgumentNullException(nameof(resultMetadataSetter));
        _propagationMetadataSetter = propagationMetadataSetter;
    }

    public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
    {
        CaptureMetadata(context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask InvokeAsync(
        ConsumeContext context,
        ConsumeExecutionDelegate next,
        CancellationToken cancellationToken = default)
    {
        if (next is null)
        {
            throw new ArgumentNullException(nameof(next));
        }

        CaptureMetadata(context);
        await next(context, cancellationToken).ConfigureAwait(false);
    }

    private void CaptureMetadata(ConsumeContext context)
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

        _propagationMetadataSetter?.Set(_propagationMetadataMapper.Capture(context));
    }
}
