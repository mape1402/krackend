namespace Krackend.Sagas.Orchestrations.Runtime.Engine
{
    public interface ISagaEngine
    {
        Task StartOrchestrationAsync(StartIntent intent, CancellationToken cancellationToken = default);

        Task OrchestrateAsync(ForwardIntent intent, CancellationToken cancellationToken = default);
    }
}
