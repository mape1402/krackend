namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    public interface IRemoteCommandDispatcher
    {
        Task DispatchAsync(RemoteCommand command, CancellationToken cancellationToken = default);
    }
}
