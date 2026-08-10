using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Handles orchestration back-channel responses and resumes waiting runtime instances.
/// </summary>
public sealed class RuntimeBackChannelResponseHandler : IRuntimeBackChannelResponseHandler
{
    private readonly IRuntimeEngine _runtimeEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeBackChannelResponseHandler"/> class.
    /// </summary>
    /// <param name="runtimeEngine">Runtime engine used to resume waiting instances.</param>
    public RuntimeBackChannelResponseHandler(IRuntimeEngine runtimeEngine)
    {
        _runtimeEngine = runtimeEngine ?? throw new ArgumentNullException(nameof(runtimeEngine));
    }

    /// <inheritdoc/>
    public async Task Handle(MessageConsumeContext context, CancellationToken cancellationToken = default)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (context.Metadata is null)
            throw new InvalidOperationException("Back-channel response does not include orchestrator metadata.");

        await _runtimeEngine.ContinueFromResponse(CreateCommand(context), cancellationToken);
    }

    private static RuntimeMessageResponseCommand CreateCommand(MessageConsumeContext context)
    {
        return new RuntimeMessageResponseCommand
        {
            OrchestrationInstanceId = context.Metadata.OrchestrationInstanceId,
            TaskExecutionId = context.Metadata.TaskExecutionId,
            DispatchId = context.Metadata.DispatchId,
            CorrelationId = context.Metadata.CorrelationId,
            Payload = context.Message
        };
    }
}
