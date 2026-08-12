namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime task dispatcher for messaging tasks.
/// </summary>
public sealed class MessagingRuntimeTaskDispatcher : IRuntimeTaskDispatcher
{
    private readonly IMessagingCommandDispatcher _messagingDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRuntimeTaskDispatcher"/> class.
    /// </summary>
    /// <param name="messagingDispatcher">Messaging command dispatcher.</param>
    public MessagingRuntimeTaskDispatcher(IMessagingCommandDispatcher messagingDispatcher)
    {
        _messagingDispatcher = messagingDispatcher ?? throw new ArgumentNullException(nameof(messagingDispatcher));
    }

    /// <inheritdoc/>
    public bool CanDispatch(string taskKind)
        => string.Equals(taskKind, "Messaging", StringComparison.OrdinalIgnoreCase)
            || string.Equals(taskKind, "0", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RuntimeTaskDispatchResult> Dispatch(
        RuntimeTaskDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _messagingDispatcher.Dispatch(CreateCommand(request), cancellationToken);
        return new RuntimeTaskDispatchResult
        {
            Succeeded = result.Succeeded,
            Status = result.Status,
            ExternalReference = result.ExternalReference,
            FailureReason = result.FailureReason
        };
    }

    private static MessagingDispatchCommand CreateCommand(RuntimeTaskDispatchRequest request)
    {
        return new MessagingDispatchCommand
        {
            CommandId = request.CommandId,
            CorrelationId = request.CorrelationId,
            Destination = request.Destination,
            MessageVersion = request.MessageVersion,
            Payload = request.Payload.DeepClone(),
            OrchestrationDefinitionKey = request.OrchestrationDefinitionKey,
            OrchestrationVersion = request.OrchestrationVersion,
            OrchestrationInstanceId = request.OrchestrationInstanceId,
            TaskExecutionId = request.TaskExecutionId,
            DispatchId = request.DispatchId,
            EnvironmentKey = request.EnvironmentKey,
            StageKey = request.StageKey,
            TaskKey = request.TaskKey,
            CurrentStatus = request.CurrentStatus,
            Attempt = request.Attempt,
            StartedOnUtc = request.StartedOnUtc,
            UpdatedOnUtc = request.UpdatedOnUtc
        };
    }
}
