namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using System.Text.Json;
using global::Mule;

internal sealed class MuleOrchestrationDurableWorkInspector(IMuleStorage storage, IOrchestrationDurableActionStateReader reader) : IOrchestrationDurableWorkInspector
{
    public async Task<DurableWorkPresence> InspectDispatchAsync(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        var id = await storage.FindByDeduplicationKeyAsync(MuleActionKeys.RemoteCommandDispatchActionKey, dispatch.Id.ToString(), cancellationToken);
        return id is null ? DurableWorkPresence.Missing : await ReadKnownAsync(id.Value.ToString(), cancellationToken);
    }

    public async Task<DurableWorkPresence> InspectTimeoutAsync(TaskExecution task, TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (task.ResponseDeadlineUtc is null) return DurableWorkPresence.Unknown;
        if (!string.IsNullOrWhiteSpace(task.TimeoutActionId))
        {
            // The referenced action may be buffered outside durable storage. Falling back to the
            // original deduplication key could find an older, terminal timer and duplicate it.
            return await reader.ReadAsync(task.TimeoutActionId, cancellationToken);
        }
        var id = await FindTimeoutActionIdAsync(task, attempt, cancellationToken);
        return id is null ? DurableWorkPresence.Missing : await ReadKnownAsync(id, cancellationToken);
    }

    public async Task<string?> FindTimeoutActionIdAsync(TaskExecution task, TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (task.ResponseDeadlineUtc is not { } deadline) return null;
        var key = string.Join(':', "task-timeout", task.Id, attempt.Id, attempt.AttemptNumber, deadline.Ticks);
        var id = await storage.FindByDeduplicationKeyAsync(MuleActionKeys.TaskResponseTimeoutActionKey, key, cancellationToken);
        return id?.ToString();
    }

    private async Task<DurableWorkPresence> ReadKnownAsync(string actionId, CancellationToken cancellationToken)
    {
        var state = await reader.ReadAsync(actionId, cancellationToken);
        // A deduplication record may represent a completed action; presence alone does not prove liveness.
        return state == DurableWorkPresence.Unknown ? DurableWorkPresence.Unknown : state;
    }

    public async Task<DurableWorkPresence> InspectCompensationAsync(CompensationExecution compensation, CancellationToken cancellationToken = default)
    {
        if (!compensation.Metadata.TryGetValue("Runtime.Compensation.Delivery.Command", out var node) || node is null)
            return DurableWorkPresence.Unknown;
        RemoteCommand? command;
        try { command = node.Deserialize<RemoteCommand>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { return DurableWorkPresence.Unknown; }
        if (command is null) return DurableWorkPresence.Unknown;
        var key = MuleRemoteCommandDispatcher.BuildDeduplicationKey(command);
        if (string.IsNullOrWhiteSpace(key)) return DurableWorkPresence.Unknown;
        var id = await storage.FindByDeduplicationKeyAsync(MuleActionKeys.RemoteCommandDispatchActionKey, key, cancellationToken);
        return id is null ? DurableWorkPresence.Missing : await ReadKnownAsync(id.Value.ToString(), cancellationToken);
    }
}
