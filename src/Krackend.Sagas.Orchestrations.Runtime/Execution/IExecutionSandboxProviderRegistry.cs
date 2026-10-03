namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

internal interface IExecutionSandboxProviderRegistry
{
    bool TryGet(string providerKey, out IExecutionSandboxProvider provider);
}

