namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal sealed class DefaultResolvedOrchestrationArtifactAccessor : IResolvedOrchestrationArtifactAccessor
    {
        private ResolvedOrchestrationArtifact _artifact;

        public ResolvedOrchestrationArtifact Get()
            => _artifact;

        public void Set(ResolvedOrchestrationArtifact artifact)
            => _artifact = artifact;
    }
}
