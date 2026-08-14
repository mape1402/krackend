namespace Krackend.Sagas.Orchestrations.Engine.DurableWork;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Mule;

/// <summary>
/// Mule action that publishes a canonical runtime dispatch through the configured transport adapter.
/// </summary>
[MuleAction("krackend.runtime.dispatch-task")]
public sealed class DispatchRuntimeTaskAction : IMuleAction<RuntimeDispatchEnvelope>
{
    private readonly IMessagingCommandDispatcher _messagingDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchRuntimeTaskAction"/> class.
    /// </summary>
    public DispatchRuntimeTaskAction(IMessagingCommandDispatcher messagingDispatcher)
    {
        _messagingDispatcher = messagingDispatcher ?? throw new ArgumentNullException(nameof(messagingDispatcher));
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(MuleActionContext<RuntimeDispatchEnvelope> context, CancellationToken cancellationToken)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var envelope = context.Payload;

        if (envelope.Destination?.Kind != RuntimeTransportKind.Message)
            throw new InvalidOperationException($"Runtime dispatch transport '{envelope.Destination?.Kind}' is not supported yet.");

        var result = await _messagingDispatcher.Dispatch(CreateCommand(envelope), cancellationToken);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.FailureReason ?? $"Runtime dispatch failed with status '{result.Status}'.");
    }

    private static MessagingDispatchCommand CreateCommand(RuntimeDispatchEnvelope envelope)
        => new()
        {
            CommandId = envelope.ExecutionKey ?? envelope.DispatchId,
            CorrelationId = envelope.CorrelationId,
            Destination = envelope.Destination.Address,
            MessageVersion = string.IsNullOrWhiteSpace(envelope.Destination.Version) ? "1.0.0" : envelope.Destination.Version,
            Payload = envelope.Payload.DeepClone(),
            OrchestrationDefinitionKey = envelope.OrchestrationName,
            OrchestrationVersion = envelope.OrchestrationVersion,
            OrchestrationInstanceId = envelope.OrchestrationInstanceId,
            TaskExecutionId = envelope.TaskExecutionId,
            DispatchId = envelope.DispatchId,
            EnvironmentKey = envelope.EnvironmentKey,
            StageKey = envelope.StageKey,
            TaskKey = envelope.TaskKey,
            CurrentStatus = "Running",
            Attempt = envelope.Attempt,
            StartedOnUtc = envelope.CreatedOnUtc,
            UpdatedOnUtc = DateTime.UtcNow
        };
}
