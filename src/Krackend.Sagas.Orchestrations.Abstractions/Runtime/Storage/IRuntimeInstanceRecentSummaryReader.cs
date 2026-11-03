namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Optionally reads recent instance summaries without loading large payloads.</summary>
public interface IRuntimeInstanceRecentSummaryReader
{
    /// <summary>Returns detached read-only instances in the same order as GetRecent.</summary>
    Task<IReadOnlyCollection<OrchestrationInstance>> GetRecentSummaries(int take, CancellationToken cancellationToken = default);
}
