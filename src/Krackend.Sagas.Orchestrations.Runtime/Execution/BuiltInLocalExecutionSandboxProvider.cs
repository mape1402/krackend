namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

internal sealed class BuiltInLocalExecutionSandboxProvider : IExecutionSandboxProvider
{
    private readonly IRemoteCommandDispatcher _dispatcher;

    public BuiltInLocalExecutionSandboxProvider(IRemoteCommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public string ProviderKey => ExecutionConstants.BuiltInLocalProvider;

    public string ExecutionMode => ExecutionConstants.InProcessTrustedMode;

    public bool IsSandbox => false;

    public Task DispatchAsync(ExecutionEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return _dispatcher.DispatchAsync(envelope.Command, cancellationToken);
    }
}

