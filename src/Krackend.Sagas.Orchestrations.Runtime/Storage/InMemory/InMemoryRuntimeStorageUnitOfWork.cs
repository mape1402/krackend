using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        public bool AutoSaveChanges => true;

        public Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IRuntimeStorageTransaction>(new NoopTransaction());

        public IDisposable DeferAutoSave()
            => new NoopDisposable();

        public Task SaveChanges(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private sealed class NoopTransaction : IRuntimeStorageTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;
        }
    }
}
