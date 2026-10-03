namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

/// <summary>
/// Migrates orchestration artifacts from older payload shapes to the runtime-supported schema.
/// </summary>
public interface IOrchestrationArtifactMigrator
{
    /// <summary>
    /// Migrates an orchestration artifact to the current runtime schema.
    /// </summary>
    /// <param name="artifact">Artifact to migrate.</param>
    /// <returns>Migrated artifact.</returns>
    OrchestrationArtifact Migrate(OrchestrationArtifact artifact);
}
