namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class NoopRuntimeStorageTransaction : IRuntimeStorageTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
