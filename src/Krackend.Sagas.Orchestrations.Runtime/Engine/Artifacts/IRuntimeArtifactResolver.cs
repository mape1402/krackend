namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal interface IRuntimeArtifactResolver
    {
        Task<ResolvedOrchestrationArtifact> ResolveAsync(string artifactId, CancellationToken cancellationToken = default);
    }
}
