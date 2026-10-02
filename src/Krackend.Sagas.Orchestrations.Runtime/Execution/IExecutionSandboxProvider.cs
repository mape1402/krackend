namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

internal interface IExecutionSandboxProvider
{
    string ProviderKey { get; }

    string ExecutionMode { get; }

    bool IsSandbox { get; }

    Task DispatchAsync(ExecutionEnvelope envelope, CancellationToken cancellationToken = default);
}

