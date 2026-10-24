namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

/// <summary>
/// Enables an execution provider to recover a persisted durable delivery intent after a runtime interruption.
/// </summary>
public interface IRecoverableExecutionSandboxProvider
{
    /// <summary>
    /// Re-submits the command represented by a durable dispatch intent.
    /// </summary>
    /// <param name="request">Persisted dispatch information and its prepared command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider acceptance result.</returns>
    Task<ExecutionDispatchRecoveryResult> RecoverDispatchAsync(
        ExecutionDispatchRecoveryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a durable command delivery that must be recovered.
/// </summary>
public sealed class ExecutionDispatchRecoveryRequest
{
    /// <summary>
    /// Gets the persisted dispatch intent.
    /// </summary>
    public required TaskDispatch Dispatch { get; init; }

    /// <summary>
    /// Gets the original fully prepared remote command.
    /// </summary>
    public required RemoteCommand Command { get; init; }
}

/// <summary>
/// Represents the provider result of recovering a durable command delivery.
/// </summary>
public sealed record ExecutionDispatchRecoveryResult(bool Succeeded, string Status, string Message)
{
    /// <summary>
    /// Creates a successful recovery result.
    /// </summary>
    public static ExecutionDispatchRecoveryResult Accepted(string status, string message)
        => new(true, status, message);

    /// <summary>
    /// Creates a rejected recovery result.
    /// </summary>
    public static ExecutionDispatchRecoveryResult Rejected(string status, string message)
        => new(false, status, message);
}
