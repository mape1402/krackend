namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class RuntimeStateTransaction : IRuntimeStateTransaction
{
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;

    public RuntimeStateTransaction(IRuntimeStorageUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await mutation(cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
