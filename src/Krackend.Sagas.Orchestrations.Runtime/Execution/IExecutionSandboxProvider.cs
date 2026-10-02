namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

/// <summary>
/// Executes a prepared runtime command through a runtime-selected execution provider.
/// </summary>
public interface IExecutionSandboxProvider
{
    /// <summary>
    /// Gets the stable provider key used by execution policies.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the execution mode implemented by the provider.
    /// </summary>
    string ExecutionMode { get; }

    /// <summary>
    /// Gets a value indicating whether the provider runs work in an isolated sandbox.
    /// </summary>
    bool IsSandbox { get; }

    /// <summary>
    /// Dispatches the prepared execution envelope.
    /// </summary>
    /// <param name="envelope">Execution envelope prepared by the runtime engine.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when dispatch has been accepted by the provider.</returns>
    Task DispatchAsync(ExecutionEnvelope envelope, CancellationToken cancellationToken = default);
}
