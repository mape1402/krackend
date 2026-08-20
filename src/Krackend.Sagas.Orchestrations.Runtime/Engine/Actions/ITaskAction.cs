namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Actions
{
    public interface ITaskAction
    {
        Task ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
