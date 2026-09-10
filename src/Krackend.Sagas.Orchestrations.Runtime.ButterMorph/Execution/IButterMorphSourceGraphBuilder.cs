namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using global::ButterMorph.Abstractions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Builds ButterMorph source graphs from an orchestration payload context.
/// </summary>
public interface IButterMorphSourceGraphBuilder
{
    /// <summary>
    /// Builds the source graphs available to a transformation.
    /// </summary>
    /// <param name="payloadContext">The accumulated orchestration payload context.</param>
    /// <returns>Source graphs keyed by ButterMorph source alias.</returns>
    IReadOnlyDictionary<string, IStructureGraph> Build(OrchestrationPayloadContext payloadContext);
}
