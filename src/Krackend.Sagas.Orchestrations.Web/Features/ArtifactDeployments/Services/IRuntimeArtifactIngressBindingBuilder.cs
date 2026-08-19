using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Web;

/// <summary>
/// Builds runtime ingress bindings from deployed orchestration artifacts.
/// </summary>
public interface IRuntimeArtifactIngressBindingBuilder
{
    /// <summary>
    /// Builds ingress bindings.
    /// </summary>
    /// <param name="artifact">Runtime artifact.</param>
    /// <returns>Runtime ingress binding set.</returns>
    RuntimeArtifactIngressBindingSet Build(RuntimeOrchestrationArtifact artifact);
}
