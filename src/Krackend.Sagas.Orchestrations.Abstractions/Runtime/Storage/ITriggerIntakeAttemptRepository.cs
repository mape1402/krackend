using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists attempts related to trigger intake acceptance and promotion.
/// </summary>
public interface ITriggerIntakeAttemptRepository
{
    Task Create(TriggerIntakeAttempt attempt, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TriggerIntakeAttempt>> GetByIntakeId(
        Id intakeId,
        CancellationToken cancellationToken = default);
}
