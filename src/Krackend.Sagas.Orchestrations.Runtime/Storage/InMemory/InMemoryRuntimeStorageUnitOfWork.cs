using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory
{
    internal sealed class InMemoryRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        public bool AutoSaveChanges => true;

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
    }
}
