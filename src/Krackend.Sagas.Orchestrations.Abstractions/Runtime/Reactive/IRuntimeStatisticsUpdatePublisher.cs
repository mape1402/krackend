namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>Signals that a committed statistics projection has changed.</summary>
public interface IRuntimeStatisticsUpdatePublisher
{
    /// <summary>Notifies connected clients without affecting the committed batch.</summary>
    Task PublishAsync(CancellationToken cancellationToken = default);
}

/// <summary>Optionally distributes statistics notifications between runtime replicas.</summary>
public interface IRuntimeStatisticsFanout
{
    /// <summary>Raised after a different replica updates its statistics.</summary>
    event Action Updated;

    /// <summary>Distributes a statistics update notification.</summary>
    Task PublishStatisticsUpdatedAsync(CancellationToken cancellationToken = default);
}
