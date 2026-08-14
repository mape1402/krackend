using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Handles orchestration back-channel responses and resumes waiting runtime instances.
/// </summary>
public sealed class RuntimeBackChannelResponseHandler : IRuntimeBackChannelResponseHandler
{
    private readonly IRuntimeDurableWorkScheduler _durableWorkScheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeBackChannelResponseHandler"/> class.
    /// </summary>
    /// <param name="durableWorkScheduler">Runtime durable work scheduler.</param>
    public RuntimeBackChannelResponseHandler(IRuntimeDurableWorkScheduler durableWorkScheduler)
    {
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
    }

    /// <inheritdoc/>
    public async Task Handle(MessageConsumeContext context, CancellationToken cancellationToken = default)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (context.Metadata is null)
            throw new InvalidOperationException("Back-channel response does not include orchestrator metadata.");

        ValidateRequiredMetadata(context);

        await _durableWorkScheduler.ScheduleProcessIngress(CreateEnvelope(context), cancellationToken);
    }

    private static RuntimeIngressEnvelope CreateEnvelope(MessageConsumeContext context)
    {
        var metadata = context.Metadata;

        return new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.TaskResponse,
            EnvironmentKey = metadata.Environment,
            OrchestrationName = metadata.OrchestrationKey,
            OrchestrationVersion = metadata.OrchestrationVersion,
            CorrelationId = metadata.CorrelationId,
            SagaId = metadata.SagaId,
            OrchestrationInstanceId = metadata.OrchestrationInstanceId,
            StageKey = metadata.CurrentState?.CurrentStageKey,
            TaskKey = metadata.CurrentState?.CurrentTaskKey,
            TaskExecutionId = metadata.TaskExecutionId,
            DispatchId = metadata.DispatchId,
            Attempt = metadata.CurrentState?.Attempt ?? 0,
            Payload = context.Message,
            ReceivedOnUtc = context.CreatedOnUtc.UtcDateTime,
            Source = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = context.Topic,
                Version = context.Version,
                MessageId = metadata.DispatchId
            }
        };
    }

    private static void ValidateRequiredMetadata(MessageConsumeContext context)
    {
        var metadata = context.Metadata;

        if (string.IsNullOrWhiteSpace(metadata.Environment))
            throw new InvalidOperationException("Back-channel response metadata must include environment.");

        if (string.IsNullOrWhiteSpace(metadata.OrchestrationKey))
            throw new InvalidOperationException("Back-channel response metadata must include orchestration key.");

        if (string.IsNullOrWhiteSpace(metadata.OrchestrationVersion))
            throw new InvalidOperationException("Back-channel response metadata must include orchestration version.");

        if (string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId))
            throw new InvalidOperationException("Back-channel response metadata must include orchestration instance id.");

        if (string.IsNullOrWhiteSpace(metadata.DispatchId))
            throw new InvalidOperationException("Back-channel response metadata must include dispatch id.");

        if (string.IsNullOrWhiteSpace(metadata.TaskExecutionId))
            throw new InvalidOperationException("Back-channel response metadata must include task execution id.");

        if (string.IsNullOrWhiteSpace(metadata.CorrelationId))
            throw new InvalidOperationException("Back-channel response metadata must include correlation id.");
    }
}
