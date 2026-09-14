namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    /// <summary>
    /// Identifies the transport used to dispatch a remote orchestration command.
    /// </summary>
    public enum RemoteCommandTransport
    {
        /// <summary>
        /// Dispatches the command through a messaging adapter.
        /// </summary>
        Messaging,

        /// <summary>
        /// Dispatches the command through an HTTP adapter.
        /// </summary>
        Http
    }
}
