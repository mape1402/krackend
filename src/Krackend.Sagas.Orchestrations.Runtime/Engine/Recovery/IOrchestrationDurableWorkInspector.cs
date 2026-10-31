#nullable enable
namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>Determines whether durable work is still discoverable by its stable identity.</summary>
public interface IOrchestrationDurableWorkInspector
{
    /// <summary>Checks a task dispatch without claiming or executing the action.</summary>
    Task<DurableWorkPresence> InspectDispatchAsync(TaskDispatch dispatch, CancellationToken cancellationToken = default);

    /// <summary>Checks a response timer without claiming or executing the action.</summary>
    Task<DurableWorkPresence> InspectTimeoutAsync(TaskExecution task, TaskExecutionAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>Finds the original timeout action when its task reference was lost.</summary>
    Task<string?> FindTimeoutActionIdAsync(TaskExecution task, TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <summary>Checks the durable action associated with a compensation delivery.</summary>
    Task<DurableWorkPresence> InspectCompensationAsync(CompensationExecution compensation, CancellationToken cancellationToken = default);
}

/// <summary>Presence does not imply that the provider action is still executable.</summary>
public enum DurableWorkPresence { Unknown, Missing, Present, Terminal }

/// <summary>Reads an action's persisted state when the configured store exposes it.</summary>
public interface IOrchestrationDurableActionStateReader
{
    /// <summary>Returns Unknown for actions absent from or unsupported by the durable store.</summary>
    Task<DurableWorkPresence> ReadAsync(string actionId, CancellationToken cancellationToken = default);
}

internal sealed class UnknownOrchestrationDurableActionStateReader : IOrchestrationDurableActionStateReader
{
    public Task<DurableWorkPresence> ReadAsync(string actionId, CancellationToken cancellationToken = default)
        => Task.FromResult(DurableWorkPresence.Unknown);
}

internal sealed class UnknownOrchestrationDurableWorkInspector : IOrchestrationDurableWorkInspector
{
    public Task<DurableWorkPresence> InspectDispatchAsync(TaskDispatch dispatch, CancellationToken cancellationToken = default)
        => Task.FromResult(DurableWorkPresence.Unknown);
    public Task<DurableWorkPresence> InspectTimeoutAsync(TaskExecution task, TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
        => Task.FromResult(DurableWorkPresence.Unknown);
    public Task<DurableWorkPresence> InspectCompensationAsync(CompensationExecution compensation, CancellationToken cancellationToken = default)
        => Task.FromResult(DurableWorkPresence.Unknown);
}
