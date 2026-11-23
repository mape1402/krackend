namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class RuntimeStateTransaction : IRuntimeStateTransaction
{
    private readonly IRuntimeStorageUnitOfWork _unitOfWork;
    private int _batchDepth;

    public RuntimeStateTransaction(IRuntimeStorageUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (_batchDepth > 0)
        {
            await mutation(cancellationToken);
            // DecisionControl keeps the complete current-stage view for the active
            // lease. The outer activation (or the next durable dispatch graph) owns
            // the flush, so internal transitions do not become separate SQL batches.
            return;
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        using (_unitOfWork.DeferAutoSave())
        {
            await mutation(cancellationToken);
            await _unitOfWork.SaveChanges(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ExecuteDeferredAsync(
        Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (_batchDepth > 0)
        {
            await mutation(cancellationToken);
            return;
        }

        await ExecuteAsync(mutation, cancellationToken);
    }

    public async Task ExecuteBatchAsync(
        Func<CancellationToken, Task> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (!_unitOfWork.PreferBatchedEngineActivations)
        {
            await mutation(cancellationToken);
            return;
        }

        if (_batchDepth > 0)
        {
            await mutation(cancellationToken);
            return;
        }

        using (_unitOfWork.DeferAutoSave())
        {
            _batchDepth++;
            try
            {
                // SaveChanges already provides one atomic database transaction. Avoid
                // wrapping the complete engine activation in a longer outer transaction;
                // unrelated sagas must remain free to commit concurrently.
                await mutation(cancellationToken);
                await _unitOfWork.SaveChanges(cancellationToken);
            }
            finally
            {
                _batchDepth--;
            }
        }
    }
}
