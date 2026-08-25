namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>
/// Tracks ingress generations applied in the current runtime process.
/// </summary>
public interface IRuntimeIngressLocalState
{
    /// <summary>
    /// Determines whether a generation was applied locally.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="ingressGeneration">Ingress generation.</param>
    /// <returns>True when the generation is already applied locally.</returns>
    bool IsApplied(string artifactId, long ingressGeneration);

    /// <summary>
    /// Marks a generation as applied locally.
    /// </summary>
    /// <param name="artifactId">Runtime artifact id.</param>
    /// <param name="ingressGeneration">Ingress generation.</param>
    void MarkApplied(string artifactId, long ingressGeneration);
}
