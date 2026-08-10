using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists official runtime trigger intake records.
/// </summary>
public interface ITriggerIntakeRepository
{
    Task Create(TriggerIntake intake, CancellationToken cancellationToken = default);

    Task Update(TriggerIntake intake, CancellationToken cancellationToken = default);

    Task<TriggerIntake> GetById(Id intakeId, CancellationToken cancellationToken = default);

    Task<TriggerIntake> GetByIdempotencyKey(
        string environmentKey,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
