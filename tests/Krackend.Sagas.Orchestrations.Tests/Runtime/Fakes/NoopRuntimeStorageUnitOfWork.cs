namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class NoopRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
{
    public bool AutoSaveChanges => true;

    public Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IRuntimeStorageTransaction>(new NoopRuntimeStorageTransaction());

    public IDisposable DeferAutoSave()
        => new NoopDisposable();

    public Task SaveChanges(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
