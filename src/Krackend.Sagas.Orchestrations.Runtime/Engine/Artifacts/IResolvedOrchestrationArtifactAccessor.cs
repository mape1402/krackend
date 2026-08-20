namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts
{
    internal interface IResolvedOrchestrationArtifactAccessor
    {
        ResolvedOrchestrationArtifact Get();

        void Set(ResolvedOrchestrationArtifact artifact);
    }
}
