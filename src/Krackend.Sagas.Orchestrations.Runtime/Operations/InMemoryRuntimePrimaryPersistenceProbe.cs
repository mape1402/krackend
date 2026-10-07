namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Default primary persistence probe used by the in-memory runtime storage adapter.
/// </summary>
internal sealed class InMemoryRuntimePrimaryPersistenceProbe : IRuntimeDependencyProbe
{
    public string Name => "primary-persistence";

    public RuntimeDependencyKind Kind => RuntimeDependencyKind.Critical;

    public ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new RuntimeDependencyProbeResult(Name, Kind, true));
}
