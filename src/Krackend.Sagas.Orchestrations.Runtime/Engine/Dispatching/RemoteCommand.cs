namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching
{
    public class RemoteCommand
    {
        public RemoteCommandTransport RemoteCommandTransport { get; set; }

        public string Payload { get; set; }

        public string SettingsPayload { get; set; }
    }
}
