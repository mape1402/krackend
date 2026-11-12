using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>Filters a selected instance's recorded stages using its deployed design.</summary>
public interface IRuntimeVisibleStageSummaryReader
{
    /// <summary>Returns the recorded stages visible in the selected instance inspector.</summary>
    Task<RuntimeStageSummaryPage> GetVisibleStageSummaries(Id instanceId, int take, CancellationToken cancellationToken = default);
}
