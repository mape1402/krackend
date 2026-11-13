namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using System.Text.Json;
using global::Mule;

internal sealed class MuleOrchestrationDurableWorkInspector(IMuleStorage storage, IOrchestrationDurableActionStateReader reader) : IOrchestrationDurableWorkInspector
{
    public async Task<DurableWorkPresence> InspectPendingResponseAsync(TaskDispatch dispatch, TaskExecutionAttempt attempt,
        DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        // A callback without an explicit attempt is also accepted by the engine for the current dispatch.
        var result = DurableWorkPresence.Missing;
        foreach (var number in attempt.AttemptNumber == 0 ? new[] { 0 } : new[] { attempt.AttemptNumber, 0 })
        {
            var id = await storage.FindByDeduplicationKeyAsync(MuleActionKeys.BackchannelActionKey,
                $"backchannel:{dispatch.Id}:{number}", cancellationToken);
            if (id is null) continue;
            var state = await reader.ReadPendingBeforeAsync(id.Value.ToString(), deadlineUtc, cancellationToken);
            // Fast-lane work can be visible in the buffer before its durable-store flush.
            if (state == DurableWorkPresence.Present ||
                (state == DurableWorkPresence.Unknown && System.Ulid.TryParse(id.Value.ToString(), out var parsed) &&
                 parsed.Time.UtcDateTime <= DateTime.SpecifyKind(deadlineUtc, DateTimeKind.Utc)))
                return DurableWorkPresence.Present;
            result = state;
        }
        return result;
    }

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
