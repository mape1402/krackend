namespace Krackend.Sagas.Orchestrations.Runtime.Execution;

internal sealed class ExecutionSandboxProviderRegistry : IExecutionSandboxProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IExecutionSandboxProvider> _providers;

    public ExecutionSandboxProviderRegistry(IEnumerable<IExecutionSandboxProvider> providers)
    {
        _providers = (providers ?? Enumerable.Empty<IExecutionSandboxProvider>())
            .Where(provider => !string.IsNullOrWhiteSpace(provider.ProviderKey))
            .GroupBy(provider => provider.ProviderKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string providerKey, out IExecutionSandboxProvider provider)
        => _providers.TryGetValue(providerKey ?? string.Empty, out provider);
}

