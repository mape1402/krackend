namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Mule;

/// <summary>
/// Schedules runtime inbox/outbox work through Mule Durable Actions.
/// </summary>
public sealed class MuleRuntimeDurableWorkScheduler : IRuntimeDurableWorkScheduler
{
    private readonly IMuleClient _muleClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MuleRuntimeDurableWorkScheduler"/> class.
    /// </summary>
    public MuleRuntimeDurableWorkScheduler(IMuleClient muleClient)
    {
        _muleClient = muleClient ?? throw new ArgumentNullException(nameof(muleClient));
    }

    /// <inheritdoc />
    public async ValueTask<Guid> ScheduleProcessIngress(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        return await _muleClient.EnqueueAsync(
            RuntimeDurableWorkActionKeys.ProcessIngress,
            envelope,
            options =>
            {
                options.CorrelationId = envelope.CorrelationId;
                options.DeduplicationKey = RuntimeIngressIdempotency.Build(envelope);
                AddIngressMetadata(options.Metadata, envelope);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Guid> ScheduleDispatchTask(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        return await _muleClient.EnqueueAsync(
            RuntimeDurableWorkActionKeys.DispatchTask,
            envelope,
            options =>
            {
                options.CorrelationId = envelope.CorrelationId;
                options.DeduplicationKey = RuntimeDispatchIdempotency.Build(envelope);
                AddDispatchMetadata(options.Metadata, envelope);
            },
            cancellationToken);
    }

    private static void AddIngressMetadata(IDictionary<string, string> metadata, RuntimeIngressEnvelope envelope)
    {
        Add(metadata, "runtime.action", RuntimeDurableWorkActionKeys.ProcessIngress.Value);
        Add(metadata, "runtime.ingress.kind", envelope.Kind.ToString());
        Add(metadata, "runtime.environment", envelope.EnvironmentKey);
        Add(metadata, "runtime.orchestration", envelope.OrchestrationName);
        Add(metadata, "runtime.version", envelope.OrchestrationVersion);
        Add(metadata, "runtime.correlationId", envelope.CorrelationId);
        Add(metadata, "runtime.sagaId", envelope.SagaId);
        Add(metadata, "runtime.instanceId", envelope.OrchestrationInstanceId);
        Add(metadata, "runtime.dispatchId", envelope.DispatchId);
        Add(metadata, "runtime.taskExecutionId", envelope.TaskExecutionId);
        Add(metadata, "runtime.transport", envelope.Source?.Kind.ToString());
        Add(metadata, "runtime.transport.address", envelope.Source?.Address);
        Add(metadata, "runtime.transport.messageId", envelope.Source?.MessageId);
    }

    private static void AddDispatchMetadata(IDictionary<string, string> metadata, RuntimeDispatchEnvelope envelope)
    {
        Add(metadata, "runtime.action", RuntimeDurableWorkActionKeys.DispatchTask.Value);
        Add(metadata, "runtime.orchestration", envelope.OrchestrationName);
        Add(metadata, "runtime.version", envelope.OrchestrationVersion);
        Add(metadata, "runtime.correlationId", envelope.CorrelationId);
        Add(metadata, "runtime.sagaId", envelope.SagaId);
        Add(metadata, "runtime.instanceId", envelope.OrchestrationInstanceId);
        Add(metadata, "runtime.dispatchId", envelope.DispatchId);
        Add(metadata, "runtime.taskExecutionId", envelope.TaskExecutionId);
        Add(metadata, "runtime.stage", envelope.StageKey);
        Add(metadata, "runtime.task", envelope.TaskKey);
        Add(metadata, "runtime.transport", envelope.Destination?.Kind.ToString());
        Add(metadata, "runtime.transport.address", envelope.Destination?.Address);
        Add(metadata, "runtime.transport.version", envelope.Destination?.Version);
    }

    private static void Add(IDictionary<string, string> metadata, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            metadata[key] = value;
    }
}
