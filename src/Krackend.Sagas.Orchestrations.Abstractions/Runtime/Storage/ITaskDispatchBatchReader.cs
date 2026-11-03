using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Optional batch reads of dispatches for diagnostics.</summary>
public interface ITaskDispatchBatchReader
{
    Task<IReadOnlyCollection<TaskDispatch>> GetByIds(
        IReadOnlyCollection<Id> dispatchIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskDispatch>> GetByAttemptIds(
        IReadOnlyCollection<Id> attemptIds, CancellationToken cancellationToken = default);
}
