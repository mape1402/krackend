using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
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
    private readonly ITaskDispatchRepository _dispatchRepository;
    private readonly ITaskExecutionRepository _taskRepository;
    private readonly ITaskExecutionAttemptRepository _attemptRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeBackChannelResponseHandler"/> class.
    /// </summary>
    public RuntimeBackChannelResponseHandler(
        IRuntimeDurableWorkScheduler durableWorkScheduler,
        ITaskDispatchRepository dispatchRepository,
        ITaskExecutionRepository taskRepository,
        ITaskExecutionAttemptRepository attemptRepository)
    {
        _durableWorkScheduler = durableWorkScheduler ?? throw new ArgumentNullException(nameof(durableWorkScheduler));
        _dispatchRepository = dispatchRepository ?? throw new ArgumentNullException(nameof(dispatchRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
    }

    /// <inheritdoc/>
    public async Task Handle(MessageConsumeContext context, CancellationToken cancellationToken = default)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (context.Metadata is null)
            throw new InvalidOperationException("Back-channel response does not include orchestrator metadata.");

        ValidateRequiredMetadata(context);
        await ValidateTrace(context, cancellationToken);

        await _durableWorkScheduler.ScheduleProcessIngress(CreateEnvelope(context), cancellationToken);
    }

    private async Task ValidateTrace(MessageConsumeContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Metadata;
        var instanceId = ParseId(metadata.OrchestrationInstanceId, "orchestration instance id");
        var taskExecutionId = ParseId(metadata.TaskExecutionId, "task execution id");
        var dispatchId = ParseId(metadata.DispatchId, "dispatch id");

        var taskExecution = await _taskRepository.GetById(taskExecutionId, cancellationToken);
        if (taskExecution.OrchestrationInstanceId != instanceId)
            throw new InvalidOperationException($"Back-channel response task execution '{taskExecution.Id}' does not belong to orchestration instance '{instanceId}'.");

        var dispatch = await _dispatchRepository.TryGetById(dispatchId, cancellationToken);
        if (dispatch is null)
            throw new InvalidOperationException($"Back-channel response dispatch '{dispatchId}' was not found.");

        var attempt = await _attemptRepository.GetByDispatchId(dispatchId, cancellationToken);
        if (attempt is null)
            throw new InvalidOperationException($"Back-channel response dispatch '{dispatchId}' is not linked to a task attempt.");

        if (attempt.TaskExecutionId != taskExecution.Id)
            throw new InvalidOperationException($"Back-channel response attempt '{attempt.Id}' does not belong to task execution '{taskExecution.Id}'.");

        if (attempt.DispatchId != dispatch.Id)
            throw new InvalidOperationException($"Back-channel response attempt '{attempt.Id}' is not correlated with dispatch '{dispatch.Id}'.");

        if (dispatch.TaskExecutionAttemptId != attempt.Id)
            throw new InvalidOperationException($"Back-channel response dispatch '{dispatch.Id}' does not belong to attempt '{attempt.Id}'.");

        if (!string.Equals(taskExecution.CorrelationId, metadata.CorrelationId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Back-channel response correlation '{metadata.CorrelationId}' does not match task correlation '{taskExecution.CorrelationId}'.");

        if (!string.Equals(dispatch.CorrelationId, metadata.CorrelationId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Back-channel response correlation '{metadata.CorrelationId}' does not match dispatch correlation '{dispatch.CorrelationId}'.");
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

    private static Id ParseId(string value, string name)
    {
        if (!Ulid.TryParse(value, out var ulid))
            throw new InvalidOperationException($"Back-channel response metadata has invalid {name}.");

        return new Id(ulid);
    }
}
