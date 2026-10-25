using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Provides durable physical-cancellation work that still needs to reach a delivery provider.
/// </summary>
public interface ITaskDispatchCancellationRepository
{
    /// <summary>
    /// Gets dispatches whose logical cancellation was persisted but whose physical cancellation has not reached a terminal provider outcome.
    /// </summary>
    /// <param name="take">Maximum number of dispatches to return.</param>
    /// <param name="skip">Number of eligible dispatches to skip.</param>
    /// <param name="attemptedBeforeUtc">Latest previous attempt eligible for retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending physical cancellation requests.</returns>
    Task<IReadOnlyCollection<TaskDispatch>> GetPhysicalCancellationPending(
        int take,
        int skip,
        DateTime attemptedBeforeUtc,
        CancellationToken cancellationToken = default);
}
