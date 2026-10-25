using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Queries instances whose dispatch cancellation intent needs recovery.</summary>
public interface IOrchestrationDispatchCancellationIntentRepository
{
    /// <summary>Returns a bounded batch of instances with pending cancellation intent.</summary>
    Task<IReadOnlyCollection<OrchestrationInstance>> GetDispatchCancellationPending(
        int take,
        int skip,
        CancellationToken cancellationToken = default);
}
