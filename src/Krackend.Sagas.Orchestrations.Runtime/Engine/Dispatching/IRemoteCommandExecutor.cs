namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    public interface IRemoteCommandExecutor 
    {
        Task ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default);
    }
}
