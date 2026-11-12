using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Reads a bounded, display-safe summary of recorded stage executions.</summary>
public interface IRuntimeStageSummaryReader
{
    Task<RuntimeStageSummaryPage> GetStageSummaries(Id instanceId, int take = 20, CancellationToken cancellationToken = default);
}

/// <summary>A recorded stage without payload or metadata.</summary>
public sealed record RuntimeStageSummary(string StageKey, int Order, string Status, bool WasSkipped,
    DateTime? StartedOnUtc, DateTime? CompletedOnUtc, DateTime? FailedOnUtc);

/// <summary>The bounded stage summary and whether additional stages exist.</summary>
public sealed record RuntimeStageSummaryPage(IReadOnlyCollection<RuntimeStageSummary> Items, bool HasMore);
