namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;

public sealed class RuntimeStateTransactionTests
{
    [Fact]
    public void ConstructorRejectsNullUnitOfWork()
    {
        Assert.Throws<ArgumentNullException>(() => new RuntimeStateTransaction(null!));
    }

    [Fact]
    public async Task ExecuteAsyncCommitsAllWritesAfterTheMutationSucceeds()
    {
        var unitOfWork = new RecordingRuntimeStorageUnitOfWork();
        var transaction = new RuntimeStateTransaction(unitOfWork);
        var mutationExecuted = false;

        await transaction.ExecuteAsync(_ =>
        {
            mutationExecuted = true;
            return Task.CompletedTask;
        });

        Assert.True(mutationExecuted);
        Assert.Equal(1, unitOfWork.BeginTransactionCount);
        Assert.Equal(1, unitOfWork.DeferAutoSaveCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        Assert.Equal(1, unitOfWork.Transaction.CommitCount);
        Assert.True(unitOfWork.Transaction.WasDisposed);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotSaveOrCommitWhenTheMutationFails()
    {
        var unitOfWork = new RecordingRuntimeStorageUnitOfWork();
        var transaction = new RuntimeStateTransaction(unitOfWork);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.ExecuteAsync(
            _ => Task.FromException(new InvalidOperationException("write failed"))));

        Assert.Equal("write failed", exception.Message);
        Assert.Equal(1, unitOfWork.BeginTransactionCount);
        Assert.Equal(1, unitOfWork.DeferAutoSaveCount);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Equal(0, unitOfWork.Transaction.CommitCount);
        Assert.True(unitOfWork.Transaction.WasDisposed);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsNullMutation()
    {
        var transaction = new RuntimeStateTransaction(new RecordingRuntimeStorageUnitOfWork());

        await Assert.ThrowsAsync<ArgumentNullException>(() => transaction.ExecuteAsync(null!));
    }

    private sealed class RecordingRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        public bool AutoSaveChanges => true;

        public int BeginTransactionCount { get; private set; }

        public int DeferAutoSaveCount { get; private set; }

        public int SaveChangesCount { get; private set; }

        public RecordingRuntimeStorageTransaction Transaction { get; } = new();

        public Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginTransactionCount++;
            return Task.FromResult<IRuntimeStorageTransaction>(Transaction);
        }

        public IDisposable DeferAutoSave()
        {
            DeferAutoSaveCount++;
            return new NoopDisposable();
        }

        public Task SaveChanges(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeStorageTransaction : IRuntimeStorageTransaction
    {
        public int CommitCount { get; private set; }

        public bool WasDisposed { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
