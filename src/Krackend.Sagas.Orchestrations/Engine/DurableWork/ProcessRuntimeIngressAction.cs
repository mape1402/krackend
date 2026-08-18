namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Mule;

/// <summary>
/// Mule action that processes canonical runtime ingress outside the transport handler.
/// </summary>
[MuleAction("krackend.runtime.process-ingress")]
public sealed class ProcessRuntimeIngressAction : IMuleAction<RuntimeIngressEnvelope>
{
    private readonly IRuntimeEngine _runtimeEngine;
    private readonly IReadOnlyCollection<IRuntimeStorageUnitOfWork> _unitOfWorks;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessRuntimeIngressAction"/> class.
    /// </summary>
    public ProcessRuntimeIngressAction(
        IRuntimeEngine runtimeEngine,
        IEnumerable<IRuntimeStorageUnitOfWork> unitOfWorks = null)
    {
        _runtimeEngine = runtimeEngine ?? throw new ArgumentNullException(nameof(runtimeEngine));
        _unitOfWorks = unitOfWorks?.ToArray() ?? [];
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(MuleActionContext<RuntimeIngressEnvelope> context, CancellationToken cancellationToken)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        switch (context.Payload.Kind)
        {
            case RuntimeIngressKind.Trigger:
                await ProcessTrigger(context.Payload, cancellationToken);
                await SaveRuntimeChanges(cancellationToken);
                break;
            case RuntimeIngressKind.TaskResponse:
                await ProcessTaskResponse(context.Payload, cancellationToken);
                await SaveRuntimeChanges(cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Runtime ingress kind '{context.Payload.Kind}' is not supported.");
        }
    }

    private async Task ProcessTrigger(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken)
    {
        var item = new TriggerIntakeBufferItem
        {
            BufferItemId = Id.New(),
            TriggerType = TriggerType.Event,
            TriggerKey = envelope.OrchestrationName,
            ArtifactVersion = envelope.OrchestrationVersion,
            EnvironmentKey = envelope.EnvironmentKey,
            CorrelationId = envelope.CorrelationId,
            IdempotencyKey = RuntimeIngressIdempotency.Build(envelope),
            SourceMessageId = envelope.Source?.MessageId,
            PayloadJson = (envelope.Payload ?? new JsonObject()).ToJsonString(),
            ReceivedOnUtc = envelope.ReceivedOnUtc == default ? DateTime.UtcNow : envelope.ReceivedOnUtc
        };

        var result = await _runtimeEngine.Process(item, cancellationToken);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Message);
    }

    private async Task ProcessTaskResponse(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken)
    {
        ValidateTaskResponse(envelope);

        await _runtimeEngine.ContinueFromResponse(new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = envelope.OrchestrationInstanceId,
            TaskExecutionId = envelope.TaskExecutionId,
            DispatchId = envelope.DispatchId,
            CorrelationId = envelope.CorrelationId,
            Payload = envelope.Payload
        }, cancellationToken);
    }

    private async Task SaveRuntimeChanges(CancellationToken cancellationToken)
    {
        foreach (var unitOfWork in _unitOfWorks)
            await unitOfWork.SaveChanges(cancellationToken);
    }

    private static void ValidateTaskResponse(RuntimeIngressEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.OrchestrationInstanceId))
            throw new InvalidOperationException("Runtime task response ingress must include orchestration instance id.");

        if (string.IsNullOrWhiteSpace(envelope.DispatchId))
            throw new InvalidOperationException("Runtime task response ingress must include dispatch id.");

        if (string.IsNullOrWhiteSpace(envelope.TaskExecutionId))
            throw new InvalidOperationException("Runtime task response ingress must include task execution id.");

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId))
            throw new InvalidOperationException("Runtime task response ingress must include correlation id.");
    }
}
